import { FailureType, SdkAutoOptions, WorkflowContext } from "../types/Workflow";
import { SpecConfig, SdkRepoConfig } from '../types/SpecConfig';
import { toolError } from '../utils/messageUtils';
import { getRepoKey, RepoKey } from '../utils/repo';
import { readFileSync } from 'fs';
import * as path from 'path';
import * as winston from 'winston';

/**
 * Resolves a repository-relative path that came from configuration, refusing any
 * value that would read outside the repository it is declared against.
 *
 * `path.join` and `path.resolve` leak through different inputs -- `join` follows
 * `..` out of the root while treating `/etc/passwd` as a relative segment, and
 * `resolve` honours absolute, drive-relative and UNC values instead -- so the
 * containment check below is what actually holds the boundary, rather than the
 * choice of joining function.
 *
 * Absolute values are rejected with their own message, and both path flavours
 * take part in every decision, so that a given configuration is accepted or
 * refused identically on Linux and Windows agents. Without that, a value such
 * as `..\..\config.json` would climb out of the root on a Windows agent while
 * landing inside it as a literal filename on a Linux one.
 */
export const resolveRepoRelativePath = (repoRootPath: string, configuredPath: string, settingName: string): string => {
  const normalizedPath = configuredPath.split('\\').join('/');

  if (path.posix.isAbsolute(normalizedPath) || path.win32.isAbsolute(normalizedPath)) {
    throw new Error(
      toolError(
        `The '${settingName}' value '${configuredPath}' must be a path relative to the repository root, but it is absolute. ` +
        `Please correct the config at the 'specificationRepositoryConfiguration.json' file under the root folder of the azure-rest-api-specs(-pr) repository`
      )
    );
  }

  const repoRoot = path.resolve(repoRootPath);
  const resolved = path.resolve(repoRoot, normalizedPath);
  const repoRootPrefix = repoRoot.endsWith(path.sep) ? repoRoot : `${repoRoot}${path.sep}`;

  if (resolved !== repoRoot && !resolved.startsWith(repoRootPrefix)) {
    throw new Error(
      toolError(
        `The '${settingName}' value '${configuredPath}' resolves outside the repository at '${repoRoot}'. ` +
        `Please correct the config at the 'specificationRepositoryConfiguration.json' file under the root folder of the azure-rest-api-specs(-pr) repository`
      )
    );
  }

  return resolved;
};

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