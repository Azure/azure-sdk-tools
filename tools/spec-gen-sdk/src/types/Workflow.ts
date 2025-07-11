
import { default as Transport } from 'winston-transport';
import * as winston from 'winston';
import { PackageData } from './PackageData';
import { SDKAutomationState } from '../automation/sdkAutomationState';
import { SwaggerToSdkConfig } from '../types/SwaggerToSdkConfig';
import { RepoKey } from '../utils/repo';
import { SpecConfig, SdkRepoConfig } from '../types/SpecConfig';

export interface SdkAutoOptions {
  specRepo: RepoKey;
  sdkName: string;
  branchPrefix: string;
  localSpecRepoPath: string;
  localSdkRepoPath: string;
  tspConfigPath?: string;
  readmePath?: string;
  pullNumber?: string;
  apiVersion?: string;
  runMode: string;
  sdkReleaseType: string;
  specCommitSha: string;
  specRepoHttpsUrl: string;
  workingFolder: string;
  headRepoHttpsUrl?: string;
  headBranch?: string;
  runEnv: 'local' | 'azureDevOps' | 'test';
  version: string;
  skipSdkGenFromOpenapi?: string;
}

export type SdkAutoContext = {
  config: SdkAutoOptions;
  logger: winston.Logger;
  fullLogFileName: string;
  filteredLogFileName: string;
  htmlLogFileName: string;
  vsoLogFileName: string;
  specRepoConfig: SpecConfig;
  sdkRepoConfig: SdkRepoConfig;
  swaggerToSdkConfig: SwaggerToSdkConfig
  isPrivateSpecRepo: boolean;
};

/*
 * VsoLogs is a map of task names to log entries. Each log entry contains an array of errors and warnings.
 */
export type VsoLogs = Map<string, {
  errors?: string[];
  warnings?: string[];
}>;

export enum FailureType {
  CodegenFailed = 'Code Generator Failed',
  SpecGenSdkFailed = 'Spec-Gen-Sdk Failed'
}

export type WorkflowContext = SdkAutoContext & {
  stagedArtifactsFolder?: string;
  sdkArtifactFolder?: string;
  isSdkConfigDuplicated?: boolean;
  specConfigPath?: string;
  pendingPackages: PackageData[];
  handledPackages: PackageData[];
  status: SDKAutomationState;
  failureType?: FailureType;
  messages: string[];
  messageCaptureTransport: Transport;
  scriptEnvs: { [key: string]: string | undefined };
  tmpFolder: string;
  vsoLogs: VsoLogs;
};
