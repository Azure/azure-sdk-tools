import { SdkAutoContext } from '../automation/entrypoint';
import { workflowInitGetDefaultBranch, workflowInitGetSpecConfig } from '../automation/workflow';
import { getRepoKey, RepoKey } from '../utils/githubUtils';
import { requireJsonc } from '../utils/requireJsonc';
import { getTypeTransformer } from './validator';
export const specConfigPath = 'specificationRepositoryConfiguration.json';

export const specConfigSchema = requireJsonc(__dirname + '/SpecConfigSchema.json');

export type SpecConfig = {
  sdkRepositoryMappings: { [sdkName: string]: SdkRepoConfig };
  overrides: { [sdkName: string]: SpecConfig };
  typespecEmitterToSdkRepositoryMapping: { [sdkName: string]: string };
};

export type SdkRepoConfig = {
  mainRepository: RepoKey;
  mainBranch: string;
  integrationRepository: RepoKey;
  secondaryRepository: RepoKey;
  secondaryBranch: string;
  integrationBranchPrefix: string;
  configFilePath: string;
};

const specConfigTransformer = getTypeTransformer<SpecConfig>(specConfigSchema, 'SpecConfig');
export const getSpecConfig = (data: unknown, specRepo: RepoKey) => {
  let specConfig = specConfigTransformer(data);
  const overrides = specConfig.overrides;
  if (overrides) {
    const overrideConfig = overrides[`${specRepo.owner}/${specRepo.name}`] ?? overrides[specRepo.name];
    if (overrideConfig) {
      specConfig = {
        ...specConfig,
        ...overrideConfig
      };
    }
  }

  return specConfig;
};

export const getSdkRepoConfig = async (context: SdkAutoContext) => {
  const data = await workflowInitGetSpecConfig(context);
  const specRepo = context.config.specRepo;
  const sdkName = context.config.sdkName;

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

  const specConfig = getSpecConfig(data, specRepo);
  let sdkRepoConfig = specConfig.sdkRepositoryMappings[sdkName];
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
    sdkRepoConfig.mainBranch ?? (await workflowInitGetDefaultBranch(context, sdkRepoConfig.mainRepository));
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
