#!/usr/bin/env node
import * as fs from 'fs';
import {
    ArtifactBlobUploader,
    ArtifactBlobUploaderContext,
    ArtifactPipelineUploader,
    ArtifactPipelineUploaderContext,
    GenerateAndBuildOutput,
    getGenerateAndBuildOutput,
    logger,
    requireJsonc,
} from '@azure-tools/sdk-generation-lib';

import {
    uploadBlobConfig,
    UploadBlobConfig,
    uploadPipelineArtifactConfig,
    UploadPipelineArtifactConfig,
} from './cliSchema/uploadArtifactConfig';

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

            const blobContext: ArtifactBlobUploaderContext = {
                azureStorageBlobSasUrl: config.azureStorageBlobSasUrl,
                azureBlobContainerName: config.azureBlobContainerName,
                language: config.language,
                pipelineBuildId: config.pipelineBuildId,
            };
            const artifactBlobUploader: ArtifactBlobUploader = new ArtifactBlobUploader(blobContext);
            const generateAndBuildOutputJson: GenerateAndBuildOutput = getGenerateAndBuildOutput(
                requireJsonc(config.generateAndBuildOutputFile)
            );

            await artifactBlobUploader.uploadSourceCode(generateAndBuildOutputJson);
            await artifactBlobUploader.uploadArtifacts(generateAndBuildOutputJson);
            break;
        case 'pipelineArtifact':
            uploadPipelineArtifactConfig.validate();
            const artifactConfig: UploadPipelineArtifactConfig = uploadPipelineArtifactConfig.getProperties();
            if (!fs.existsSync(artifactConfig.generateAndBuildOutputFile)) {
                throw new Error(`generateAndBuildOutputFile:${artifactConfig.generateAndBuildOutputFile} isn's exist!`);
            }

            const pipelineContext: ArtifactPipelineUploaderContext = {
                artifactDir: artifactConfig.artifactDir,
                language: artifactConfig.language,
            };
            const artifactPipelineUploader: ArtifactPipelineUploader = new ArtifactPipelineUploader(pipelineContext);
            const pipelineGenerateAndBuildOutputJson: GenerateAndBuildOutput = getGenerateAndBuildOutput(
                requireJsonc(artifactConfig.generateAndBuildOutputFile)
            );

            await artifactPipelineUploader.publishSourceCode(pipelineGenerateAndBuildOutputJson);
            await artifactPipelineUploader.publishArtifacts(pipelineGenerateAndBuildOutputJson);
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
