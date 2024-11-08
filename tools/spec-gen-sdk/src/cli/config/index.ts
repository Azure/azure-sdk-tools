import { configurationSchema } from './schema';

export const sdkAutomationCliConfig = configurationSchema.validate().getProperties();
sdkAutomationCliConfig.githubApp.privateKey = sdkAutomationCliConfig.githubApp.privateKey.replace(/\\n/g, '\n');
