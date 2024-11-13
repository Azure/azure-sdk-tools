import { SDKAutomationState } from '../sdkAutomationState';

/**
 * The data that describes an SDK repository package.
 */
export interface SDKRepositoryPackageData {
  /**
   * The name of the package.
   */
  readonly name: string;
  /**
   * The relative path to the root of the package folder from the root of the SDK repository.
   */
  readonly relativeFolderPath: string;
  /**
   * The relative path to the root of the extra package folders from the root of the SDK repository.
   */
  extraRelativeFolderPaths: string[];
  /**
   * The current status of creating and uploading this package.
   */
  status: SDKAutomationState;
  /**
   * Message to be shown for this package.
   */
  messages: string[];
  /**
   * Does this package has breaking change.
   */
  hasBreakingChange?: boolean;
  /**
   * The URL of the blob where this SDK repository package's logs will be written to.
   */
  logsBlobUrl: string;
  /**
   * The URLs of the blobs where this SDK repository package's artifacts will be written to.
   */
  artifactBlobUrls?: string[];
  /**
   * The URLs of the generated apiView.
   */
  apiViewUrl?: string;
  /**
   * Used to indicate whether the package should be released as public
   */
  isPrivatePackage: boolean;
  /**
   * The installation instructions for this package.
   */
  installationInstructions?: string;
  /**
   * Lite installation instruction for this package.
   */
  liteInstallationInstruction?: string | undefined;
  /**
   * The URL of the blob where this SDK repository package's installation instructions will be
   * written to.
   */
  installationInstructionsBlobUrl?: string;
  /**
   * The URL to the created generation pull request for this SDK repository package.
   */
  generationPullRequestUrl?: string;
  /**
   * The URL to the diff page of the created generation pull request for this SDK repository package.
   */
  generationPullRequestDiffUrl?: string;
  /**
   * The URL to the integration pull request for this SDK repository package.
   */
  integrationPullRequestUrl?: string;
  /**
   * The files in this package that have changed.
   */
  readonly changedFilePaths: string[];
  /**
   * The name of the generation branch for this package.
   */
  readonly generationBranch: string;
  /**
   * The repository where the SDK's generation pull request and branch will be created. This is the
   * first repository where the automatically generated SDK will appear.
   */
  readonly generationRepository: string;
  /**
   * The URL to this package's generation repository.
   */
  readonly generationRepositoryUrl: string;
  /**
   * The name of the integration branch for this package.
   */
  readonly integrationBranch: string;
  /**
   * The repository where the SDK's integration/staging pull request and branch will be created. The
   * SDK's integration branch and pull request are where merged SDK generation pull requests will be
   * staged before they are merged and published in the main repository.
   */
  readonly integrationRepository: string;
  /**
   * Whether or not this SDK repository package's generation branch should be based off of the
   * integration branch or the main branch.
   */
  readonly useIntegrationBranch: boolean;
  /**
   * The main repository for the SDK. This is where the SDK packages are published from.
   */
  readonly mainRepository: string;
}
