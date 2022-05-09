import * as convict from 'convict';

import { ServiceType } from '@azure-tools/sdk-generation-lib';

const stringMustHaveLength = (value: string) => {
    if (value.length === 0) {
        throw new Error('must not be empty');
    }
};

export class ResultPublisherBlobConfig {
    logsAndResultPath: string;
    pipelineBuildId: string;
    taskName: string;
    sdkGenerationName: string;
    azureStorageBlobSasUrl: string;
    azureBlobContainerName: string;
}

export const resultPublisherBlobConfig = convict<ResultPublisherBlobConfig>({
    logsAndResultPath: {
        default: '',
        format: stringMustHaveLength,
        env: 'LOGS_AND_RESULT_PATH',
    },
    pipelineBuildId: {
        default: '',
        env: 'PIPELINE_BUILDID',
        format: stringMustHaveLength,
    },
    taskName: {
        default: '',
        env: 'TASK_NAME',
        format: String,
    },
    sdkGenerationName: {
        default: '',
        env: 'SDKGENERATION_NAME',
        nullable: true,
        format: stringMustHaveLength,
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

export class ResultPublisherDBCGConfig {
    mongodb: {
        server: string;
        port: number;
        database: string;
        username: string;
        password: string;
        ssl: boolean;
    };
    pipelineBuildId: string;
    sdkGenerationName: string;
    service: string;
    serviceType: ServiceType;
    language: string;
    swaggerRepo: string;
    sdkRepo: string;
    codegenRepo: string;
    triggerType: string;
}

export const resultPublisherDBCGConfig = convict<ResultPublisherDBCGConfig>({
    mongodb: {
        server: {
            doc: 'The host used to connect db',
            default: '',
            env: 'sdkGenerationMongoDbHost',
            format: stringMustHaveLength,
        },
        port: {
            doc: 'The port used to connect db',
            default: 10225,
            env: 'sdkGenerationMongoDbPort',
            format: Number,
        },
        database: {
            doc: 'The database used to connect db',
            default: '',
            env: 'sdkGenerationMongoDbDatabase',
            format: stringMustHaveLength,
        },
        username: {
            doc: 'The username used to connect db',
            default: '',
            env: 'sdkGenerationMongoDbUsername',
            format: stringMustHaveLength,
        },
        password: {
            doc: 'The password used to connect db',
            default: '',
            env: 'sdkGenerationMongoDbPassword',
            format: stringMustHaveLength,
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            default: true,
            env: 'sdkGenerationMongoDbSsl',
            format: Boolean,
        },
    },
    pipelineBuildId: {
        default: '',
        env: 'PIPELINE_BUILDID',
        format: stringMustHaveLength,
    },
    sdkGenerationName: {
        default: '',
        env: 'SDKGENERATION_NAME',
        nullable: true,
        format: stringMustHaveLength,
    },
    service: {
        default: '',
        env: 'SERVICE',
        format: stringMustHaveLength,
    },
    serviceType: {
        default: '',
        env: 'SERVICETYPE',
        format: ['data-plane', 'resource-manager'],
    },
    language: {
        default: '',
        env: 'LANGUAGE',
        format: ['js', 'python', 'go', 'net', 'java'],
    },
    swaggerRepo: {
        default: '',
        env: 'SWAGGER_REPO',
        format: stringMustHaveLength,
    },
    sdkRepo: {
        default: '',
        env: 'SDK_REPO',
        format: stringMustHaveLength,
    },
    codegenRepo: {
        default: '',
        env: 'CODEGEN_REPO',
        format: stringMustHaveLength,
    },
    triggerType: {
        default: '',
        env: 'TRIGGER_TYPE',
        format: ['ad-hoc', 'ci', 'release'],
    },
});

export class ResultPublisherDBResultConfig {
    mongodb: {
        server: string;
        port: number;
        database: string;
        username: string;
        password: string;
        ssl: boolean;
    };
    pipelineBuildId: string;
    taskResultsPath: string;
}

export const resultPublisherDBResultConfig = convict<ResultPublisherDBResultConfig>({
    mongodb: {
        server: {
            doc: 'The host used to connect db',
            default: '',
            env: 'sdkGenerationMongoDbHost',
            format: stringMustHaveLength,
        },
        port: {
            doc: 'The port used to connect db',
            default: 10225,
            env: 'sdkGenerationMongoDbPort',
            format: Number,
        },
        database: {
            doc: 'The database used to connect db',
            default: '',
            env: 'sdkGenerationMongoDbDatabase',
            format: stringMustHaveLength,
        },
        username: {
            doc: 'The username used to connect db',
            default: '',
            env: 'sdkGenerationMongoDbUsername',
            format: stringMustHaveLength,
        },
        password: {
            doc: 'The password used to connect db',
            default: '',
            env: 'sdkGenerationMongoDbPassword',
            format: stringMustHaveLength,
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            default: true,
            env: 'sdkGenerationMongoDbSsl',
            format: Boolean,
        },
    },
    pipelineBuildId: {
        default: '',
        env: 'PIPELINE_BUILDID',
        format: stringMustHaveLength,
    },
    taskResultsPath: {
        default: '',
        env: 'TASK_RESULTS_PATH',
        format: stringMustHaveLength,
    },
});

export class ResultPublisherEventHubConfig {
    eventHubConnectionString: string;
    partitionKey?: string;
    pipelineBuildId: string;
    triggerName: string; // the agent, e.g. UnifiedPipeline, Release, individual
    pipelineTriggerSource?: string; // e.g. github, openapi_hub
    pullRequestNumber?: string; // the pull request number if it is triggerred by pr
    headSha?: string; // the CI commit
    unifiedPipelineBuildId?: string; // a unique build id unified pipeline assigned for each completed pipeline build id
    unifiedPipelineTaskKey?: string; // a unified pipeline task key, e.g. LintDiff, Semantic
    unifiedPipelineSubTaskKey?: string; // sub task key, for dynamic generated sub task message
    logPath?: string;
    resultPath?: string;
}

export const resultPublisherEventHubConfig = convict<ResultPublisherEventHubConfig>({
    eventHubConnectionString: {
        default: '',
        env: 'EVENTHUB_SAS_URL',
        format: stringMustHaveLength,
    },
    partitionKey: {
        default: '',
        env: 'PARTITIONKEY',
        nullable: true,
        format: String,
    },
    pipelineBuildId: {
        default: '',
        env: 'PIPELINE_BUILDID',
        format: stringMustHaveLength,
    },
    triggerName: {
        default: '',
        env: 'TRIGGER_NAME',
        format: ['UnifiedPipeline', 'Release', 'individual'],
    },
    pipelineTriggerSource: {
        default: null,
        env: 'PIPELINE_TRIGGER_SOURCE',
        nullable: true,
        format: ['github', 'openapi_hub'],
    },
    pullRequestNumber: {
        default: null,
        env: 'PULLREQUEST_NUMBER',
        nullable: true,
        format: stringMustHaveLength,
    },
    headSha: {
        default: null,
        env: 'HEAD_SHA',
        nullable: true,
        format: stringMustHaveLength,
    },
    unifiedPipelineBuildId: {
        default: null,
        env: 'UNIFIED_PIPELINE_BUILDID',
        nullable: true,
        format: stringMustHaveLength,
    },
    unifiedPipelineTaskKey: {
        default: null,
        env: 'UNIFIED_PIPELINE_TASKKEY',
        nullable: true,
        format: stringMustHaveLength,
    },
    unifiedPipelineSubTaskKey: {
        default: null,
        env: 'UNIFIED_PIPELINE_SUBTASKKEY',
        nullable: true,
        format: stringMustHaveLength,
    },
    logPath: {
        default: null,
        env: 'LOG_PATH',
        nullable: true,
        format: stringMustHaveLength,
    },
    resultPath: {
        default: null,
        env: 'RESULT_PATH',
        nullable: true,
        format: stringMustHaveLength,
    },
});
