#!/usr/bin/env node
import * as fs from 'fs';

import {
    logger,
    uploadGenerateAndBuildFile,
    uploadLogsAndResult,
    publishResultConfig,
    PublishResultConfig,
    sendDB,
    TaskResult,
    requireJsonc,
} from '@azure-tools/sdk-generation-lib';

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
        uploadLogsAndResult(
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
        const result = await uploadGenerateAndBuildFile(
            generateAndBuildOutputFile,
            azureStorageBlobSasUrl,
            azureBlobContainerName,
            language,
            sdkGenerationName,
            pipelineBuildId
        );
        if (result.hasFailedResult) {
            logger.error('Upload package failed !');
        } else {
            console.log('Upload package Success !!!');
        }
    }

    // send result and codegeninfo to db
    if (sendDBFlag !== undefined) {
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

        sendDB(
            config.mongodb.server,
            config.mongodb.port,
            config.mongodb.username,
            config.mongodb.password,
            config.mongodb.database,
            config.mongodb.ssl,
            true,
            pipelineBuildId,
            taskResults,
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
