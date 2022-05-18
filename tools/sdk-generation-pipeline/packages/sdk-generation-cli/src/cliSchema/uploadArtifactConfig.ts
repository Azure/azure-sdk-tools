import convict from 'convict';

import { assertNullOrEmpty } from '../utils/validator';

export class UploadBlobInput {
    generateAndBuildOutputFile: string;
    pipelineBuildId: string;
    language: string;
    azureStorageBlobSasUrl: string;
    azureBlobContainerName: string;
}

export const uploadBlobInput = convict<UploadBlobInput>({
    generateAndBuildOutputFile: {
        default: null,
        format: assertNullOrEmpty,
        env: 'GENERATE_AND_BUILD_OUTPUTFILE',
    },
    pipelineBuildId: {
        default: null,
        env: 'PIPELINE_BUILDID',
        format: assertNullOrEmpty,
    },
    language: {
        default: null,
        env: 'LANGUAGE',
        format: ['js', 'python', 'go', 'net', 'java'],
    },
    azureStorageBlobSasUrl: {
        default: null,
        env: 'AZURE_STORAGE_BLOB_SAS_URL',
        format: assertNullOrEmpty,
    },
    azureBlobContainerName: {
        default: 'sdk-generation',
        env: 'AZURE_BLOB_CONTAINER_NAME',
        format: assertNullOrEmpty,
    },
});

export class UploadPipelineArtifactInput {
    generateAndBuildOutputFile: string;
    artifactDir: string;
    language: string;
}

export const uploadPipelineArtifactInput = convict<UploadPipelineArtifactInput>({
    generateAndBuildOutputFile: {
        default: null,
        format: assertNullOrEmpty,
        env: 'GENERATE_AND_BUILD_OUTPUTFILE',
    },
    artifactDir: {
        default: null,
        env: 'ARTIFACT_DIR',
        format: assertNullOrEmpty,
    },
    language: {
        default: null,
        env: 'LANGUAGE',
        format: ['js', 'python', 'go', 'net', 'java'],
    },
});
