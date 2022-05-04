import * as convict from 'convict';
import {taskBasicConfig, TaskBasicConfig} from '@azure-tools/sdk-generation-lib';

export class PublishLogCliConfig extends TaskBasicConfig{
    sdkGenerationServiceHost: string
    taskFullLog: string
    certPath: string;
    keyPath: string;
}

export const publishLogCliConfig = convict<PublishLogCliConfig>({
    sdkGenerationServiceHost: {
        default: 'localhost:3000',
        env: 'SDK_GENERATION_SERVICE_HOST',
        format: String
    },
    taskFullLog: {
        default: '/tmp/sdk-generation/pipe.full.log',
        env: 'TASK_FULL_LOG',
        format: String
    },
    certPath: {
        default: '/tmp/sdk-generation.pem',
        env: 'CERT_PATH',
        format: String
    },
    keyPath: {
        default: '/tmp/private.key',
        env: 'KEY_PATH',
        format: String
    },
    ...taskBasicConfig
});
