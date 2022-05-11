#!/usr/bin/env node
import * as fs from 'fs';
import {
    AzureBlobClient,
    GenerateAndBuildOutput,
    getGenerateAndBuildOutput,
    logger,
    publishArtifacts,
    publishSourceCode,
    requireJsonc,
    uploadArtifacts,
    uploadSourceCode,
} from '@azure-tools/sdk-generation-lib';

import {
    uploadBlobConfig,
    UploadBlobConfig,
    uploadPipelineArtifactConfig,
    UploadPipelineArtifactConfig,
} from './cliSchema/artifactUploaderConfig';

async function main() {
    const args = parseArgs(process.argv);
    const uploadTypt = args['uploadTypt'];

    switch (uploadTypt) {
        case 'blob':
            uploadBlobConfig.validate();
            const config: UploadBlobConfig = uploadBlobConfig.getProperties();
            if (!fs.existsSync(config.generateAndBuildOutputFile)) {
                throw new Error(`generateAndBuildOutputFile:${config.generateAndBuildOutputFile} isn's exist!`);
            }

            const azureBlobClient = new AzureBlobClient(config.azureStorageBlobSasUrl, config.azureBlobContainerName);
            const generateAndBuildOutputJson: GenerateAndBuildOutput = getGenerateAndBuildOutput(
                requireJsonc(config.generateAndBuildOutputFile)
            );
            await uploadSourceCode(
                azureBlobClient,
                config.language,
                config.pipelineBuildId,
                generateAndBuildOutputJson
            );
            await uploadArtifacts(azureBlobClient, config.language, config.pipelineBuildId, generateAndBuildOutputJson);
            break;
        case 'pipelineArtifact':
            uploadPipelineArtifactConfig.validate();
            const artifactConfig: UploadPipelineArtifactConfig = uploadPipelineArtifactConfig.getProperties();
            if (!fs.existsSync(artifactConfig.generateAndBuildOutputFile)) {
                throw new Error(`generateAndBuildOutputFile:${artifactConfig.generateAndBuildOutputFile} isn's exist!`);
            }

            const pipelineGenerateAndBuildOutputJson: GenerateAndBuildOutput = getGenerateAndBuildOutput(
                requireJsonc(artifactConfig.generateAndBuildOutputFile)
            );
            await publishSourceCode(
                artifactConfig.artifactDir,
                artifactConfig.language,
                pipelineGenerateAndBuildOutputJson
            );
            await publishArtifacts(
                artifactConfig.artifactDir,
                artifactConfig.language,
                pipelineGenerateAndBuildOutputJson
            );
            break;
        default:
            throw new Error(`unknown upload type ${uploadTypt}!`);
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
