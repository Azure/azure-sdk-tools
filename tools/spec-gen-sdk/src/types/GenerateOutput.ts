import { SDKAutomationState } from '../sdkAutomationState';
import { requireJsonc } from '../utils/requireJsonc';
import { InstallInstructionScriptOutput } from './InstallInstructionScriptOutput';
import { getTypeTransformer } from './validator';

export const generateOutputSchema = requireJsonc(__dirname + '/GenerateOutputSchema.json');

export type GenerateOutput = {
  packages: PackageResult[];
};

export type PackageResult = {
  packageName?: string;
  path: string[];
  readmeMd?: string[];
  typespecProject?: string[]
  version?: string;
  changelog?: {
    hasBreakingChange: boolean;
    content: string;
    breakingChangeItems?: string[];
  },
  artifacts?: string[];
  apiViewArtifact?: string;
  language?: string;
  installInstructions?: InstallInstructionScriptOutput;
  result: SDKAutomationState;
};

export const getGenerateOutput = getTypeTransformer<GenerateOutput>(generateOutputSchema, 'GenerateOutput');
