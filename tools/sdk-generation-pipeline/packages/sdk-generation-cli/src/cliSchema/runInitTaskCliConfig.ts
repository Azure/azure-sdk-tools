import * as convict from 'convict';
import {taskBasicConfig, TaskBasicConfig} from '@azure-tools/sdk-generation-lib';

export class RunInitTaskCliConfig extends TaskBasicConfig {
    initOutput: string;
}

export const runInitTaskCliConfig = convict<RunInitTaskCliConfig>({
    initOutput: {
        default: '/tmp/initOutput.json',
        env: 'INIT_OUTPUT_JSON',
        format: String
    },
    ...taskBasicConfig
});
