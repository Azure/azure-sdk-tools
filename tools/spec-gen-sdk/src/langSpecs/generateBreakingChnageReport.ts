import { RunOptions } from '@ts-common/azure-js-dev-tools';
import { SDKRepositoryPackage } from './sdkRepositoryPackage';

export type BreakingChangeReportOptions = RunOptions & {
  changedPackage: SDKRepositoryPackage;
  captureOutput: (logLine: string) => void,
  captureError: (logLine: string) => void,
  captureChangeLog: (logLine: string, containsBreakingChange?: boolean) => void,
};

export type GenerateBreakingChangeReportOptions = (option: BreakingChangeReportOptions) => Promise<boolean>;
