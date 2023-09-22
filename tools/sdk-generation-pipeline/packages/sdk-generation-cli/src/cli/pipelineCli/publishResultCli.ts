#!/usr/bin/env node
import {
    AzureSDKTaskName,
    BlobBasicContext,
    CodeGeneration,
    CompletedEvent,
    generateTotalResult,
    getTaskResults,
    InProgressEvent,
    logger,
    MongoConnectContext,
    PipelineRunEvent,
    QueuedEvent,
    requireJsonc,
    ResultBlobPublisher,
    ResultDBPublisher,
    ResultEventhubPublisher,
    SDKPipelineStatus,
    StorageType,
    TaskResult,
    Trigger
} from '@azure-tools/sdk-generation-lib';
import * as fs from 'fs';

import {
    ResultPublisherBlobInput,
    resultPublisherBlobInput,
    ResultPublisherDBCodeGenerationInput,
    resultPublisherDBCodeGenerationInput,
    ResultPublisherDBResultInput,
    resultPublisherDBResultInput,
    ResultPublisherEventHubInput,
    resultPublisherEventHubInput
} from '../../cliSchema/publishResultConfig';

async function publishBlob() {
    resultPublisherBlobInput.validate();
    const config: ResultPublisherBlobInput = resultPublisherBlobInput.getProperties();
    const context: BlobBasicContext = {
        pipelineBuildId: config.pipelineBuildId,
        sdkGenerationName: config.sdkGenerationName,
        azureStorageBlobSasUrl: config.azureStorageBlobSasUrl,
        azureBlobContainerName: config.azureBlobContainerName
    };
    const resultBlobPublisher: ResultBlobPublisher = new ResultBlobPublisher(context);
    await resultBlobPublisher.uploadLogsAndResult(config.logsAndResultPath, config.taskName as AzureSDKTaskName);
}

function initCodegen(config: ResultPublisherDBCodeGenerationInput, pipelineStatus: SDKPipelineStatus): CodeGeneration {
    const cg: CodeGeneration = new CodeGeneration();
    cg.name = config.sdkGenerationName;
    cg.service = config.service;
    cg.serviceType = config.serviceType;
    cg.tag = config.tag;
    cg.sdk = config.language;
    cg.swaggerRepo = config.swaggerRepo;
    cg.sdkRepo = config.sdkRepo;
    cg.codegenRepo = config.codegenRepo;
    cg.owner = config.owner ? config.owner : '';
    cg.type = config.triggerType;
    cg.status = pipelineStatus;
    cg.lastPipelineBuildID = config.pipelineBuildId;
    cg.swaggerPR = config.swaggerRepo;

    return cg;
}

function initMongoConnectContext(config: ResultPublisherDBCodeGenerationInput): MongoConnectContext {
    const mongoConnectContext: MongoConnectContext = {
        name: 'mongodb',
        type: 'mongodb',
        host: config.mongodb.server,
        port: config.mongodb.port,
        username: config.mongodb.username,
        password: config.mongodb.password,
        database: config.mongodb.database,
        ssl: config.mongodb.ssl,
        synchronize: true,
        logging: true
    };

    return mongoConnectContext;
}

async function publishDB(pipelineStatus: SDKPipelineStatus) {
    resultPublisherDBCodeGenerationInput.validate();
    const config: ResultPublisherDBCodeGenerationInput = resultPublisherDBCodeGenerationInput.getProperties();
    const cg: CodeGeneration = initCodegen(config, pipelineStatus);
    const mongoConnectContext: MongoConnectContext = initMongoConnectContext(config);
    const publisher: ResultDBPublisher = new ResultDBPublisher(mongoConnectContext);

    await publisher.connectDB();
    await publisher.sendSdkGenerationToDB(cg);

    if (pipelineStatus === 'completed') {
        resultPublisherDBResultInput.validate();
        const resultConfig: ResultPublisherDBResultInput = resultPublisherDBResultInput.getProperties();
        const taskResultsPathArray = JSON.parse(resultConfig.taskResultsPath);
        const taskResults: TaskResult[] = [];

        for (const taskResultPath of taskResultsPathArray) {
            if (fs.existsSync(taskResultPath)) {
                taskResults.push(requireJsonc(taskResultPath));
            } else {
                logger.error(`SendSdkGenerationToDB failed !, ${taskResultPath} isn't exist`);
            }
        }

        await publisher.sendSdkTaskResultToDB(resultConfig.pipelineBuildId, taskResults);
    }

    await publisher.close();
}

function getTrigger(config: ResultPublisherEventHubInput): Trigger {
    let trigger: Trigger;
    try {
        trigger = JSON.parse(config.trigger);
    } catch (error) {
        logger.error(`Wrong json format:` + config.trigger);
        throw new Error(error);
    }

    return trigger;
}

async function publishEventhub(pipelineStatus: SDKPipelineStatus) {
    resultPublisherEventHubInput.validate();
    const config: ResultPublisherEventHubInput = resultPublisherEventHubInput.getProperties();
    const trigger: Trigger = getTrigger(config);
    let event: PipelineRunEvent = undefined;
    const publisher: ResultEventhubPublisher = new ResultEventhubPublisher(config.eventHubConnectionString);

    switch (pipelineStatus) {
    case 'queued':
        event = {
            status: 'queued',
            trigger: trigger,
            pipelineBuildId: config.pipelineBuildId
        } as QueuedEvent;
        break;
    case 'in_progress':
        event = {
            status: 'in_progress',
            trigger: trigger,
            pipelineBuildId: config.pipelineBuildId
        } as InProgressEvent;
        break;
    case 'completed':
        if (!config.resultsPath || !config.logPath) {
            throw new Error(`Invalid completed event parameter!`);
        }

        const taskResults: TaskResult[] = getTaskResults(config.resultsPath);
        const taskTotalResult: TaskResult = generateTotalResult(taskResults, config.pipelineBuildId);
        event = {
            status: 'completed',
            trigger: trigger,
            pipelineBuildId: config.pipelineBuildId,
            logPath: config.logPath,
            result: taskTotalResult
        } as CompletedEvent;
        break;
    default:
        throw new Error(`Unsupported status: ` + (pipelineStatus as string));
    }
    await publisher.publishEvent(event);
    await publisher.close();
}

async function main() {
    const args = parseArgs(process.argv);
    const storageType = args['storageType'];
    const pipelineStatus = args['pipelineStatus'];

    switch (storageType as StorageType) {
    case StorageType.Blob:
        await publishBlob();
        break;
    case StorageType.Db:
        await publishDB(pipelineStatus);
        break;
    case StorageType.EventHub:
        await publishEventhub(pipelineStatus);
        break;
    default:
        throw new Error(`Unknown storageType:${storageType}!`);
    }
}

/**
 * Parse a list of command line arguments.
 * @param argv List of cli args(process.argv)
 */
const flagRegex = /^--([^=:]+)([=:](.+))?$/;
export function parseArgs(argv: string[]) {
    const result: any = {};
    for (const arg of argv) {
        const match = flagRegex.exec(arg);
        if (match) {
            const key = match[1];
            const rawValue = match[3];
            result[key] = rawValue;
        }
    }
    return result;
}

main().catch((e) => {
    logger.error(`${e.message}
    ${e.stack}`);
    process.exit(1);
});
