import * as fs from 'fs';
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
import { generateReport, generateHtmlFromFilteredLog, saveFilteredLog } from './reportStatus';
import { SpecConfig, SdkRepoConfig, getSpecConfig, specConfigPath } from '../types/SpecConfig';
import { getSwaggerToSdkConfig, SwaggerToSdkConfig } from '../types/SwaggerToSdkConfig';

interface SdkAutoOptions {
  specRepo: RepoKey;
  sdkName: string;
  branchPrefix: string;
  localSpecRepoPath: string;
  localSdkRepoPath: string;
  tspConfigPath?: string;
  readmePath?: string;  
  pullNumber?: number;
  apiVersion?: string;
  specCommitSha: string;
  specRepoHttpsUrl: string;
  workingFolder: string;
  isTriggeredByPipeline: boolean;
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
  specRepoConfig: SpecConfig;
  sdkRepoConfig: SdkRepoConfig;
  swaggerToSdkConfig: SwaggerToSdkConfig
  isPrivateSpecRepo: boolean;
};


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

  const fullLogFileName = path.join(options.workingFolder, 'full.log');
  const filteredLogFileName = path.join(options.workingFolder, 'filtered.log');
  const htmlLogFileName = path.join(options.workingFolder, `${options.sdkName}-result.html`);
  if (fs.existsSync(fullLogFileName)) {
    fs.rmSync(fullLogFileName);
  }
  if (fs.existsSync(filteredLogFileName)) {
    fs.rmSync(filteredLogFileName);
  }
  logger.add(loggerFileTransport(fullLogFileName));
  logger.info(`Log to ${fullLogFileName}`);
  const localSpecConfigPath = path.join(options.localSpecRepoPath, specConfigPath);
  const specConfigContent = loadConfigContent(localSpecConfigPath, logger);  
  const specRepoConfig = getSpecConfig(specConfigContent, options.specRepo);

  const sdkRepoConfig = await getSdkRepoConfig(options, specRepoConfig);
  const swaggerToSdkConfigPath = path.join(options.localSdkRepoPath, sdkRepoConfig.configFilePath);
  const swaggerToSdkConfigContent = loadConfigContent(swaggerToSdkConfigPath, logger);
  const swaggerToSdkConfig = getSwaggerToSdkConfig(swaggerToSdkConfigContent);

  return {
    htmlLogFileName,
    config: options,
    logger,
    fullLogFileName,
    filteredLogFileName,
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
      sdkContext.logger.error(`FatalError: ${e.message}. Please refer to the inner logs for details or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
      workflowContext.status = 'failed';
      setFailureType(workflowContext, FailureType.PipelineFrameworkFailed);
      workflowContext.messages.push(e.message);
    }
    if (e.stack) {
      sdkContext.logger.error(`ErrorStack: ${e.stack}.`);
    }
  }
  if (workflowContext) {
    generateReport(workflowContext);
    saveFilteredLog(workflowContext);
    generateHtmlFromFilteredLog(workflowContext);
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
    const fileContent = fs.readFileSync(fileName).toString();
    const result = JSON.parse(fileContent);
    return result;
  }
  catch (error) {
    logger.error(`IOError: Fails to read config [${fileName}]'. Please ensure the spec config exists with the correct path and the content is valid. Error: ${error.message}`);
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
  let sdkRepoConfig = specRepoConfig.sdkRepositoryMappings[sdkName];
  if (sdkRepoConfig === undefined) {
    throw new Error(`ConfigError: SDK ${sdkName} is not defined in SpecConfig. Please add the related config at the 'specificationRepositoryConfiguration.json' file under the root folder of the azure-rest-api-specs(-pr) repository.`);
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
