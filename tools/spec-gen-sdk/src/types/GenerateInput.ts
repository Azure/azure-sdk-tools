import { requireJsonc } from '../utils/requireJsonc';
import { InstallInstructionScriptInput } from './InstallInstructionScriptInput';

export const generateInputSchema = requireJsonc(__dirname + '/GenerateInputSchema.json');

export type GenerateInput = {
  specFolder: string;
  headSha: string;
  repoHttpsUrl: string;
  apiVersion?: string;
  runMode: string;
  sdkReleaseType: string;
  changedFiles: string[];
  relatedReadmeMdFiles?: string[];
  relatedTypeSpecProjectFolder?: string[];
  installInstructionInput: InstallInstructionScriptInput;
  autorestConfig?: string;
};
