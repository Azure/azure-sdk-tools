import { Logger, getDefaultLogger } from '@azure/logger-js';
import {
  getRepository,
  RealGitHub,
  Repository,
  StringMap
} from '@ts-common/azure-js-dev-tools';
import { defaultIntegrationBranchPrefix, defaultMainBranch, SDKRepositoryMapping, defaultConfigFilePath } from './sdkRepositoryMapping';
import { readFileSync } from 'fs';
import { SDKAutomationContext } from './sdkAutomation';

const specConfigPath = 'specificationRepositoryConfiguration.json';

/**
 * A configuration object that can be put at the root of specification repositories.
 */
export interface SpecificationRepositoryConfiguration {
  /**
   * This is the base branch of pull requests that SDK Automation will be triggered for. Defaults to
   * master.
   */
  readonly sdkAutomationBaseBranch?: string;
  /**
   * A mapping of SDK repository names to the names of the SDK repositories that all interaction
   * should go to instead. This is mostly used for private repository support in order to redirect
   * any readme.md configuration file requests to the private SDK repository instead of the public
   * SDK repository.
   */
  readonly sdkRepositoryMappings?: StringMap<string | Partial<SDKRepositoryMapping>>;
  /**
   * Internal use only. If repo mapping missing owner, owner will be added based on this property.
   */
  specRepoOwner?: string;

  readonly overrides?: StringMap<SpecificationRepositoryConfiguration>;
}

/**
 * Get the specification repository configuration object from this pull request's specification
 * repository, or undefined if no configuration file could be found.
 */
export async function getSpecificationRepositoryConfiguration(
  context: SDKAutomationContext,
  repository: Repository | string
): Promise<SpecificationRepositoryConfiguration | undefined> {
  const { logger, github } = context;
  const repo = getRepository(repository);

  const githubClient = await (github as RealGitHub).getClient(repo);
  await logger.logSection(
    `Getting specification repository configuration from "${repo.owner}/${repo.name}/${specConfigPath}"...`
  );
  const rsp = await githubClient.repos.getContent({
    owner: repo.owner,
    repo: repo.name,
    path: specConfigPath,
    mediaType: {
      format: 'raw'
    }
  });

  const result = JSON.parse(rsp.data as unknown as string);

  return result;
}

export async function getLocalSpecificationRepositoryConfiguration(
  specRepoFolder: string,
  logger?: Logger
): Promise<SpecificationRepositoryConfiguration | undefined> {
  if (!logger) {
    logger = getDefaultLogger();
  }

  const filePath = `${specRepoFolder}/specificationRepositoryConfiguration.json`;
  await logger.logSection(`Getting specification repository configuration from "${filePath}"...`);

  const buffer = readFileSync(filePath);
  const result = JSON.parse(buffer.toString());

  return result;
}

/**
 * Get the SDKRepositoryMapping object associated with the SDK with the provided repository name.
 * @param sdkRepositoryName The name of the SDK repository to get the mapping object for.
 * @param configuration The configuration object to get the SDKRepositoryMapping object from.
 */
export async function getSDKRepositoryMapping(
  sdkRepositoryName: string,
  configuration?: SpecificationRepositoryConfiguration,
  logger?: Logger
): Promise<SDKRepositoryMapping> {
  if (typeof sdkRepositoryName !== 'string') {
    return sdkRepositoryName;
  }

  const resolveSdkRepoName = (repoName: string) =>
    repoName.indexOf('/') > 0 || configuration === undefined ? repoName : `${configuration.specRepoOwner}/${repoName}`;

  const originalRepositoryName: string = resolveSdkRepoName(sdkRepositoryName);
  let result = configuration?.sdkRepositoryMappings?.[sdkRepositoryName] ?? originalRepositoryName;
  if (!result) {
    return {
      generationRepository: originalRepositoryName,
      integrationRepository: originalRepositoryName,
      mainRepository: originalRepositoryName,
      integrationBranchPrefix: defaultIntegrationBranchPrefix,
      mainBranch: defaultMainBranch,
      secondaryRepository: originalRepositoryName,
      secondaryBranch: defaultMainBranch,
      configFilePath: defaultConfigFilePath
    };
  }

  if (typeof result === 'string') {
    result = resolveSdkRepoName(result);
    if (logger) {
      await logger.logInfo(`Mapping "${originalRepositoryName}" to "${result}".`);
    }
    return {
      generationRepository: result,
      integrationRepository: result,
      mainRepository: result,
      integrationBranchPrefix: defaultIntegrationBranchPrefix,
      mainBranch: defaultMainBranch,
      secondaryRepository: result,
      secondaryBranch: defaultMainBranch,
      configFilePath: defaultConfigFilePath
    };
  }

  result.mainRepository = resolveSdkRepoName(result.mainRepository ?? originalRepositoryName);
  result.integrationRepository = resolveSdkRepoName(result.integrationRepository ?? result.mainRepository);
  result.generationRepository = resolveSdkRepoName(result.generationRepository ?? result.integrationRepository);
  result.integrationBranchPrefix = result.integrationBranchPrefix ?? defaultIntegrationBranchPrefix;
  result.mainBranch = result.mainBranch ?? defaultMainBranch;
  result.secondaryRepository = resolveSdkRepoName(result.secondaryRepository ?? result.mainRepository);
  result.secondaryBranch = result.secondaryBranch ?? result.mainBranch;
  result.configFilePath = result.configFilePath ?? defaultConfigFilePath;
  if (logger) {
    await logger.logInfo(
      `Mapping "${originalRepositoryName}" generation repository to "${result.generationRepository}".`
    );
    await logger.logInfo(
      `Mapping "${originalRepositoryName}" integration repository to "${result.integrationRepository}".`
    );
    await logger.logInfo(`Mapping "${originalRepositoryName}" main repository to "${result.mainRepository}".`);
    await logger.logInfo(`Mapping "${originalRepositoryName}" ` +
      `secondary repository to "${result.secondaryRepository}".`);
    await logger.logInfo(`Using "${result.integrationBranchPrefix}" as the integration branch prefix.`);
    await logger.logInfo(`Using "${result.mainBranch}" as the main branch in the main repository.`);
    await logger.logInfo(`Using "${result.secondaryBranch}" as the secondary branch in the secondary repository.`);
  }
  return result as SDKRepositoryMapping;
}
