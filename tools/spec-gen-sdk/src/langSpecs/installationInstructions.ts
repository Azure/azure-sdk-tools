import { SDKRepositoryPackage } from './sdkRepositoryPackage';

/**
 * The options that will be provided when generating installation instructions.
 */
export interface InstallationInstructionsOptions {
  /**
   * The name of the package.
   */
  packageName: string;
  /**
   * The URLs that the package artifacts can be downloaded from.
   */
  artifactUrls: string[];
  /**
   * The URL to the generation repository.
   */
  generationRepositoryUrl: string;
  /**
   * The name of the head branch for the SDK repository's generation pull request.
   */
  sdkRepositoryGenerationPullRequestHeadBranch: string;

  artifactDownloadCommand: (url: string, fileName: string) => string;

  package: SDKRepositoryPackage;
}

/**
 * The types that can be used to generate installation instructions.
 */
export type InstallationInstructions =
  | string
  | string[]
  | Promise<string | string[]>
  | ((options: InstallationInstructionsOptions) => string | string[] | Promise<string | string[]>);
