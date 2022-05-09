#!/usr/bin/env node
import * as fs from 'fs';
import { Connection, createConnection } from 'typeorm';
import {
    AzureSDKTaskName,
    CodeGeneration,
    CompletedEvent,
    InProgressEvent,
    logger,
    publishEvent,
    QueuedEvent,
    requireJsonc,
    SDKPipelineStatus,
    sendSdkGenerationToDB,
    sendSdkTaskResultToDB,
    StorageType,
    TaskResult,
    TaskResultEntity,
    Trigger,
    UnifiedPipelineTrigger,
    uploadLogsAndResult,
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
} from './cliSchema/resultPublisherConfig';

async function publishBlob() {
    resultPublisherBlobConfig.validate();
    const config: ResultPublisherBlobConfig = resultPublisherBlobConfig.getProperties();
    await uploadLogsAndResult(
        config.logsAndResultPath,
        config.pipelineBuildId,
        config.taskName as AzureSDKTaskName,
        config.sdkGenerationName,
        config.azureStorageBlobSasUrl,
        config.azureBlobContainerName
    );
}

async function publishDB(pipelineStatus: SDKPipelineStatus) {
    resultPublisherDBCGConfig.validate();
    const config: ResultPublisherDBCGConfig = resultPublisherDBCGConfig.getProperties();
    const mongoDbConnection: Connection = await createConnection({
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
        entities: [TaskResultEntity, CodeGeneration],
    });

    await sendSdkGenerationToDB(
        mongoDbConnection,
        config.pipelineBuildId,
        config.sdkGenerationName,
        config.service,
        config.serviceType,
        config.language,
        config.swaggerRepo,
        config.sdkRepo,
        config.codegenRepo,
        config.triggerType,
        pipelineStatus
    );

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

        await sendSdkTaskResultToDB(resultConfig.pipelineBuildId, mongoDbConnection, taskResults);
    }

    await mongoDbConnection.close();
}

async function publishEventhub(pipelineStatus: SDKPipelineStatus) {
    resultPublisherEventHubConfig.validate();
    const config: ResultPublisherEventHubConfig = resultPublisherEventHubConfig.getProperties();
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
    } else {
        trigger = { name: config.triggerName } as Trigger;
    }

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
