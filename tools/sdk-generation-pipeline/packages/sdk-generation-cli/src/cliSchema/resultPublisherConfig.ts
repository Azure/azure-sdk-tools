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
            format: stringMustHaveLength,
            default: '',
            env: 'sdkGenerationMongoDbHost',
        },
        port: {
            doc: 'The port used to connect db',
            format: Number,
            default: 10225,
            env: 'sdkGenerationMongoDbPort',
        },
        database: {
            doc: 'The database used to connect db',
            format: stringMustHaveLength,
            default: '',
            env: 'sdkGenerationMongoDbDatabase',
        },
        username: {
            doc: 'The username used to connect db',
            format: stringMustHaveLength,
            default: '',
            env: 'sdkGenerationMongoDbUsername',
        },
        password: {
            doc: 'The password used to connect db',
            format: stringMustHaveLength,
            default: '',
            env: 'sdkGenerationMongoDbPassword',
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            format: Boolean,
            default: true,
            env: 'sdkGenerationMongoDbSsl',
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
            format: stringMustHaveLength,
            default: '',
            env: 'sdkGenerationMongoDbHost',
        },
        port: {
            doc: 'The port used to connect db',
            format: Number,
            default: 10225,
            env: 'sdkGenerationMongoDbPort',
        },
        database: {
            doc: 'The database used to connect db',
            format: stringMustHaveLength,
            default: '',
            env: 'sdkGenerationMongoDbDatabase',
        },
        username: {
            doc: 'The username used to connect db',
            format: stringMustHaveLength,
            default: '',
            env: 'sdkGenerationMongoDbUsername',
        },
        password: {
            doc: 'The password used to connect db',
            format: stringMustHaveLength,
            default: '',
            env: 'sdkGenerationMongoDbPassword',
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            format: Boolean,
            default: true,
            env: 'sdkGenerationMongoDbSsl',
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
