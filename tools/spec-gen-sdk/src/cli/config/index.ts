import { configurationSchema } from './schema';

export const sdkAutomationCliConfig = configurationSchema.validate().getProperties();
