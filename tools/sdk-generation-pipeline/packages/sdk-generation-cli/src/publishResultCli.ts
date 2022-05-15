#!/usr/bin/env node
import * as fs from 'fs';
import { Connection, createConnection } from 'typeorm';
import {
    AzureSDKTaskName,
    BlobBasicContext,
    CodeGeneration,
    CompletedEvent,
    InProgressEvent,
    logger,
    MongoConnectContext,
    publishEvent,
    QueuedEvent,
    requireJsonc,
    ResultBlobPublisher,
    ResultDBPublisher,
    SDKPipelineStatus,
    StorageType,
    TaskResult,
    Trigger,
    UnifiedPipelineTrigger,
} from '@azure-tools/sdk-generation-lib';

import {
    resultPublisherBlobConfig,
    ResultPublisherBlobConfig,
    resultPublisherDBCGConfig,
    ResultPublisherDBCGConfig,
    resultPublisherDBResultConfig,
    ResultPublisherDBResultConfig,
    resultPublisherEventHubConfig,
    ResultPublisherEventHubConfig,
} from './cliSchema/publishResultConfig';

async function publishBlob() {
    resultPublisherBlobConfig.validate();
    const config: ResultPublisherBlobConfig = resultPublisherBlobConfig.getProperties();
    const context: BlobBasicContext = {
        pipelineBuildId: config.pipelineBuildId,
        sdkGenerationName: config.sdkGenerationName,
        azureStorageBlobSasUrl: config.azureStorageBlobSasUrl,
        azureBlobContainerName: config.azureBlobContainerName,
    };
    const resultBlobPublisher: ResultBlobPublisher = new ResultBlobPublisher(context);
    await resultBlobPublisher.uploadLogsAndResult(config.logsAndResultPath, config.taskName as AzureSDKTaskName);
}

function initCodegen(config: ResultPublisherDBCGConfig, pipelineStatus: SDKPipelineStatus): CodeGeneration {
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

function initMongoConnectContext(config: ResultPublisherDBCGConfig): MongoConnectContext {
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
        logging: true,
    };

    return mongoConnectContext;
}

async function publishDB(pipelineStatus: SDKPipelineStatus) {
    resultPublisherDBCGConfig.validate();
    const config: ResultPublisherDBCGConfig = resultPublisherDBCGConfig.getProperties();
    const publisher: ResultDBPublisher = new ResultDBPublisher();
    const cg: CodeGeneration = initCodegen(config, pipelineStatus);
    const mongoConnectContext: MongoConnectContext = initMongoConnectContext(config);

    await publisher.connectDB(mongoConnectContext);
    await publisher.sendSdkGenerationToDB(cg);

    if (pipelineStatus === 'completed') {
        resultPublisherDBResultConfig.validate();
        const resultConfig: ResultPublisherDBResultConfig = resultPublisherDBResultConfig.getProperties();
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

function getTrigger(config: ResultPublisherEventHubConfig): any {
    let trigger: any = undefined;
    if (
        config.pipelineTriggerSource &&
        config.pullRequestNumber &&
        config.headSha &&
        config.unifiedPipelineBuildId &&
        config.unifiedPipelineTaskKey
    ) {
        trigger = {
            name: config.triggerName,
            source: config.pipelineTriggerSource,
            pullRequestNumber: config.pullRequestNumber,
            headSha: config.headSha,
            unifiedPipelineBuildId: config.unifiedPipelineBuildId,
            unifiedPipelineTaskKey: config.unifiedPipelineTaskKey,
            unifiedPipelineSubTaskKey: config.unifiedPipelineSubTaskKey,
        } as UnifiedPipelineTrigger;
    } else if (config.triggerName) {
        trigger = { name: config.triggerName } as Trigger;
    } else {
        throw new Error(`Both UnifiedPipelineTrigger and Trigger ard invalid!`);
    }
    return trigger;
}

async function publishEventhub(pipelineStatus: SDKPipelineStatus) {
    resultPublisherEventHubConfig.validate();
    const config: ResultPublisherEventHubConfig = resultPublisherEventHubConfig.getProperties();
    let trigger: any = getTrigger(config);

    switch (pipelineStatus) {
        case 'queued':
            await publishEvent(config.eventHubConnectionString, {
                status: 'queued',
                trigger: trigger,
                pipelineBuildId: config.pipelineBuildId,
            } as QueuedEvent);
            break;
        case 'in_progress':
            await publishEvent(config.eventHubConnectionString, {
                status: 'in_progress',
                trigger: trigger,
                pipelineBuildId: config.pipelineBuildId,
            } as InProgressEvent);
            break;
        case 'completed':
            if (!config.resultPath || !config.logPath || !fs.existsSync(config.resultPath)) {
                throw new Error(`Invalid completed event parameter!`);
            }
            const taskResult: TaskResult = requireJsonc(config.resultPath);
            await publishEvent(config.eventHubConnectionString, {
                status: 'completed',
                trigger: trigger,
                pipelineBuildId: config.pipelineBuildId,
                logPath: config.logPath,
                result: taskResult,
            } as CompletedEvent);
            break;
    }
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
