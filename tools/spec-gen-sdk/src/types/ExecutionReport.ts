import { SDKAutomationState } from '../automation/sdkAutomationState';
import { requireJsonc } from '../utils/requireJsonc';
import { getTypeTransformer } from './validator';

export const executionReportSchema = requireJsonc(__dirname + '/ExecutionReportSchema.json');

export type ExecutionReport = {
  packages: PackageReport[];
  services?: string[];
  executionResult: SDKAutomationState;
  fullLogPath: string;
  filteredLogPath?: string;
  vsoLogPath?: string;
  sdkArtifactFolder?: string;
  sdkApiViewArtifactFolder?: string;
};

export type PackageReport = {
  packageName?: string;
  result: SDKAutomationState;
  artifactPaths?: string[];
  readmeMd?: string[];
  typespecProject?: string[]
  version?: string;
  apiViewArtifact?: string;
  language?: string;
  hasBreakingChange?: boolean;
  breakingChangeLabel?: string;
  shouldLabelBreakingChange: boolean;
  areBreakingChangeSuppressed?: boolean;
  presentBreakingChangeSuppressions?: string[];
  absentBreakingChangeSuppressions?: string[];
  installInstructions?: string;
};

export const getExecutionReport = getTypeTransformer<ExecutionReport>(executionReportSchema, 'ExecutionReport');
