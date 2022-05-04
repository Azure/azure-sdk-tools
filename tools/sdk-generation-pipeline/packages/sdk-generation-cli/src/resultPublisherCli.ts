#!/usr/bin/env node
import * as fs from 'fs';
import { Connection, createConnection } from 'typeorm';
import {
    AzureSDKTaskName,
    CodeGeneration,
    logger,
    PipelineStatus,
    requireJsonc,
    sendSdkGenerationToDB,
    sendSdkTaskResultToDB,
    StorageType,
    TaskResult,
    TaskResultEntity,
    uploadLogsAndResult,
} from '@azure-tools/sdk-generation-lib';

import {
    resultPublisherBlobConfig,
    ResultPublisherBlobConfig,
    resultPublisherDBCGConfig,
    ResultPublisherDBCGConfig,
    resultPublisherDBResultConfig,
    ResultPublisherDBResultConfig,
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

async function publishDB(pipelineStatus: PipelineStatus) {
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

    if (pipelineStatus === PipelineStatus.complete) {
        resultPublisherDBResultConfig.validate();
        const resultConfig: ResultPublisherDBResultConfig = resultPublisherDBResultConfig.getProperties();
        const taskResultsPathArray = JSON.parse(resultConfig.taskResultsPath);
        const taskResults: TaskResult[] = [];

        for (const taskResultPath of taskResultsPathArray) {
            if (fs.existsSync(taskResultPath)) {
                taskResults.push(requireJsonc(taskResultPath));
            }
        }

        await sendSdkTaskResultToDB(resultConfig.pipelineBuildId, mongoDbConnection, taskResults);
    }

    await mongoDbConnection.close();
}

function publishEventhub(pipelineStatus: PipelineStatus) {}

async function main() {
    const args = parseArgs(process.argv);
    const storageType = args['storageType'];
    const pipelineStatus = args['pipelineStatus'];

    switch (storageType as StorageType) {
        case StorageType.Blob:
            await publishBlob();
            break;
        case StorageType.Db:
            publishDB(pipelineStatus);
            break;
        case StorageType.EventHub:
            publishEventhub(pipelineStatus);
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
