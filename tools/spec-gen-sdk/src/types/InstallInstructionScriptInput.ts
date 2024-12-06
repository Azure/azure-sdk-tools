import { requireJsonc } from '../utils/requireJsonc';
import { TriggerType } from './TriggerType';

export const installInstructionScriptInputSchema = requireJsonc(
  __dirname + '/InstallInstructionScriptInputSchema.json'
);

export type InstallInstructionScriptInput = {
  packageName?: string;
  artifacts?: string[];
  isPublic: boolean;
  downloadUrlPrefix: string;
  downloadCommandTemplate: string;
  trigger: TriggerType;
};
