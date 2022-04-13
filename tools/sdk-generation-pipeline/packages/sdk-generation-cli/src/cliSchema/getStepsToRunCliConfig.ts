import * as convict from 'convict';
import {taskBasicConfig, TaskBasicConfig} from '@azure-tools/sdk-generation-lib';

export class GetStepsToRunCliConfig extends TaskBasicConfig {
    skippedSteps: string
}

export const getStepsToRunCliConfig = convict<GetStepsToRunCliConfig>({
   skippedSteps: {
       default: '',
       env: 'SKIPPED_STEPS',
       format: String
   },
    ...taskBasicConfig
});
