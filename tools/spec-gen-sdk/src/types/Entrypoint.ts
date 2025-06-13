import { SwaggerToSdkConfig } from '../types/SwaggerToSdkConfig';
import { RepoKey } from '../utils/repo';
import { SpecConfig, SdkRepoConfig } from '../types/SpecConfig';
import * as winston from 'winston';

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