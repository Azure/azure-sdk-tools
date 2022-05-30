#!/usr/bin/env node
import {
    ArtifactBlobUploader,
    ArtifactBlobUploaderContext,
    GenerateAndBuildOutput,
    getGenerateAndBuildOutput,
    logger,
    requireJsonc
} from '@azure-tools/sdk-generation-lib';
import * as fs from 'fs';

import { UploadBlobInput, uploadBlobInput } from '../../cliSchema/uploadArtifactConfig';

async function main() {
    uploadBlobInput.validate();
    const config: UploadBlobInput = uploadBlobInput.getProperties();
    if (!fs.existsSync(config.generateAndBuildOutputFile)) {
        throw new Error(`generateAndBuildOutputFile:${config.generateAndBuildOutputFile} isn's exist!`);
    }

    const blobContext: ArtifactBlobUploaderContext = {
        azureStorageBlobSasUrl: config.azureStorageBlobSasUrl,
        azureBlobContainerName: config.azureBlobContainerName,
        language: config.language,
        pipelineBuildId: config.pipelineBuildId
    };
    const artifactBlobUploader: ArtifactBlobUploader = new ArtifactBlobUploader(blobContext);
    const generateAndBuildOutputJson: GenerateAndBuildOutput = getGenerateAndBuildOutput(
        requireJsonc(config.generateAndBuildOutputFile)
    );

    await artifactBlobUploader.uploadSourceCode(generateAndBuildOutputJson);
    await artifactBlobUploader.uploadArtifacts(generateAndBuildOutputJson);
}

main().catch((e) => {
    logger.error(`${e.message}
    ${e.stack}`);
    process.exit(1);
});
