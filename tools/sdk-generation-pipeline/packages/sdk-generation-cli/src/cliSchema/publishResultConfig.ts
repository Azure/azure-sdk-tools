#!/usr/bin/env node
import { ServiceType } from '@azure-tools/sdk-generation-lib';
import convict from 'convict';

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
        arg: 'logsAndResultPath'
    },
    pipelineBuildId: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'buildId'
    },
    taskName: {
        default: null,
        format: String,
        arg: 'taskName'
    },
    sdkGenerationName: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'sdkGenerationName'
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

export class ResultPublisherDBCodeGenerationInput {
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

export const resultPublisherDBCodeGenerationInput = convict<ResultPublisherDBCodeGenerationInput>({
    mongodb: {
        server: {
            doc: 'The host used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_HOST',
            format: assertNullOrEmpty
        },
        port: {
            doc: 'The port used to connect db',
            default: 10225,
            env: 'SDKGENERATION_MONGODB_PORT',
            format: Number
        },
        database: {
            doc: 'The database used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_DATABASE',
            format: assertNullOrEmpty
        },
        username: {
            doc: 'The username used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_USERNAME',
            format: assertNullOrEmpty
        },
        password: {
            doc: 'The password used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_PASSWORD',
            format: assertNullOrEmpty
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            default: true,
            env: 'SDKGENERATION_MONGODB_SSL',
            format: Boolean
        }
    },
    pipelineBuildId: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'buildId'
    },
    sdkGenerationName: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'sdkGenerationName'
    },
    service: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'service'
    },
    serviceType: {
        default: null,
        format: ['data-plane', 'resource-manager'],
        arg: 'serviceType'
    },
    language: {
        default: null,
        format: ['js', 'python', 'go', 'net', 'java'],
        arg: 'language'
    },
    swaggerRepo: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'swaggerRepo'
    },
    sdkRepo: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'sdkRepo'
    },
    codegenRepo: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'codegenRepo'
    },
    triggerType: {
        default: null,
        format: ['ad-hoc', 'ci', 'release'],
        arg: 'triggerType'
    },
    tag: {
        default: null,
        nullable: true,
        format: String,
        arg: 'tag'
    },
    owner: {
        default: null,
        nullable: true,
        format: String,
        arg: 'owner'
    },
    codePR: {
        default: null,
        nullable: true,
        format: String,
        arg: 'codePR'
    }
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
            format: assertNullOrEmpty
        },
        port: {
            doc: 'The port used to connect db',
            default: 10225,
            env: 'SDKGENERATION_MONGODB_PORT',
            format: Number
        },
        database: {
            doc: 'The database used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_DATABASE',
            format: assertNullOrEmpty
        },
        username: {
            doc: 'The username used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_USERNAME',
            format: assertNullOrEmpty
        },
        password: {
            doc: 'The password used to connect db',
            default: null,
            env: 'SDKGENERATION_MONGODB_PASSWORD',
            format: assertNullOrEmpty
        },
        ssl: {
            doc: 'Whether used ssl to connect db',
            default: true,
            env: 'SDKGENERATION_MONGODB_SSL',
            format: Boolean
        }
    },
    pipelineBuildId: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'buildId'
    },
    taskResultsPath: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'taskResultsPath'
    }
});

export class ResultPublisherEventHubInput {
    eventHubConnectionString: string;
    partitionKey?: string;
    pipelineBuildId: string;
    trigger: string;
    logPath?: string;
    resultsPath?: string;
}

export const resultPublisherEventHubInput = convict<ResultPublisherEventHubInput>({
    eventHubConnectionString: {
        default: null,
        env: 'EVENTHUB_SAS_URL',
        format: assertNullOrEmpty
    },
    partitionKey: {
        default: null,
        env: 'PARTITIONKEY',
        nullable: true,
        format: String
    },
    pipelineBuildId: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'buildId'
    },
    trigger: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'trigger'
    },
    logPath: {
        default: null,
        nullable: true,
        format: String,
        arg: 'logPath'
    },
    resultsPath: {
        doc: 'task result files array',
        default: null,
        nullable: true,
        format: String,
        arg: 'resultsPath'
    }
});
