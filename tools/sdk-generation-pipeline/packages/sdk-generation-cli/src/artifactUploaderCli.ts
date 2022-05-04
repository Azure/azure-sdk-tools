#!/usr/bin/env node
import * as fs from 'fs';
import {
    AzureBlobClient,
    GenerateAndBuildOutput,
    getGenerateAndBuildOutput,
    logger,
    requireJsonc,
    uploadArtifacts,
    uploadSourceCode,
} from '@azure-tools/sdk-generation-lib';

import { artifactUploaderConfig, ArtifactUploaderConfig } from './cliSchema/artifactUploaderConfig';

async function main() {
    artifactUploaderConfig.validate();
    const config: ArtifactUploaderConfig = artifactUploaderConfig.getProperties();
    if (!fs.existsSync(config.generateAndBuildOutputFile)) {
        throw new Error(`generateAndBuildOutputFile:${config.generateAndBuildOutputFile} isn's exist!`);
    }

    const azureBlobClient = new AzureBlobClient(config.azureStorageBlobSasUrl, config.azureBlobContainerName);
    const generateAndBuildOutputJson: GenerateAndBuildOutput = getGenerateAndBuildOutput(
        requireJsonc(config.generateAndBuildOutputFile)
    );
    await uploadSourceCode(azureBlobClient, config.language, config.pipelineBuildId, generateAndBuildOutputJson);
    await uploadArtifacts(azureBlobClient, config.language, config.pipelineBuildId, generateAndBuildOutputJson);
}

main().catch((e) => {
    logger.error(`${e.message}
    ${e.stack}`);
    process.exit(1);
});
