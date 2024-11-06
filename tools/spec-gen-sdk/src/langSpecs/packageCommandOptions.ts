import { RepositoryCommandOptions } from './repositoryCommandOptions';
import { SDKRepositoryPackageData } from './sdkRepositoryPackage';

/**
 * Options that can be used within a package command.
 */
export interface PackageCommandOptions extends RepositoryCommandOptions {
  /**
   * The path to the package folder relative to the repository folder.
   */
  readonly relativePackageFolderPath: string;
  /**
   * The rooted path to the package folder.
   */
  readonly rootedPackageFolderPath: string;
  /**
   * The rooted file paths to the files in the SDK repository that were changed after AutoRest was
   * run.
   */
  readonly changedFilePaths: string[];
  /**
   * The data associated with the package.
   */
  readonly packageData: SDKRepositoryPackageData;

  /**
   * Enable pipeline Build.BuildID
   */
  readonly buildID: string;
  /**
   * Package index, starts from 0
   */
  readonly packageIndex: number;
}
