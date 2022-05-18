#!/usr/bin/env node
import convict from 'convict';

import { ServiceType } from '@azure-tools/sdk-generation-lib';
import { assertNullOrEmpty } from '../utils/validator';

export class ResultPublisherBlobInput {
    logsAndResultPath: string;
    pipelineBuildId: string;
    taskName: string;
    sdkGenerationName: string;
    azureStorageBlobSasUrl: string;
    azureBlobContainerName: string;
}

export const resultPublisherBlobInput = convict<ResultPublisherBlobInput>({
    logsAndResultPath: {
        default: null,
        format: assertNullOrEmpty,
        env: 'LOGS_AND_RESULT_PATH',
    },
    pipelineBuildId: {
        default: null,
        env: 'PIPELINE_BUILDID',
        format: assertNullOrEmpty,
    },
    taskName: {
        default: null,
        env: 'TASK_NAME',
        format: String,
    },
    sdkGenerationName: {
        default: null,
        env: 'SDKGENERATION_NAME',
        format: assertNullOrEmpty,
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

export class ResultPublisherDBCGInput {
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
    tag?: string;
    owner?: string;
    codePR?: string;
}

export const resultPublisherDBCGInput = convict<ResultPublisherDBCGInput>({
    mongodb: {
        server: {
            doc: 'The host used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_HOST',
            format: assertNullOrEmpty,
        },
        port: {
            doc: 'The port used to connect db',
            default: 10225,
            env: 'SDKGENERATION_MONGODB_PORT',
            format: Number,
        },
        database: {
            doc: 'The database used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_DATABASE',
            format: assertNullOrEmpty,
        },
        username: {
            doc: 'The username used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_USERNAME',
            format: assertNullOrEmpty,
        },
        password: {
            doc: 'The password used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_PASSWORD',
            format: assertNullOrEmpty,
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            default: true,
            env: 'SDKGENERATION_MONGODB_SSL',
            format: Boolean,
        },
    },
    pipelineBuildId: {
        default: null,
        env: 'PIPELINE_BUILDID',
        format: assertNullOrEmpty,
    },
    sdkGenerationName: {
        default: null,
        env: 'SDKGENERATION_NAME',
        format: assertNullOrEmpty,
    },
    service: {
        default: null,
        env: 'SERVICE',
        format: assertNullOrEmpty,
    },
    serviceType: {
        default: null,
        env: 'SERVICETYPE',
        format: ['data-plane', 'resource-manager'],
    },
    language: {
        default: null,
        env: 'LANGUAGE',
        format: ['js', 'python', 'go', 'net', 'java'],
    },
    swaggerRepo: {
        default: null,
        env: 'SWAGGER_REPO',
        format: assertNullOrEmpty,
    },
    sdkRepo: {
        default: null,
        env: 'SDK_REPO',
        format: assertNullOrEmpty,
    },
    codegenRepo: {
        default: null,
        env: 'CODEGEN_REPO',
        format: assertNullOrEmpty,
    },
    triggerType: {
        default: null,
        env: 'TRIGGER_TYPE',
        format: ['ad-hoc', 'ci', 'release'],
    },
    tag: {
        default: null,
        evn: 'TAG',
        nullable: true,
        format: String,
    },
    owner: {
        default: null,
        evn: 'OWNER',
        nullable: true,
        format: String,
    },
    codePR: {
        default: null,
        evn: 'OWNER',
        nullable: true,
        format: String,
    },
});

export class ResultPublisherDBResultInput {
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

export const resultPublisherDBResultInput = convict<ResultPublisherDBResultInput>({
    mongodb: {
        server: {
            doc: 'The host used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_HOST',
            format: assertNullOrEmpty,
        },
        port: {
            doc: 'The port used to connect db',
            default: 10225,
            env: 'SDKGENERATION_MONGODB_PORT',
            format: Number,
        },
        database: {
            doc: 'The database used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_DATABASE',
            format: assertNullOrEmpty,
        },
        username: {
            doc: 'The username used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_USERNAME',
            format: assertNullOrEmpty,
        },
        password: {
            doc: 'The password used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_PASSWORD',
            format: assertNullOrEmpty,
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            default: true,
            env: 'SDKGENERATION_MONGODB_SSL',
            format: Boolean,
        },
    },
    pipelineBuildId: {
        default: null,
        env: 'PIPELINE_BUILDID',
        format: assertNullOrEmpty,
    },
    taskResultsPath: {
        default: null,
        env: 'TASK_RESULTS_PATH',
        format: assertNullOrEmpty,
    },
});

export class ResultPublisherEventHubInput {
    eventHubConnectionString: string;
    partitionKey?: string;
    pipelineBuildId: string;
    trigger: string;
    logPath?: string;
    resultPath?: string;
}

export const resultPublisherEventHubInput = convict<ResultPublisherEventHubInput>({
    eventHubConnectionString: {
        default: null,
        env: 'EVENTHUB_SAS_URL',
        format: assertNullOrEmpty,
    },
    partitionKey: {
        default: null,
        env: 'PARTITIONKEY',
        nullable: true,
        format: String,
    },
    pipelineBuildId: {
        default: null,
        env: 'PIPELINE_BUILDID',
        format: assertNullOrEmpty,
    },
    trigger: {
        default: null,
        env: 'TRIGGER',
        format: assertNullOrEmpty,
    },
    logPath: {
        default: null,
        env: 'LOG_PATH',
        nullable: true,
        format: assertNullOrEmpty,
    },
    resultPath: {
        default: null,
        env: 'RESULT_PATH',
        nullable: true,
        format: assertNullOrEmpty,
    },
});
