import { FailureType, SdkAutoOptions, WorkflowContext } from "../types/Workflow";
import { SpecConfig, SdkRepoConfig } from '../types/SpecConfig';
import { toolError } from '../utils/messageUtils';
import { getRepoKey, RepoKey } from '../utils/repo';
import { readFileSync } from 'fs';
import * as winston from 'winston';

export const setFailureType = (context: WorkflowContext, failureType: FailureType) => {
  if (context.failureType !== FailureType.CodegenFailed) {
    context.failureType = failureType;
  }
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