/**
 * The mapping that determines where different branches/pull requests will be created within the SDK
 * Automation process.
 */
export interface SDKRepositoryMapping {
  /**
   * The repository where the SDK's generation pull request and branch will be created. This is the
   * first repository where the automatically generated SDK will appear. If this is not specified,
   * then the integrationRepository will be used instead.
   */
  generationRepository: string;
  /**
   * The repository where the SDK's integration/staging pull request and branch will be created. The
   * SDK's integration branch and pull request are where merged SDK generation pull requests will be
   * staged before they are merged and published in the main repository. If this is not specified,
   * then the mainRepository will be used instead.
   */
  integrationRepository: string;
  /**
   * The main repository for the SDK. This is where the SDK packages are published from.
   */
  mainRepository: string;
  /**
   * The secondary repository for the SDK since some SDK should exclude tooling scripts.
   * If this is not specified, then the mainRepository will be used instead.
   */
  secondaryRepository: string;
  /**
   * The prefix that will be applied to the beginning of integration branches. Defaults to
   * "sdkAutomation".
   */
  integrationBranchPrefix: string;
  /**
   * The name of the branch in the main repository that integration branches will be based on
   * (integration pull requests will merge into). Defaults to "master"."
   */
  mainBranch: string;
  /**
   * The name of the branch in the secondary repository that integration branches will be based on
   * (integration pull requests will merge into). Defaults to "master".
   */
  secondaryBranch: string;

  configFilePath: string;
}

/**
 * The default prefix that will be applied to integration branches.
 */
export const defaultIntegrationBranchPrefix = 'sdkAutomation';
/**
 * The default main branch in the main repository that integration branches will be based on.
 */
export const defaultMainBranch = 'master';

export const defaultConfigFilePath = 'swagger_to_sdk_config.json';
