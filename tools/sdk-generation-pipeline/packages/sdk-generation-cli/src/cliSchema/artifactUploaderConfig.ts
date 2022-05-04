import * as convict from 'convict';

const stringMustHaveLength = (value: string) => {
    if (value.length === 0) {
        throw new Error('must not be empty');
    }
};

export class ArtifactUploaderConfig {
    generateAndBuildOutputFile: string;
    pipelineBuildId: string;
    language: string;
    azureStorageBlobSasUrl: string;
    azureBlobContainerName: string;
}

export const artifactUploaderConfig = convict<ArtifactUploaderConfig>({
    generateAndBuildOutputFile: {
        default: '',
        format: stringMustHaveLength,
        env: 'GENERATE_AND_BUILD_OUTPUTFILE',
    },
    pipelineBuildId: {
        default: '',
        env: 'PIPELINE_BUILDID',
        format: stringMustHaveLength,
    },
    language: {
        default: '',
        env: 'LANGUAGE',
        format: ['js', 'python', 'go', 'net', 'java'],
    },
    azureStorageBlobSasUrl: {
        default: '',
        env: 'AZURE_STORAGE_BLOB_SAS_URL',
        format: stringMustHaveLength,
    },
    azureBlobContainerName: {
        default: 'sdk-generation',
        env: 'AZURE_BLOB_CONTAINER_NAME',
        nullable: true,
        format: stringMustHaveLength,
    },
});
