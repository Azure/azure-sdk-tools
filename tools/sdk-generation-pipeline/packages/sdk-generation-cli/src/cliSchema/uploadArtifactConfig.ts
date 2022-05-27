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
        arg: 'generateAndBuildOutputFile'
    },
    pipelineBuildId: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'buildId'
    },
    language: {
        default: null,
        format: ['js', 'python', 'go', 'net', 'java'],
        arg: 'language'
    },
    azureStorageBlobSasUrl: {
        default: null,
        env: 'AZURE_STORAGE_BLOB_SAS_URL',
        format: assertNullOrEmpty
    },
    azureBlobContainerName: {
        default: 'sdk-generation',
        env: 'AZURE_BLOB_CONTAINER_NAME',
        format: assertNullOrEmpty
    }
});
