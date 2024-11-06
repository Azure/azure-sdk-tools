import { requireJsonc } from '../utils/requireJsonc';
import { InstallInstructionScriptInput } from './InstallInstructionScriptInput';
import { TriggerType } from './TriggerType';

export const generateInputSchema = requireJsonc(__dirname + '/GenerateInputSchema.json');

export type GenerateInput = {
  dryRun: boolean;
  specFolder: string;
  headSha: string;
  headRef: string;
  repoHttpsUrl: string;
  trigger: TriggerType;
  changedFiles: string[];
  relatedReadmeMdFiles?: string[];
  relatedTypeSpecProjectFolder?: string[];
  installInstructionInput: InstallInstructionScriptInput;
  autorestConfig?: string;
};
