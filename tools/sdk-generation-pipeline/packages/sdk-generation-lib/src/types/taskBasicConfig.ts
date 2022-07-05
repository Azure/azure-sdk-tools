import convict from 'convict';

export class TaskBasicConfig {
    sdkRepo: string;
    configPath: string;
    pipelineId: string;
    queuedAt: string;
    pipeLog: string;
    pipeFullLog: string;
    azureStorageBlobSasUrl: string;
    azureBlobContainerName: string;
    sdkGenerationName: string;
    buildId: string;
    taskName: string;
    mockServerLog: string;
}

export const taskBasicConfig = {
    sdkRepo: {
        default: '',
        env: 'SDK_REPO',
        format: String
    },
    configPath: {
        default: 'eng/codegen_to_sdk_config.json',
        env: 'CONFIG_PATH',
        format: String
    },
    pipelineId: {
        default: '',
        env: 'PIPELINE_ID',
        format: String
    },
    queuedAt: {
        default: '',
        env: 'QUEUE_AT',
        format: String
    },
    pipeLog: {
        default: '/tmp/sdk-generation/pipe.log',
        env: 'PIPE_LOG',
        format: String
    },
    pipeFullLog: {
        default: '/tmp/sdk-generation/pipe.full.log',
        env: 'PIPE_FULL_LOG',
        format: String
    },
    mockServerLog: {
        default: '',
        env: 'MOCK_SERVER_LOG',
        format: String
    },
    sdkGenerationName: {
        default: '',
        env: 'SDK_GENERATION_NAME',
        format: String
    },
    buildId: {
        default: '',
        env: 'BUILD_ID',
        format: String
    },
    taskName: {
        default: '',
        env: 'TASK_NAME',
        format: String
    },
    azureStorageBlobSasUrl: {
        default: '',
        env: 'AZURE_STORAGE_BLOB_SAS_URL',
        format: String
    },
    azureBlobContainerName: {
        default: 'sdks',
        env: 'AZURE_BLOB_CONTAINER_NAME',
        format: String
    }
};
export const getTaskBasicConfig = convict<TaskBasicConfig>(taskBasicConfig);
