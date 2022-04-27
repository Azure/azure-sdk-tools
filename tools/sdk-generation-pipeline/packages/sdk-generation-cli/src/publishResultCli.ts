import * as fs from 'fs';
import { Connection, createConnection } from 'typeorm';

import {
    AzureBlobClient,
    CodeGeneration,
    logger,
    publishResultConfig,
    PublishResultConfig,
    uploadArtifacts,
    uploadSourceCode,
    uploadLogsAndResult,
    requireJsonc,
    sendSdkGenerationToDB,
    sendSdkTaskResultToDB,
    TaskResult,
    TaskResultEntity,
    GenerateAndBuildOutput,
    getGenerateAndBuildOutput,
} from '@azure-tools/sdk-generation-lib';

async function SendDB(
    taskResultsPath: string,
    pipelineBuildId: string,
    sdkGenerationName: string,
    service: string,
    serviceType: string,
    language: string,
    swaggerRepo: string,
    sdkRepo: string,
    codegenRepo: string,
    triggerType: string,
    status: string,
) {
    const config: PublishResultConfig = publishResultConfig.getProperties();
    const taskResultsPathArray = JSON.parse(taskResultsPath);
    const taskResults: TaskResult[] = [];
    if (taskResultsPath !== undefined) {
        for (const taskResultPath of taskResultsPathArray) {
            if (fs.existsSync(taskResultPath)) {
                taskResults.push(requireJsonc(taskResultPath));
            }
        }
    }

    if (
        config.mongodb.server === undefined ||
        config.mongodb.port === undefined ||
        config.mongodb.username === undefined ||
        config.mongodb.password === undefined ||
        config.mongodb.database === undefined ||
        config.mongodb.ssl === undefined ||
        pipelineBuildId === undefined
    ) {
        throw new Error('db parameter is empty!');
    } else {
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

        sendSdkTaskResultToDB(pipelineBuildId, mongoDbConnection, taskResults);
        sendSdkGenerationToDB(
            mongoDbConnection,
            pipelineBuildId,
            sdkGenerationName,
            service,
            serviceType,
            language,
            swaggerRepo,
            sdkRepo,
            codegenRepo,
            triggerType,
            status,
        );

        await mongoDbConnection.close();
    }
}

async function main() {
    const args = parseArgs(process.argv);
    const logsAndResultPath = args['logsAndResultPath'];
    const createPR = args['createPR'];
    const sendDBFlag = args['sendDBFlag'];
    const generateAndBuildOutputFile = args['generateAndBuildOutputFile'];
    const azureStorageBlobSasUrl = process.env['AZURE_STORAGE_BLOB_SAS_URL'];
    const azureBlobContainerName = args['azureBlobContainerName'];
    const language = args['language'];
    const sdkGenerationName = args['sdkGenerationName'];
    const pipelineBuildId = args['pipelineBuildId'];
    const taskName = args['taskName'];
    const service = args['service'];
    const serviceType = args['serviceType'];
    const swaggerRepo = args['swaggerRepo'];
    const sdkRepo = args['sdkRepo'];
    const codegenRepo = args['codegenRepo'];
    const triggerType = args['triggerType'];
    const status = args['pipelineStatus'];
    const taskResultsPath = args['taskResultsPath'];

    // upload logs and task result
    if (logsAndResultPath !== undefined) {
        await uploadLogsAndResult(
            logsAndResultPath,
            pipelineBuildId,
            taskName,
            sdkGenerationName,
            azureStorageBlobSasUrl,
            azureBlobContainerName
        );
    }

    // upload package to blob
    if (generateAndBuildOutputFile !== undefined) {
        if (!fs.existsSync(generateAndBuildOutputFile)) {
            throw new Error(`generateAndBuildOutputFile:${generateAndBuildOutputFile} isn's exist!`);
        }
        if (azureStorageBlobSasUrl === undefined) {
            throw new Error('azureStorageBlobSasUrl  is empty!');
        }
        if (azureBlobContainerName === undefined) {
            throw new Error('azureBlobContainerName  is empty!');
        }
        const azureBlobClient = new AzureBlobClient(
            azureStorageBlobSasUrl,
            azureBlobContainerName
        );
        const generateAndBuildOutputJson: GenerateAndBuildOutput = getGenerateAndBuildOutput(
            requireJsonc(generateAndBuildOutputFile)
        );
        await uploadSourceCode(azureBlobClient, language, pipelineBuildId, generateAndBuildOutputJson);
        await uploadArtifacts(azureBlobClient, language, pipelineBuildId, generateAndBuildOutputJson)
    }

    // send result and codegeninfo to db
    if (sendDBFlag !== undefined) {
        SendDB(
            taskResultsPath,
            pipelineBuildId,
            sdkGenerationName,
            service,
            serviceType,
            language,
            swaggerRepo,
            sdkRepo,
            codegenRepo,
            triggerType,
            status,
        );
    }

    if (createPR !== undefined) {
        // TODO: Create PR in release
    }

    return;
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
