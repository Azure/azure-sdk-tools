import { PackageCommand } from './packageCommand';
import { InstallationInstructions } from './installationInstructions';
import { RepositoryCommand } from './repositoryCommand';
import { GenerateBreakingChangeReportOptions } from './generateBreakingChnageReport';
import { SDKRepository } from '../sdkRepository';
import { GithubLabel } from '../utils/githubUtils';
import { SDKRepositoryPackage } from './sdkRepositoryPackage';
import { Logger } from '@azure/logger-js';

/**
 * Process mod's generate method for each readmeMdFileUrl.
 * Batch for invoking runGeneration method for all readmeMdFileUrl first.
 * Sequencial for processing each readmeMdFileUrl wholly one by one.
 */
export enum ReadmeMdFileProcessMod {
  Batch,
  Sequencial
}

/**
 * A configuration for how to interact with repositories in a specific programming language.
 */
export interface LanguageConfiguration {
  /**
   * The name of the programming language.
   */
  name: string;
  /**
   * The package name of the generator that will be used to generate this language's packages.
   */
  generatorPackageName: string;
  /**
   * Process mod's generate method for each readmeMdFileUrl.
   */
  readmeMdFileProcessMod?: ReadmeMdFileProcessMod;
  /**
   * Aliases that the programming language may also be known by.
   */
  aliases?: string[];
  /**
   * Commands that will be run to generate source code that will later be combined into packages. If
   * this is not specified, then it will default to just running AutoRest.
   */
  generationCommands?: RepositoryCommand | RepositoryCommand[];
  /**
   * A function that returns the name of a package.
   */
  packageNameCreator?: (
    rootedRepositoryFolderPath: string,
    relativePackageFolderPath: string,
    readmeMdFileUrl: string
  ) => string | Promise<string>;

  packageNameAltPrefix?: string;

  /**
   * A function that customize the message show on github.
   */
  RunnerReportLoggerCreator?: (
    output: string[]
  ) => Logger;
  /**
   * A function that implements the logics after code-gen. Initialized for Terraform.
   */
  runLangAfterScripts?: (
    sdkRepo: SDKRepository,
    sdkPackage: SDKRepositoryPackage
  ) => Promise<boolean>;
  /**
   * The name of a file that is found at the root of a generated package for this language.
   */
  packageRootFileName?: string | RegExp;
  /**
   * Commands that should be run in the context of a package that was changed as a result of running
   * AutoRest. These will be run before files are added and committed to the generation branch.
   */
  afterGenerationCommands?: PackageCommand | PackageCommand[];
  /**
   * The command that should be run to create a package for this language.
   */
  packageCommands?: PackageCommand | PackageCommand[];
  /**
   * The text of the comment that explains how to install/reference an SDK build drop from a
   * customer's code.
   */
  installationInstructions?: InstallationInstructions;
  /**
   * Lite installation instruction for this Lang.
   */
  liteInstallationInstruction?: InstallationInstructions;
  /**
   * Flag used to indicate whether the package should be published as public release.
   */
  isPrivatePackage?: boolean;
  /**
   * This variable will be deleted when new SDK breaking change review flow is being used.
   * We will use 'breakingChangesLabel' instead.
   */
  breakingChangeLabel?: GithubLabel;
  /**
   * Keep release PR open after being created.
   */
  keepReleasePROpen?: boolean;

  /**
   * Breaking change for this Lang.
   */
  breakingChangesLabel?: GithubLabel;

  generateBreakingChangeReport?: GenerateBreakingChangeReportOptions;

  getExtraRelativeFolderPaths?: (relativePath: string, fileChanged: string[], logger: Logger) => Promise<string[]>;
}
