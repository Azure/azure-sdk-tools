import { requireJsonc } from '../utils/requireJsonc';
import { getTypeTransformer } from './validator';

export const installInstructionScriptOutputSchema = requireJsonc(
  __dirname + '/InstallInstructionScriptOutputSchema.json'
);

export type InstallInstructionScriptOutput = {
  full: string;
  lite?: string;
};

export const getInstallInstructionScriptOutput = getTypeTransformer<InstallInstructionScriptOutput>(
  installInstructionScriptOutputSchema, 'InstallInstructionScriptOutput'
);
