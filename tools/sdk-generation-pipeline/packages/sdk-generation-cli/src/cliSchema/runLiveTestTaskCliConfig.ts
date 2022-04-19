import * as convict from 'convict';
import {taskBasicConfig, TaskBasicConfig} from '@azure-tools/sdk-generation-lib';

export class RunLiveTestTaskCliConfig extends TaskBasicConfig {
    packageFolders: string;
    liveTestInputJson: string;
    liveTestOutputJson: string;
}

export const runLiveTestTaskCliConfig = convict<RunLiveTestTaskCliConfig>({
    packageFolders: {
        default: '',
        env: 'PACKAGE_FOLDERS',
        format: String
    },
    liveTestInputJson: {
        default: '/tmp/liveTestInput.json',
        env: 'Live_TEST_INPUT_JSON',
        format: String
    },
    liveTestOutputJson: {
        default: '/tmp/liveTestOutput.json',
        env: 'Live_TEST_OUTPUT_JSON',
        format: String
    },
    ...taskBasicConfig
});
