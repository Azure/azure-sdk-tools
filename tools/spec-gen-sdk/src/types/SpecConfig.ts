import { RepoKey } from '../utils/githubUtils';
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
