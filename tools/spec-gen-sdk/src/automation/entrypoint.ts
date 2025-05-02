import {existsSync, mkdirSync, readFileSync, rmSync} from 'fs';
import * as winston from 'winston';
import { getRepoKey, RepoKey } from '../utils/repo';
import {
  FailureType,
  setFailureType,
  WorkflowContext,
  workflowInit,
  workflowMain
} from './workflow';
import {
  loggerConsoleTransport,
  loggerDevOpsTransport,
  loggerFileTransport,
  loggerTestTransport,
  loggerWaitToFinish,
  sdkAutoLogLevels
} from './logging';
import path from 'path';
import { generateReport, generateHtmlFromFilteredLog, saveFilteredLog, saveVsoLog } from './reportStatus';
import { SpecConfig, SdkRepoConfig, getSpecConfig, specConfigPath } from '../types/SpecConfig';
import { getSwaggerToSdkConfig, SwaggerToSdkConfig } from '../types/SwaggerToSdkConfig';
import { extractPathFromSpecConfig } from '../utils/utils';
import { toolError } from '../utils/messageUtils';

interface SdkAutoOptions {
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

export const getSdkAutoContext = async (options: SdkAutoOptions): Promise<SdkAutoContext> => {
  const logger = winston.createLogger({
    levels: sdkAutoLogLevels.levels
  });

  if (options.runEnv === 'local') {
    logger.add(loggerConsoleTransport());
  } else if (options.runEnv === 'azureDevOps') {
    logger.add(loggerDevOpsTransport());
  } else if (options.runEnv === 'test') {
    logger.add(loggerTestTransport());
  }

  // Extract relevant parts from tspConfigPath or readmePath
  const fileNamePrefix = extractPathFromSpecConfig(options.tspConfigPath, options.readmePath)
  const logFolder = path.join(options.workingFolder, 'out/logs');
  if (!existsSync(logFolder)) {
    mkdirSync(logFolder, { recursive: true });
  }
  const fullLogFileName = path.join(logFolder, `${fileNamePrefix}-full.log`);
  const filteredLogFileName = path.join(logFolder, `${fileNamePrefix}-filtered.log`);
  const vsoLogFileName = path.join(logFolder, `${fileNamePrefix}-vso.log`);
  const htmlLogFileName = path.join(logFolder, `${fileNamePrefix}-${options.sdkName.substring("azure-sdk-for-".length)}-gen-result.html`);
  if (existsSync(fullLogFileName)) {
    rmSync(fullLogFileName);
  }
  if (existsSync(filteredLogFileName)) {
    rmSync(filteredLogFileName);
  }
  if (existsSync(htmlLogFileName)) {
    rmSync(htmlLogFileName);
  }
  if (existsSync(vsoLogFileName)) {
    rmSync(vsoLogFileName);
  }
  logger.add(loggerFileTransport(fullLogFileName));
  logger.info(`Log to ${fullLogFileName}, spec-gen-sdk version: ${options.version}`);
  const localSpecConfigPath = path.join(options.localSpecRepoPath, specConfigPath);
  const specConfigContent = loadConfigContent(localSpecConfigPath, logger);  
  const specRepoConfig = getSpecConfig(specConfigContent, options.specRepo);

  const sdkRepoConfig = await getSdkRepoConfig(options, specRepoConfig);
  const swaggerToSdkConfigPath = path.join(options.localSdkRepoPath, sdkRepoConfig.configFilePath);
  const swaggerToSdkConfigContent = loadConfigContent(swaggerToSdkConfigPath, logger);
  const swaggerToSdkConfig = getSwaggerToSdkConfig(swaggerToSdkConfigContent);

  return {
    config: options,
    logger,
    fullLogFileName,
    filteredLogFileName,
    vsoLogFileName,
    htmlLogFileName,
    specRepoConfig,
    sdkRepoConfig,
    swaggerToSdkConfig,
    isPrivateSpecRepo: options.specRepo.name.endsWith('-pr')
  };
};

export const sdkAutoMain = async (options: SdkAutoOptions) => {
  const sdkContext = await getSdkAutoContext(options);
  let workflowContext: WorkflowContext | undefined = undefined;

  try {
    workflowContext = await workflowInit(sdkContext);
    await workflowMain(workflowContext);
  } catch (e) {
    if (workflowContext) {
      const message = "Refer to the inner logs for details or report this issue through https://aka.ms/azsdk/support/specreview-channel.";
      sdkContext.logger.error(message);
      workflowContext.status = workflowContext.status === 'notEnabled' ? workflowContext.status : 'failed';
      setFailureType(workflowContext, FailureType.SpecGenSdkFailed);
      workflowContext.messages.push(e.message);
      vsoLogError(workflowContext, message);
      if (e.stack) {
        vsoLogError(workflowContext, `ErrorStack: ${e.stack}.`);
      }
    }
    if (e.stack) {
      sdkContext.logger.error(`ErrorStack: ${e.stack}.`);
    }
  }
  if (workflowContext) {
    saveFilteredLog(workflowContext);
    generateHtmlFromFilteredLog(workflowContext);
    generateReport(workflowContext);
    saveVsoLog(workflowContext);
  }
  await loggerWaitToFinish(sdkContext.logger);
  return workflowContext?.status;
};

export const getLanguageByRepoName = (repoName: string) => {
  if (!repoName) {
    return 'unknown';
  } else if (repoName.includes('js')) {
    return 'JavaScript';
  } else if (repoName.includes('go')) {
    return 'Go';
  } else if (repoName.includes('net')) {
    return '.Net';
  } else if (repoName.includes('java')) {
    return 'Java';
  } else if (repoName.includes('python')) {
    return 'Python';
  } else {
    return repoName;
  }
};

export const loadConfigContent = (fileName: string, logger: winston.Logger) => {
  logger.info(`Load config file: ${fileName}`);
  try {
    const fileContent = readFileSync(fileName).toString();
    const result = JSON.parse(fileContent);
    return result;
  }
  catch (error) {
    logger.error(toolError(`Fails to read config [${fileName}]'. Please ensure the spec config exists with the correct path and the content is valid. Error: ${error.message}`));
    throw error;
  }
};

export const getSdkRepoConfig = async (options: SdkAutoOptions, specRepoConfig: SpecConfig) => {
  const specRepo = options.specRepo;
  const sdkName = options.sdkName;
  const getConfigRepoKey = (repo: RepoKey | string | undefined, fallback: RepoKey): RepoKey => {
    if (repo === undefined) {
      return fallback;
    }
    const repoKey = getRepoKey(repo);
    if (!repoKey.owner) {
      repoKey.owner = fallback.owner;
    }
    return repoKey;
  };
  let sdkRepositoryMappings = specRepoConfig.sdkRepositoryMappings;
  if (specRepo.name.endsWith("-pr")) {
    sdkRepositoryMappings = specRepoConfig.overrides[`${specRepo.owner}/${specRepo.name}`]?.sdkRepositoryMappings ?? specRepoConfig.overrides[`Azure/${specRepo.name}`]?.sdkRepositoryMappings;
  }
  if (!sdkRepositoryMappings) {
    throw new Error(toolError(`SDK repository mappings cannot be found in SpecConfig for ${specRepo.owner}/${specRepo.name}. Please add the related config at the 'specificationRepositoryConfiguration.json' file under the root folder of the azure-rest-api-specs(-pr) repository`));
  }
  let sdkRepoConfig = sdkRepositoryMappings[sdkName];
  if (sdkRepoConfig === undefined) {
    throw new Error(toolError(`SDK ${sdkName} is not defined in SpecConfig. Please add the related config at the 'specificationRepositoryConfiguration.json' file under the root folder of the azure-rest-api-specs(-pr) repository`));
  }

  if (typeof sdkRepoConfig === 'string') {
    sdkRepoConfig = {
      mainRepository: getRepoKey(sdkRepoConfig)
    } as SdkRepoConfig;
  }

  sdkRepoConfig.mainRepository = getConfigRepoKey(sdkRepoConfig.mainRepository, {
    owner: specRepo.owner,
    name: sdkName
  });
  sdkRepoConfig.mainBranch =
    sdkRepoConfig.mainBranch ?? "main";
  sdkRepoConfig.integrationRepository = getConfigRepoKey(
    sdkRepoConfig.integrationRepository,
    sdkRepoConfig.mainRepository
  );
  sdkRepoConfig.integrationBranchPrefix = sdkRepoConfig.integrationBranchPrefix ?? 'sdkAutomation';
  sdkRepoConfig.secondaryRepository = getConfigRepoKey(sdkRepoConfig.secondaryRepository, sdkRepoConfig.mainRepository);
  sdkRepoConfig.secondaryBranch = sdkRepoConfig.secondaryBranch ?? sdkRepoConfig.mainBranch;
  sdkRepoConfig.configFilePath = sdkRepoConfig.configFilePath ?? 'swagger_to_sdk_config.json';

  return sdkRepoConfig;
};

export function vsoLogError(context: WorkflowContext, message, task: string = "spec-gen-sdk"): void {
  vsoLogErrors(context, [message], task);
}

export function vsoLogWarning(context: WorkflowContext, message, task: string = "spec-gen-sdk"): void {
  vsoLogWarnings(context, [message], task);
}
export function vsoLogErrors(
  context: WorkflowContext,
  errors: string[],
  task: string = "spec-gen-sdk"
): void {
  if (context.config.runEnv !== 'azureDevOps') {
    return;
  }
  if (!context.vsoLogs.has(task)) {
    // Create a new entry with the initial errors
    context.vsoLogs.set(task, { errors: [...errors] });
    return;
  }

  // If the task already exists, merge the new errors into the existing array
  const logEntry = context.vsoLogs.get(task);
  if (logEntry?.errors) {
    logEntry.errors.push(...errors);
  } else {
    logEntry!.errors = [...errors];
  }
}

export function vsoLogWarnings(
  context: WorkflowContext,
  warnings: string[],
  task: string = "spec-gen-sdk"
): void {
  if (context.config.runEnv !== 'azureDevOps') {
    return;
  }
  if (!context.vsoLogs.has(task)) {
    // Create a new entry with the initial errors
    context.vsoLogs.set(task, { warnings: [...warnings] });
    return;
  }

  // If the task already exists, merge the new errors into the existing array
  const logEntry = context.vsoLogs.get(task);
  if (logEntry?.warnings) {
    logEntry.warnings.push(...warnings);
  } else {
    logEntry!.warnings = [...warnings];
  }
}
