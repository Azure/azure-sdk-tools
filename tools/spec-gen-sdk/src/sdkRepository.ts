import { Logger, prefix, wrapLogger } from '@azure/logger-js';
import { sync as rimrafSync } from 'rimraf';
import {
  any,
  autorest,
  AutoRestOptions,
  AutoRestOptionValue,
  BlobStorageAppendBlob,
  BlobStoragePrefix,
  Command,
  contains,
  createFolder,
  ExecutableGit,
  first,
  folderExists,
  getParentFolderPath,
  getRepository,
  Git,
  GitHub,
  GitHubGetPullRequestsOptions,
  GitHubPullRequest,
  joinPath,
  map,
  parseCommands,
  pathRelativeTo,
  replaceAll,
  Repository,
  run,
  StringMap,
  where,
  commandToString,
  findEntryInPath
} from '@ts-common/azure-js-dev-tools';
import { ReadmeMdFileProcessMod, LanguageConfiguration } from './langSpecs/languageConfiguration';
import { createPackageCommandOptions, runPackageCommands } from './langSpecs/packageCommand';
import { createRepositoryCommandOptions, runRepositoryCommands } from './langSpecs/repositoryCommand';
import {
  createLogsBlob,
  getSplitLineCompositeLogger,
  trimNewLine,
  errorToLog
} from './sdkAutomation';
import { SDKAutomationState } from './sdkAutomationState';
import { SDKRepositoryPackage, SDKRepositoryPackageData } from './langSpecs/sdkRepositoryPackage';
import {
  getCreateSdkPullRequests,
  getSDKGenerationPullRequestBase,
  SwaggerToSDKConfiguration,
  getConfigMeta,
  getConfigAdvancedOption
} from './swaggerToSDKConfiguration';
import { updatePullRequestLabels } from './utils/githubUtils';
import { getPullRequestRepository, SpecificationPullRequestData } from './specificationPullRequest';
import { PackageCommandOptions } from './langSpecs/packageCommandOptions';
import { RepositoryCommandOptions } from './langSpecs/repositoryCommandOptions';
import { getRunnerReportLogger } from './runnerReportLogger';
import { resolveChangedTags } from './utils/resolveChangedTags';
import { existsSync } from 'fs';
import { SpecificationPullRequestGenerationContext } from './specificationPullRequestGeneration';

const REMOTE_NAME_GEN = 'generation';
const REMOTE_NAME_INT = 'integration';
const REMOTE_NAME_MAIN = 'main';
const REMOTE_NAME_SECONDARY = 'secondary';
const BRANCH_GENERATION = 'sdkAutomation/generated';

export interface SDKRepositoryContext extends SpecificationPullRequestGenerationContext {

  /**
   * Details about the specification pull request.
   */
  specificationPullRequest?: SpecificationPullRequestData;

  isPipelineTriggered: boolean;

  useMergedRoutine: boolean;

  /**
   * Update the generation data blobs with the current state of the generation.
   */
  writeGenerationData(): Promise<unknown>;
}

/**
 * The data that describes an SDK repository.
 */
export interface SDKRepositoryData {
  /**
   * The repository where the SDK's generation pull request and branch will be created. This is the
   * first repository where the automatically generated SDK will appear.
   */
  generationRepository: string;
  /**
   * The URL of the SDK's generation repository.
   */
  generationRepositoryUrl: string;
  /**
   * The repository where the SDK's integration/staging pull request and branch will be created. The
   * SDK's integration branch and pull request are where merged SDK generation pull requests will be
   * staged before they are merged and published in the main repository.
   */
  integrationRepository: string;
  /**
   * The URL of the SDK's integration repository.
   */
  integrationRepositoryUrl: string;
  /**
   * Used for updating integration branch since some tooling scripts should be excluded.
   */
  readonly secondaryRepository: string;
  readonly secondaryRepositoryUrl: string;
  readonly secondaryBranch: string;
  /**
   * The main repository for the SDK. This is where the SDK packages are published from.
   */
  readonly mainRepository: string;
  /**
   * The URL of the SDK's main repository.
   */
  readonly mainRepositoryUrl: string;
  /**
   * The prefix that will be applied to integration branches.
   */
  readonly integrationBranchPrefix: string;
  /**
   * The name of the branch in the main repository that integration branches will be based on
   * (integration pull requests will merge into). Defaults to "master"."
   */
  readonly mainBranch: string;
  /**
   * The name of the programming language that was assigned to this SDK repository.
   */
  readonly languageName: string;
  /**
   * The OpenAPI readme.md AutoRest configuration file URLs to generate within the context of this
   * SDK repository.
   */
  readonly readmeMdFileUrlsToGenerate: string[];
  /**
   * The generation status for this SDK repository.
   */
  status: SDKAutomationState;
  /**
   * Generation message to be shown.
   */
  messages: string[];
  /**
   * If sdk breaking change is detected.
   */
  hasBreakingChange?: boolean;
  /**
   * The URL of the blob where this SDK repository's generation logs will be written to.
   */
  logsBlobUrl?: string;
  /**
   * The URL of the blob where this SDK repository's diff will be written to.
   */
  diffBlobUrl?: string;
  /**
   * The changed packages in this SDK repository.
   */
  changedPackages?: SDKRepositoryPackageData[];

  artifactsZipUrl?: string;
}

/**
 * An SDK repository that will have AutoRest run in it.
 */
export class SDKRepository {
  /**
   * The packages in this SDK repository that were changed after AutoRest was run.
   */
  public readonly changedPackages: SDKRepositoryPackage[];

  public readonly repoPath: string;

  public readonly git: ExecutableGit;

  /**
   * Enable pipeline's Build.BuildID
   */
  public readonly buildID: string;
  public currentReadmeIndex: number;

  /**
   * A mapping between a readme.md file's URL to the scripts that should be run after AutoRest is
   * run on the readme.md file's URL.
   */
  private readonly readmeMdAfterScripts: StringMap<string[]>;

  private readonly remoteMainBranchName: string;

  private readonly localMainBranchName: string;

  private readonly enableCreatePullRequests: boolean;

  /**
   * Used for integration branch since some tooling scripts should be excluded.
   */
  private readonly remoteSecondaryRepositoryUrl: string;
  private readonly remoteSecondaryBranchName: string;

  /**
   * Create a new SDKRepository object.
   * @param logsBlob The blob that this SDK repository's logs will be written to.
   * @param logger The logger that will write logs related to this SDK repository.
   * @param language The LanguageConfiguration that will be used for this SDKRepository.
   * @param swaggerToSDKConfiguration The swagger_to_sdk_config.json file for this repository.
   * @param specPRIterationPrefix The BlobStoragePrefix where this SDK repository's blobs will be written to.
   * @param sdkRepositoryPrefix The BlobStoragePrefix where this SDK repository's blobs will be written to.
   * @param context The context object that will be used to invoke operations outside of this
   * object's scope.
   * @param data The data that describes the SDKRepository.
   * @param buildID The automation pipeline's BuildID.
   */
  constructor(
    public readonly logsBlob: BlobStorageAppendBlob,
    public readonly logger: Logger,
    public readonly language: LanguageConfiguration,
    public readonly swaggerToSDKConfiguration: SwaggerToSDKConfiguration,
    private readonly sdkRepositoryPrefix: BlobStoragePrefix,
    public readonly context: SDKRepositoryContext,
    public readonly data: SDKRepositoryData,
    buildID: string
  ) {
    this.readmeMdAfterScripts = {};
    this.changedPackages = [];
    this.enableCreatePullRequests =
      getCreateSdkPullRequests(this.swaggerToSDKConfiguration) && this.context.createGenerationPullRequests;
    this.remoteMainBranchName = this.data.mainBranch;
    this.localMainBranchName = this.remoteMainBranchName;
    this.remoteSecondaryBranchName = this.data.secondaryBranch;
    this.remoteSecondaryRepositoryUrl = this.data.secondaryRepositoryUrl;
    this.repoPath = getRepositoryFolderPath(
      this.context.generationWorkingFolderPath,
      this.swaggerToSDKConfiguration,
      this.data.mainRepository
    );
    this.git = this.context.git.scope({
      executionFolderPath: this.repoPath,
      runner: this.context.runner,
      throwOnError: true,
      log: (text: string) => this.logger.logInfo(trimNewLine(text)),
      showCommand: true
    });
    this.buildID = buildID;
  }

  public addReadmdMdAfterScripts(readmeMdFileUrl: string, afterScripts: string[]): void {
    this.readmeMdAfterScripts[readmeMdFileUrl] = afterScripts;
  }

  /**
   * Generate the modified SDKs in this repository, upload any generated artifacts, and create any
   * necessary pull requests in the SDK repository.
   */
  public async generate(): Promise<void> {
    await this.logsBlob.create({ contentType: 'text/plain' });
    this.data.logsBlobUrl = this.context.getBlobProxyUrl(this.logsBlob);
    this.data.status = 'inProgress';
    await this.context.writeGenerationData();
    if (this.context.useMergedRoutine) {
      await this.closeGenerationPullRequests(true);
    }

    try {
      await createFolder(this.repoPath);
      await this.rewriteRepositoryToFallback();
      await this.cloneSDKRepository();

      const languageReadmeMdFileProcessModMod = this.language.readmeMdFileProcessMod ?? ReadmeMdFileProcessMod.Batch;

      for (this.currentReadmeIndex = 0; this.currentReadmeIndex
        < this.data.readmeMdFileUrlsToGenerate.length; this.currentReadmeIndex ++ ) {
        if (this.currentReadmeIndex > 0
          && languageReadmeMdFileProcessModMod.valueOf() !== ReadmeMdFileProcessMod.Batch) {
          await this.resetSDKRepository();
          this.changedPackages.splice(0);
        }
        const readmeMdFileUrl = this.data.readmeMdFileUrlsToGenerate[this.currentReadmeIndex];
        await this.runGeneration(readmeMdFileUrl);
        if (languageReadmeMdFileProcessModMod.valueOf() === ReadmeMdFileProcessMod.Batch
          && this.currentReadmeIndex < this.data.readmeMdFileUrlsToGenerate.length - 1) {
          continue;
        }
        await this.runAfterScriptsInRepo();

        const changedPackages = await this.findChangedPackages();
        this.addChangedPackages(changedPackages);

        await this.forEachChangedPackage(
          (changedPackage) => runLangAfterScript(this.language, this, changedPackage),
          changedPackages);

        await this.context.writeGenerationData();

        if (languageReadmeMdFileProcessModMod.valueOf() === ReadmeMdFileProcessMod.Batch) {
          await this.logger.logInfo('readmeMdFileProcessMod: In Batch Mode.');
          for (const readmeMdFileUrlRunScript of this.data.readmeMdFileUrlsToGenerate) {
            await this.forEachChangedPackage(
              (changedPackage) => this.runAfterScripts(changedPackage, readmeMdFileUrlRunScript),
              changedPackages
            );
          }
        } else {
          await this.logger.logInfo('readmeMdFileProcessMod: In Sequencial Mode.');
          await this.forEachChangedPackage(
            (changedPackage) => this.runAfterScripts(changedPackage, readmeMdFileUrl),
            changedPackages
          );
        }

        await this.git.addAll();
        await this.git.run(['commit', '--allow-empty', '-m', 'SDK Automation Generation']);

        await this.forEachChangedPackage(
          (changedPackage) => this.createPackageAndInstallationInstruction(changedPackage),
          changedPackages
        );

        // Token may expire after autorest generation so we refresh token here
        await this.git.refreshRemoteAuthentication();

        await this.forEachChangedPackage(
          (changedPackage) => this.updateBranchForPullRequest(changedPackage),
          changedPackages
        );

        await this.forEachChangedPackage(async (changedPackage) => {
          const packageContentChanged = await this.updateTargetBranch(changedPackage);
          if (packageContentChanged) {
            await this.updateTargetPR(changedPackage);
            await this.generateBreakingChangeLog(changedPackage);
          } else {
            await changedPackage.logger.logWarning('No file is changed.');
          }
          if (changedPackage.data.status === 'inProgress') {
            changedPackage.data.status = packageContentChanged ? 'succeeded' : 'warning';
          }
        }, changedPackages);

        // await this.createArtifactsZip();
        await this.context.writeGenerationData();
      }
    } catch (error) {
      await this.logError(`SDK Repository Error: ${JSON.stringify(error, undefined, 2)}`);
      await this.logError(error.stack);

      throw error;
    } finally {
      await this.deleteClonedSDKRepository();
      await this.updateStatusAfterGeneration();
    }
  }

  /**
   * Close the generation pull requests that are associated with the specification pull request.
   * @param parallel Whether or not to close the generation pull requests in parallel.
   */
  public async closeGenerationPullRequests(parallel: boolean): Promise<void> {
    const disableGenerationPRAutomation = getConfigAdvancedOption(
      this.swaggerToSDKConfiguration,
      'disable_generation_pr_automation'
    );
    const github: GitHub = this.context.github;
    const generationRepository: string = this.data.generationRepository;

    return this.forEachGenerationPullRequest(parallel, async (generationPullRequest: GitHubPullRequest) => {
      const generationPullRequestRepository: Repository = getPullRequestRepository(generationPullRequest);

      if (disableGenerationPRAutomation) {
        await this.logger.logInfo(
          `Skip to close generation pull request at ${generationPullRequest.html_url} as it's disabled in config.`
        );
      } else {
        try {
          await this.logger.logInfo(`Closing pull request ${generationPullRequest.html_url}...`);
          await github.closePullRequest(generationPullRequestRepository, generationPullRequest.number);
          await this.logger.logInfo(
            `Deleting branch "${generationPullRequest.head.ref}" in ${generationRepository}...`
          );
          await github.deleteBranch(generationRepository, generationPullRequest.head.ref);
        } catch (error) {
          await this.logger.logError(
            `Failed to close the generation pull request at ` +
              `${generationPullRequest.html_url}: ${error}, ${JSON.stringify(error)}`
          );
        }
      }

      if (!this.context.isPipelineTriggered) {
        await updatePullRequestLabels(
          github,
          generationPullRequestRepository,
          generationPullRequest,
          ['GenerationPR', 'SpecPRClosed'],
          this.logger
        );
      }
    });
  }

  private async updateStatusAfterGeneration(): Promise<void> {
    if (this.data.status === 'inProgress') {
      if (!any(this.data.changedPackages)) {
        this.data.status = 'warning';
      } else {
        const packageStates: SDKAutomationState[] = map(
          this.data.changedPackages,
          (changedPackage: SDKRepositoryPackageData) => changedPackage.status
        );
        this.data.status =
          first(packageStates, (packageState: SDKAutomationState) => packageState === 'failed') ||
          first(packageStates, (packageState: SDKAutomationState) => packageState === 'warning') ||
          'succeeded';
      }
    }
    await this.context.writeGenerationData();
    if (this.data.status === 'failed') {
      throw new Error('ResultFailure: The result is marked as failure due to at least one required step fails. Please refer to the detail log in pipeline run or local console for more information.');
    }
  }

  /**
   * Log an error message and set this SDK repository's status to "failed".
   * @param errorMessage The error message to log.
   */
  private logError(errorMessage: string): Promise<unknown> {
    this.data.status = 'failed';
    return Promise.all([this.logger.logError(errorMessage), this.context.writeGenerationData()]);
  }

  private async checkRepositoryBranchesStatus(repositoryUrl: string): Promise<boolean> {
    const regexp: RegExp = /https:\/\/github.com\//;
    repositoryUrl = repositoryUrl.replace(regexp, '');
    const branches = await this.context.github.getAllBranches(repositoryUrl);
    let response: boolean;
    if (branches && Array.isArray(branches) && branches.length > 0) {
      response = true;
    } else {
      response = false;
    }
    return response;
  }

  private async rewriteRepositoryToFallback(): Promise<void> {
    const [
      generationRepositoryExist,
      integrationRepositoryExist,
      mainRepositoryExist,
      secondaryRepositoryExist
    ] = await Promise.all([
      this.checkRepositoryBranchesStatus(this.data.generationRepositoryUrl),
      this.checkRepositoryBranchesStatus(this.data.integrationRepositoryUrl),
      this.checkRepositoryBranchesStatus(this.data.mainRepositoryUrl),
      this.checkRepositoryBranchesStatus(this.data.secondaryRepositoryUrl)
    ]);

    if (!mainRepositoryExist) {
      throw new Error(`ConfigError: Main repository ${this.data.mainRepositoryUrl} doesn't exist. Please refer to the https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/sdkautomation/SpecConfigSchema.json schema to fix the 'mainRepository' in 'specificationRepositoryConfiguration.json' under the root folder of the 'azure-rest-api-specs(-pr) repository.`);
    }

    if (!secondaryRepositoryExist) {
      throw new Error(`ConfigError: Secondary repository ${this.data.secondaryRepositoryUrl} doesn't exist. Please refer to the https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/sdkautomation/SpecConfigSchema.json schema to fix the 'secondaryRepository' in 'specificationRepositoryConfiguration.json' under the root folder of the 'azure-rest-api-specs(-pr) repository.`);
    }

    if (!integrationRepositoryExist) {
      await this.logError(
        `Integration repository ${this.data.integrationRepositoryUrl} doesn't exist. ` +
          `Using fallback ${this.data.mainRepositoryUrl}`
      );
      this.data.integrationRepository = this.data.mainRepository;
      this.data.integrationRepositoryUrl = this.data.mainRepositoryUrl;
    }

    if (!generationRepositoryExist) {
      await this.logError(
        `Generation repository ${this.data.generationRepositoryUrl} doesn't exist. ` +
          `Using fallback ${this.data.integrationRepositoryUrl}`
      );
      this.data.generationRepository = this.data.integrationRepository;
      this.data.generationRepositoryUrl = this.data.integrationRepositoryUrl;
    }
  }

  private async deleteClonedSDKRepository(): Promise<void> {
    const repositoryFolderPath = this.repoPath;

    if (!this.context.deleteClonedRepositories) {
      await this.logger.logInfo(
        `Not deleting clone of ${this.data.generationRepository} at folder ${repositoryFolderPath}.`
      );
      return;
    }

    await this.logger.logSection(
      `Deleting clone of ${this.data.generationRepository} at folder ${repositoryFolderPath}...`
    );
    if (!(await folderExists(repositoryFolderPath))) {
      await this.logger.logWarning(`repository folder (${repositoryFolderPath}) doesn't exist.`);
      return;
    }

    try {
      rimrafSync(repositoryFolderPath);
    } catch {
      await this.logger.logWarning(`Failed to delete repository folder (${repositoryFolderPath}).`);
      return;
    }

    await this.logger.logInfo(
      `Finished deleting clone of ${this.data.generationRepository} at folder ${repositoryFolderPath}.`
    );
  }

  /**
   * Clone this SDK repository to the provided folder path.
   */
  private async cloneSDKRepository(): Promise<boolean> {
    await this.logger.logSection(`Cloning SDK repo...`);

    await this.git.resetRepoFolder();

    await this.git.addRemote(REMOTE_NAME_GEN, this.data.generationRepositoryUrl);
    await this.git.addRemote(REMOTE_NAME_INT, this.data.integrationRepositoryUrl);
    await this.git.addRemote(REMOTE_NAME_MAIN, this.data.mainRepositoryUrl);
    await this.git.addRemote(REMOTE_NAME_SECONDARY, this.data.secondaryRepositoryUrl);
    await this.git.fetch({ remoteName: REMOTE_NAME_MAIN });
    await this.git.fetch({ remoteName: REMOTE_NAME_SECONDARY, refSpec: this.data.secondaryBranch });

    await this.git.checkout(`${REMOTE_NAME_MAIN}/${this.data.mainBranch}`, {
      localBranchName: this.localMainBranchName
    });
    await this.git.checkout(this.localMainBranchName, {
      localBranchName: BRANCH_GENERATION
    });

    return true;
  }

  private async resetSDKRepository(): Promise<void> {
    await this.git.checkout(BRANCH_GENERATION);
    await this.git.resetAll({
      hard: true,
      target: this.localMainBranchName
    });
    await this.git.run(['clean', '-xdf']);
  }

  /**
   * Get the generation pull requests in this SDK repository that were created as a result of the
   * provided specification pull request.
   * @param specificationPullRequestNumber The specification pull request's number.
   */
  private async getGenerationPullRequests(
    specificationPullRequestNumber: number,
    options?: GitHubGetPullRequestsOptions
  ): Promise<GitHubPullRequest[]> {
    const allPullRequests: GitHubPullRequest[] = [];
    allPullRequests.push(...(await this.context.github.getPullRequests(this.data.integrationRepository, options)));
    if (this.data.integrationRepository !== this.data.mainRepository) {
      allPullRequests.push(...(await this.context.github.getPullRequests(this.data.mainRepository, options)));
    }
    const suffix = `@${specificationPullRequestNumber}`;
    return where(allPullRequests, (pr: GitHubPullRequest) => pr.head.label.endsWith(suffix));
  }

  private async forEachChangedPackage(
    action: (changedPackage: SDKRepositoryPackage) => unknown,
    packages: SDKRepositoryPackage[],
    predicate: (changedPackage: SDKRepositoryPackage) => boolean = (_) => true
  ): Promise<void> {
    const filteredPackage = where(packages, predicate);
    for (const changedPackage of filteredPackage) {
      try {
        await Promise.resolve(action(changedPackage));
      } catch (error) {
        await changedPackage.logError(`SDK Repository Package Error: ${error}, ${JSON.stringify(error)}`);
      }
    }
  }

  private async updateBranchForPullRequest(changedPackage: SDKRepositoryPackage): Promise<void> {
    const {
      logger,
      data: { useIntegrationBranch, integrationBranch }
    } = changedPackage;

    if (!useIntegrationBranch) {
      return;
    }

    const checkOutIntegrationBranchFromSecondary = async () =>
      this.git.checkout(`${REMOTE_NAME_SECONDARY}/${this.remoteSecondaryBranchName}`, {
        localBranchName: integrationBranch
      });
    await checkOutIntegrationBranchFromSecondary();

    if (!this.context.useMergedRoutine) {
      await logger.logInfo(
        `Update main branch ` +
          `with secondary branch: ${this.remoteSecondaryRepositoryUrl}/${this.remoteSecondaryBranchName}`
      );
      const secondaryRepositoryUrl = await this.git.getRemoteUrl(REMOTE_NAME_SECONDARY);
      const integrationRepositoryUrl = await this.git.getRemoteUrl(REMOTE_NAME_INT);
      if (secondaryRepositoryUrl !== integrationRepositoryUrl) {
        try {
          await this.git.push({ setUpstream: REMOTE_NAME_INT, branchName: this.localMainBranchName });
        } catch {
          await logger.logError(
            `Fail to push branch ${integrationBranch} to ${REMOTE_NAME_INT}/${this.localMainBranchName}`
          );
        }
      }
    }
    return;
  }

  private async updateTargetBranch(changedPackage: SDKRepositoryPackage): Promise<boolean> {
    const {
      logger,
      data: { integrationBranch }
    } = changedPackage;
    const { targetName, targetRepo, targetBranch, baseBranch } = this.getGenBranchInfo(changedPackage);

    if (targetBranch !== integrationBranch) {
      await this.git.checkout(baseBranch);
      // Create Branch
      await logger.logSection(`Creating SDK branch "${targetBranch}" in ${targetRepo} ...`);
      // We generate output starting from main branch so checkout main branch here
      await this.git.createLocalBranch(targetBranch);
    } else {
      await this.git.checkout(integrationBranch);
    }

    await this.updatePackageFolderToIndexFromBranch(changedPackage, BRANCH_GENERATION);
    // List files to add inside the package
    const packageDiff: ExecutableGit.DiffResult = await this.git.diff({
      commit1: baseBranch,
      usePager: false,
      ignoreSpace: 'all',
      staged: true,
      nameOnly: true
    });
    await logger.logInfo(`${packageDiff.filesChanged.length} files staged for commit:`);
    for (const filePath of packageDiff.filesChanged) {
      await logger.logInfo(pathRelativeTo(filePath, this.repoPath));
    }

    if (!any(packageDiff.filesChanged)) {
      await logger.logInfo(
        `No differences were detected between ` +
          `the generation branch and its parent branch after the after_scripts were run.`
      );
      changedPackage.data.status = 'warning';
    } else {
      // Get commit message and commit
      const specPullRequest = this.context.specificationPullRequest!;
      const sdkCommitMessages: string[] = [`Generated from ${specPullRequest.headCommit}`];

      if (this.context.useMergedRoutine) {
        try {
          const commitResponse = await this.context.github
            .getCommit(specPullRequest.headRepository, specPullRequest.headCommit);
          if (commitResponse !== undefined) {
            sdkCommitMessages.push(commitResponse!.commit.message);
          }
        } catch (error) {
          await logger.logWarning(
            `Unable to get details about the head commit. ` +
              `This is probably because the head commit is in a fork that we don't have permission to read from. ` +
              `${error} ${JSON.stringify(error)}`
          );
        }
      }

      await this.git.commit(sdkCommitMessages);
    }

    await logger.logInfo(`Pushing branch "${targetBranch}" to "${targetRepo}"...`);
    await this.git.push({ setUpstream: targetName, branchName: targetBranch, force: true });

    return any(packageDiff.filesChanged);
  }

  private async updatePackageFolderToIndexFromBranch(
    changedPackage: SDKRepositoryPackage,
    refSpec: string
  ): Promise<void> {
    const checkoutFolder = async (folderPath: string) => {
      try {
        rimrafSync(joinPath(changedPackage.repositoryFolderPath, folderPath));
      } catch {
        await this.logger.logError(
          `Fail to delete folder: ${joinPath(changedPackage.repositoryFolderPath, folderPath)}`
        );
      }
      const result = await this.git.run(['checkout', refSpec, '--', folderPath], {
        throwOnError: false
      });
      if (result.exitCode) {
        changedPackage.data.status = 'warning';
        await changedPackage.logger.logWarning(
          `Failed to checkout ${folderPath} from ${refSpec} . Please check ${refSpec} for detail.`
        );
      } else {
        await this.git.add(folderPath);
      }
    };

    await checkoutFolder(changedPackage.data.relativeFolderPath);

    for (const folderPath of changedPackage.data.extraRelativeFolderPaths) {
      await checkoutFolder(folderPath);
    }
  }

  private async createPackageAndInstallationInstruction(changedPackage: SDKRepositoryPackage): Promise<void> {
    if (!this.language.packageCommands || this.language.packageCommands.length === 0) {
      await changedPackage.logger.logWarning(`${this.language.name} has no registered package commands.`);
    } else {
      const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(this, changedPackage);
      if (
        await runPackageCommands(
          changedPackage,
          this.language.packageCommands,
          packageCommandOptions,
          this.language.isPrivatePackage
        )
      ) {
        await changedPackage.createAndUploadInstallationInstructions();
      }
    }
  }

  private async generateBreakingChangeLog(changedPackage: SDKRepositoryPackage): Promise<void> {
    if (typeof this.language.generateBreakingChangeReport !== 'function') {
      return;
    }

    let genSuccess = false;
    try {
      genSuccess = await this.language.generateBreakingChangeReport({
        changedPackage,
        showCommand: true,
        captureOutput: (text: string) => changedPackage.logger.logInfo(trimNewLine(text)),
        captureError: (text: string) => changedPackage.logger.logError(trimNewLine(text)),
        captureChangeLog: (text: string, containsBreakingChange?: boolean) => {
          if (containsBreakingChange) {
            this.data.hasBreakingChange = true;
            changedPackage.data.hasBreakingChange = true;
            changedPackage.data.status = 'warning';
          }
          changedPackage.data.messages.push(`${text}`);
          return changedPackage.logger.logInfo(trimNewLine(text));
        },
        log: (text: string) => changedPackage.logger.logInfo(trimNewLine(text))
      });
    } catch (e) {
      await changedPackage.logError(`${e.stack} ${e.message}`);
    }

    if (!genSuccess) {
      await changedPackage.logError(`[ChangeLog] WARNING: Failed to generate ChangeLog.`);
      changedPackage.data.status = 'warning';
    }
  }

  private getGenBranchInfo(
    changedPackage: SDKRepositoryPackage
  ): {
    baseName: string;
    baseRepo: string;
    baseBranch: string;
    targetName: string;
    targetRepo: string;
    targetBranch: string;
  } {
    const useMergedRoutine = this.context.useMergedRoutine;

    let baseName: string = REMOTE_NAME_MAIN;
    let baseRepo: string = changedPackage.data.mainRepository;
    let baseBranch: string = this.remoteMainBranchName;
    let targetName: string = REMOTE_NAME_GEN;
    let targetRepo: string = changedPackage.data.generationRepository;
    let targetBranch: string = changedPackage.data.generationBranch;

    if (changedPackage.data.useIntegrationBranch) {
      if (useMergedRoutine) {
        targetName = REMOTE_NAME_INT;
        targetRepo = changedPackage.data.integrationRepository;
        targetBranch = changedPackage.data.integrationBranch;
      } else {
        baseName = REMOTE_NAME_INT;
        baseRepo = changedPackage.data.integrationRepository;
        baseBranch = this.localMainBranchName;
      }
    }

    return { baseName, baseRepo, baseBranch, targetName, targetRepo, targetBranch };
  }

  private async updateTargetPR(changedPackage: SDKRepositoryPackage): Promise<void> {
    const { logger } = changedPackage;

    if (!this.enableCreatePullRequests) {
      await logger.logWarning(`Creating pull request is disabled.`);
      return;
    }

    const useMergedRoutine = this.context.useMergedRoutine;
    const { baseRepo, baseBranch, targetBranch, targetRepo } = this.getGenBranchInfo(changedPackage);
    const headLabel = `${getRepository(targetRepo).owner}:${targetBranch}`;

    await logger.logInfo(`Checking if pull request exists...`);
    const github = this.context.github;
    const openPullRequests =
      await github.getPullRequests(baseRepo, {
          head: headLabel,
          base: baseBranch,
          state: 'open',
          sort: 'updated'
        // tslint:disable-next-line: no-any
        } as any);
    let pullRequest: GitHubPullRequest | undefined = first(
      openPullRequests,
      (openPullRequest: GitHubPullRequest) => openPullRequest.state === 'open'
    );

    const specPR = this.context.specificationPullRequest!;
    let body = useMergedRoutine
      ? `Created to release ${changedPackage.data.name}.\
<b>Reopen</b> this PR to release the SDK.
If you can't reopen it, click \
<a href="https://github.com/${baseRepo}/compare/${baseBranch}...${headLabel}?expand=1">here</a>\
 to create a new one.`
      : `Created to sync ${specPR.htmlUrl}.`;
    if (changedPackage.data.installationInstructions) {
      body += `\n${changedPackage.data.installationInstructions}`;
    }
    const title =
      `[${useMergedRoutine ? 'ReleasePR' : 'AutoPR'} ${this.language.packageNameAltPrefix || ''}${
        changedPackage.data.name
      }]` + ` ${specPR.title}`;

    if (pullRequest) {
      await logger.logInfo(`Pull request already exists at "${pullRequest.html_url}" Updating it.`);
      await github.updatePullRequest(baseRepo, pullRequest.number, { title, body });
    } else {
      await logger.logSection(
        `Creating SDK pull request in "${baseRepo}" from "${targetBranch}" to "${baseBranch}"...`
      );
      const maintainerCanModify: boolean = baseRepo === targetRepo;
      pullRequest = await github.createPullRequest(baseRepo, baseBranch, headLabel, {
        title,
        body,
        maintainerCanModify
      });
      await logger.logInfo(`Created pull request at ${pullRequest.html_url}.`);
    }

    if (useMergedRoutine && !this.language.keepReleasePROpen) {
      await logger.logInfo(`Closing ReleasePR after created`);
      await github.closePullRequest(baseRepo, pullRequest.number);
    }

    if (!this.context.isPipelineTriggered) {
      await updatePullRequestLabels(github, baseRepo, pullRequest, ['GenerationPR', 'SpecPRInProgress'], logger);
    }

    changedPackage.data.generationPullRequestUrl = pullRequest.html_url;
    changedPackage.data.generationPullRequestDiffUrl = pullRequest.html_url + '/files';
  }

  private addChangedPackages(changedPackages: SDKRepositoryPackage[]): void {
    for (const changedPackage of changedPackages) {
      if (
        !contains(
          this.changedPackages,
          (sdkPackage: SDKRepositoryPackage) => sdkPackage.data.name === changedPackage.data.name
        )
      ) {
        this.changedPackages.push(changedPackage);

        if (!this.data.changedPackages) {
          this.data.changedPackages = [];
        }
        this.data.changedPackages.push(changedPackage.data);
      }
    }
  }

  private async findChangedPackages(): Promise<SDKRepositoryPackage[]> {
    const diffResult = await this.getDiffResult();
    if (diffResult === undefined) {
      await this.logger.logError('Failed to find any diff after autorest so no changed packages was found.');
      return [];
    }

    const changedPackages: SDKRepositoryPackage[] = [];
    const changedPackageFolderPaths: string[] = await this.getChangedPackageFolderPaths(diffResult);
    let packageIndex: number = 1;
    for (const changedPackageFolderPath of changedPackageFolderPaths) {
      const relativePackageFolderPath: string = pathRelativeTo(changedPackageFolderPath, this.repoPath);
      const packageName: string = await getPackageName(this.language, this.repoPath, relativePackageFolderPath,
        this.data.readmeMdFileUrlsToGenerate[this.currentReadmeIndex]);
      const altPackageName: string = this.language.packageNameAltPrefix
        ? `${this.language.packageNameAltPrefix}${packageName}`
        : packageName;
      const packageDataMessages = [];

      const packagePrefix: BlobStoragePrefix = getPackagePrefix(this.sdkRepositoryPrefix, altPackageName);
      const packageLogsBlob: BlobStorageAppendBlob = await createLogsBlob(packagePrefix);
      // Add warning/error message to messages to show on github.
      const logger = getSplitLineCompositeLogger(
        prefix(
          wrapLogger(this.logger, {
            logError: this.logger.logInfo,
            logWarning: this.logger.logInfo
          }),
          `[${altPackageName}]`
        ),
        this.context.getBlobLogger(packageLogsBlob),
        this.language.RunnerReportLoggerCreator ?
        this.language.RunnerReportLoggerCreator(packageDataMessages) :
        getRunnerReportLogger(packageDataMessages)
      );
      await logger.logInfo(`Package name for "${changedPackageFolderPath}" is "${altPackageName}".`);

      const extraRelativeFolderPaths = this.language.getExtraRelativeFolderPaths
        ? await this.language.getExtraRelativeFolderPaths(relativePackageFolderPath, diffResult.filesChanged, logger)
        : [];
      const useIntegrationBranch =
        getSDKGenerationPullRequestBase(this.swaggerToSDKConfiguration) === 'integration_branch';
      const integrationBranch = `${this.data.integrationBranchPrefix}/${altPackageName.replace(/\//g, '_')}`;
      const generationBranch =
        `${integrationBranch}@` +
        `${this.context.specificationPullRequest ? this.context.specificationPullRequest.number : 'specPrNumber'}`;

      const packageData: SDKRepositoryPackageData = {
        name: packageName,
        messages: packageDataMessages,
        relativeFolderPath: relativePackageFolderPath,
        extraRelativeFolderPaths: extraRelativeFolderPaths,
        status: 'inProgress',
        logsBlobUrl: this.context.getBlobProxyUrl(packageLogsBlob),
        changedFilePaths: where(diffResult.filesChanged, (changedFilePath: string) =>
          changedFilePath.startsWith(changedPackageFolderPath)
        ),
        integrationBranch,
        integrationRepository: this.data.integrationRepository,
        useIntegrationBranch,
        generationBranch,
        generationRepository: this.data.generationRepository,
        generationRepositoryUrl: this.data.generationRepositoryUrl,
        mainRepository: this.data.mainRepository,
        isPrivatePackage: this.language.isPrivatePackage === undefined ? false : this.language.isPrivatePackage
      };

      changedPackages.push(
        new SDKRepositoryPackage(
          this.repoPath,
          this.language,
          packageLogsBlob,
          packagePrefix,
          logger,
          this.context,
          packageData,
          this.currentReadmeIndex * 100 + packageIndex
        )
      );
      packageIndex++;
    }
    if (changedPackages.length === 0) {
      await this.logger.logWarning('Failed to find any changed packages. Please check the changed file list.');
      await this.logger.logWarning(diffResult.filesChanged.join('\n'));
    }
    return changedPackages;
  }

  private async getChangedPackageFolderPaths(diffResult: Git.DiffResult): Promise<string[]> {
    const result: string[] = [];
    if (!this.language.packageRootFileName) {
      await this.logger.logInfo(
        `No packageRootFileName property has been specified in the language configuration for ${this.language.name}.`
      );
    } else {
      for (const modifiedFile of diffResult.filesChanged) {
        const packageRootFilePath: string | undefined = await findEntryInPath(
          this.language.packageRootFileName,
          modifiedFile,
          (path) => existsSync(path)
        );
        if (!packageRootFilePath) {
          await this.logger.logVerbose(`No package root file found for modified file ${modifiedFile}.`);
        } else {
          const packageFolderPath: string = getParentFolderPath(packageRootFilePath);
          if (packageFolderPath !== this.repoPath && !contains(result, packageFolderPath)) {
            result.push(packageFolderPath);
          }
        }
      }

      await this.logger.logInfo(`Found ${result.length} package folder${result.length === 1 ? '' : 's'} that changed:`);
      for (const changedPackageFolder of result) {
        await this.logger.logInfo(`  ${changedPackageFolder}`);
      }
    }

    return result;
  }

  private async getDiffResult(): Promise<Git.DiffResult | undefined> {
    await this.logger.logSection('Getting diff after AutoRest ran...');
    const diffBlob: BlobStorageAppendBlob = getDiffBlob(this.sdkRepositoryPrefix);
    await diffBlob.create({ contentType: 'text/plain' });
    const diffBlobLogger: Logger = await this.context.getBlobLogger(diffBlob);

    // We stage all files before diffing so that we can detect new/untracked files. If we didn't,
    // then new/untracked files wouldn't show up in the diff.
    await this.git.addAll();
    let firstLineLogged = false;
    const gitDiffResult: Git.DiffResult = await this.git.diff({
      usePager: false,
      commit1: `${REMOTE_NAME_MAIN}/${this.data.mainBranch}`,
      ignoreSpace: 'all',
      staged: true,
      showResult: true,
      log: async (text: string) => {
        if (!firstLineLogged) {
          await this.logger.logInfo(trimNewLine(text));
          firstLineLogged = true;
        }
        await diffBlobLogger.logInfo(trimNewLine(text));
      }
    });
    await this.git.resetAll();
    this.data.diffBlobUrl = this.context.getBlobProxyUrl(diffBlob);

    let result: Git.DiffResult | undefined;
    if (!any(gitDiffResult.filesChanged)) {
      await this.logger.logInfo(`No changes were detected after AutoRest ran.`);
    } else {
      result = gitDiffResult;
      await this.logger.logInfo(`The following files were changed:`);
      for (const file of gitDiffResult.filesChanged) {
        await this.logger.logInfo(`  ${file}`);
      }
    }

    return result;
  }

  private async runGeneration(readmeMdFileUrl: string): Promise<boolean> {
    const runGen = async (options: AutoRestOptions) => {
      await addAutoRestAndGeneratorVersions(options, this.language, this.logger);
      if (this.language.generationCommands) {
        const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(this, this.repoPath);
        return await runRepositoryCommands(this, this.language.generationCommands, repositoryCommandOptions);
      }

      try {
        await autorest(readmeMdFileUrl, options, {
          autorestPath: joinPath(process.cwd(), 'node_modules/.bin/autorest'),
          runner: this.context.runner,
          executionFolderPath: this.repoPath,
          showCommand: true,
          log: (text: string) => this.logger.logInfo(trimNewLine(text)),
          captureOutput: (text: string) => this.logger.logInfo(trimNewLine(text)),
          captureError: (text: string) => this.logger.logError(trimNewLine(text)),
          capturePrefix: 'AutoRest',
          throwOnError: true
        });
        return true;
      } catch (error) {
        await this.logError(`Failed to run autorest.`);
        await this.logError(errorToLog(error, false));
        return false;
      }
    };

    const autorestOptions = await getResolvedAutoRestOptions(
      this.repoPath,
      this.swaggerToSDKConfiguration,
      readmeMdFileUrl,
      'autorest_options'
    );

    const autorestOptionsForOtherSDK = await getResolvedAutoRestOptions(
      this.repoPath,
      this.swaggerToSDKConfiguration,
      readmeMdFileUrl,
      'autorest_options_for_otherSDK'
    );

    if (Object.keys(autorestOptionsForOtherSDK).length > 0 && !(autorestOptionsForOtherSDK instanceof Array)) {
      await runGen(autorestOptionsForOtherSDK);
      await this.logger.logInfo('Finish autorest_option_for_otherSDK');
    }

    if (!(autorestOptions instanceof Array)) {
      return runGen(autorestOptions);
    }

    let result = true;
    for (const autorestOption of autorestOptions) {
      result = result && (await runGen(autorestOption));
    }
    return result;
  }

  private async runAfterScriptsInRepo(): Promise<void> {
    const scripts = getConfigMeta(this.swaggerToSDKConfiguration, 'after_scripts_in_repo') || [];
    if (scripts.length === 0) {
      await this.logger.logInfo('No after_scripts_in_repo to run');
      return;
    }

    const commandOptions = createRepositoryCommandOptions(this, this.repoPath);
    const envConfig = getConfigMeta(this.swaggerToSDKConfiguration, 'envs') || {};
    const envs = resolveEnvironmentVariables(envConfig, this.repoPath);

    const commands: Command[] = [];
    scripts.forEach((script) => commands.push(...parseCommands(script)));
    for (const command of commands) {
      replaceCommandVariables(command, commandOptions, this.logger);
      try {
        await run(command, undefined, {
          ...commandOptions,
          runner: this.context.runner,
          executionFolderPath: this.repoPath,
          showCommand: true,
          showResult: true,
          log: this.logger.logInfo,
          environmentVariables: envs,
          capturePrefix: `after_scripts_in_repo|${command.executable}`,
          throwOnError: true
        });
      } catch (e) {
        await this.logger.logError(`Failed to run after_scripts_in_repo: ${commandToString(command)}`);
        this.data.status = 'failed';
        throw e;
      }
    }
  }

  private async runAfterScripts(changedPackage: SDKRepositoryPackage, readmeMdFileUrl: string): Promise<void> {
    const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(this, changedPackage);

    if (this.language.afterGenerationCommands && this.language.afterGenerationCommands.length > 0) {
      await runPackageCommands(changedPackage, this.language.afterGenerationCommands, packageCommandOptions);
    }

    const afterScripts = getConfigMeta(this.swaggerToSDKConfiguration, 'after_scripts') || [];
    const readmeMdAfterScripts: string[] | undefined = this.readmeMdAfterScripts[readmeMdFileUrl];
    if (readmeMdAfterScripts) {
      afterScripts.push(...readmeMdAfterScripts);
    }

    if (afterScripts.length === 0) {
      await changedPackage.logger.logInfo(`No after_scripts to run.`);
      return;
    }

    const envConfig = getConfigMeta(this.swaggerToSDKConfiguration, 'envs');
    const envs = resolveEnvironmentVariables(envConfig, changedPackage.repositoryFolderPath);
    await changedPackage.logger.logSection(`Running after_scripts...`);

    const afterScriptCommands: Command[] = [];
    for (const afterScript of afterScripts) {
      afterScriptCommands.push(...parseCommands(afterScript));
    }

    for (const afterScriptCommand of afterScriptCommands) {
      replaceCommandVariables(afterScriptCommand, packageCommandOptions, changedPackage.logger);
      try {
        await run(afterScriptCommand, undefined, {
          ...packageCommandOptions,
          runner: changedPackage.context.runner,
          executionFolderPath: changedPackage.repositoryFolderPath,
          showCommand: true,
          showResult: true,
          log: (text: string) => changedPackage.logger.logInfo(trimNewLine(text)),
          environmentVariables: envs,
          capturePrefix: `after_scripts|${afterScriptCommand.executable}`,
          throwOnError: true
        });
      } catch (e) {
        if (changedPackage.data.relativeFolderPath === 'schemas') {
          changedPackage.data.status = 'warning';
        } else {
          await changedPackage.logError(`Failed to run after_scripts: ${commandToString(afterScriptCommand)}`);
          changedPackage.data.status = 'failed';
        }
        return;
      }
    }
  }

  private async forEachGenerationPullRequest(
    parallel: boolean,
    action: (generationPullRequest: GitHubPullRequest) => Promise<void>
  ): Promise<void> {
    const specificationPullRequest = this.context.specificationPullRequest!;

    await this.logger.logInfo(
      `Getting generation pull requests for specification pull request ${specificationPullRequest.htmlUrl}...`
    );
    const matchingGenerationPullRequests: GitHubPullRequest[] = await this.getGenerationPullRequests(
      specificationPullRequest.number,
      { open: true }
    );
    if (!any(matchingGenerationPullRequests)) {
      await this.logger.logInfo(`No generation pull requests found.`);
    } else {
      if (parallel) {
        await Promise.all(
          matchingGenerationPullRequests.map((matchingGenerationPullRequest: GitHubPullRequest) => {
            return action(matchingGenerationPullRequest);
          })
        );
      } else {
        for (const matchingGenerationPullRequest of matchingGenerationPullRequests) {
          await action(matchingGenerationPullRequest);
        }
      }
    }
  }
}

/**
 * Convert each value in the StringMap using the conversion function.
 * @param map The map to convert.
 * @param conversion The conversion function that converts values of type T to values of type U.
 */
export function resolveEnvironmentVariables(
  envMap: StringMap<string | number | boolean> | undefined,
  repositoryFolderPath: string
): StringMap<string> {
  const envs: StringMap<string> = {};
  for (const [entryName, entryValue] of Object.entries(process.env)) {
    envs[entryName] = entryValue || '';
  }

  if (envMap) {
    for (let [entryName, entryValue] of Object.entries(envMap)) {
      if (entryName.toLowerCase().startsWith('sdkrel:')) {
        entryName = entryName.substring('sdkrel:'.length);
        entryValue = joinPath(repositoryFolderPath, entryValue as string);
      }
      envs[entryName] = entryValue.toString();
    }
  }

  return envs;
}

export async function getResolvedAutoRestOptions(
  repositoryFolderPath: string,
  swaggerToSDKConfiguration: SwaggerToSDKConfiguration,
  readmeMdFileUrl: string,
  optionName: 'autorest_options' | 'autorest_options_for_otherSDK'
): Promise<AutoRestOptions | AutoRestOptions[]> {
  const autorestOptions: AutoRestOptions = getConfigMeta(swaggerToSDKConfiguration, optionName) || {};
  let resolveChangedTagsOptionName: string | undefined = undefined;

  for (const autorestOptionName of Object.keys(autorestOptions)) {
    const autorestOptionValue: AutoRestOptionValue = autorestOptions[autorestOptionName];
    if (autorestOptionName.startsWith('sdkrel:') && typeof autorestOptionValue === 'string') {
      const resolvedAutorestOptionName: string = autorestOptionName.substring('sdkrel:'.length);
      const resolvedAutorestOptionValue: string = joinPath(repositoryFolderPath, autorestOptionValue);
      delete autorestOptions[autorestOptionName];
      autorestOptions[resolvedAutorestOptionName] = resolvedAutorestOptionValue;
    }

    if (autorestOptionName.startsWith('tags-changed-in-batch:')) {
      resolveChangedTagsOptionName = autorestOptionName.substring('tags-changed-in-batch:'.length);
      autorestOptions[resolveChangedTagsOptionName] = autorestOptions[autorestOptionName];
      delete autorestOptions[autorestOptionName];
    }
  }

  if (resolveChangedTagsOptionName === undefined) {
    return autorestOptions;
  }

  // Try to resolve the changed tags
  const changedTags = await resolveChangedTags();
  delete autorestOptions[resolveChangedTagsOptionName];
  return changedTags.map((tagName) => ({
    ...autorestOptions,
    tag: tagName
  }));
}

export async function addAutoRestAndGeneratorVersions(
  autorestOptions: AutoRestOptions,
  languageConfiguration: LanguageConfiguration,
  logger: Logger
): Promise<void> {
  if (typeof autorestOptions.use !== 'string' || !autorestOptions.use) {
    await logger.logInfo(
      `No generator was specified to use for this ${languageConfiguration.name} SDK repository. ` +
        `Defaulting to ${languageConfiguration.generatorPackageName}.`
    );
    autorestOptions.use = languageConfiguration.generatorPackageName;
  }
}

/**
 * Get the path that the repository should be cloned to.
 * @param workingFolderPath The path to the current generation instance's working folder.
 * @param swaggerToSDKConfig The SwaggerToSDK configuration from the repository.
 * @param repositoryNumber The repository's number in the ordered of repositories in the
 * generation's readme.md file.
 */
export function getRepositoryFolderPath(
  workingFolderPath: string,
  swaggerToSDKConfig: SwaggerToSDKConfiguration | undefined,
  repository: string
): string {
  const repositoryFolderRelativePath: string =
    getConfigAdvancedOption(swaggerToSDKConfig, 'clone_dir') || getRepository(repository).name;
  return joinPath(workingFolderPath, repositoryFolderRelativePath);
}

export function getDiffBlob(storagePrefix: BlobStoragePrefix): BlobStorageAppendBlob {
  return storagePrefix.getAppendBlob('diff.txt');
}

/**
 * Get the name of the package at the provided package folder path.
 * @param languageConfiguration The language configuration for the package.
 * @param rootedRepositoryFolderPath The rooted folder path to the cloned repository.
 * @param relativePackageFolderPath The relative folder path from the root of the repository to the
 * package folder.
 */
export function getPackageName(
  languageConfiguration: LanguageConfiguration,
  rootedRepositoryFolderPath: string,
  relativePackageFolderPath: string,
  readmeMdFileUrl: string
): Promise<string> {
  return Promise.resolve(
    languageConfiguration.packageNameCreator
      ? languageConfiguration.packageNameCreator(rootedRepositoryFolderPath, relativePackageFolderPath, readmeMdFileUrl)
      : relativePackageFolderPath
  );
}

export async function runLangAfterScript(
  languageConfiguration: LanguageConfiguration,
  sdkRepo: SDKRepository,
  changedPackage: SDKRepositoryPackage
): Promise<boolean> {
  if (languageConfiguration.runLangAfterScripts) {
    await changedPackage.logger.logInfo('Running runLangAfterScript');
    return languageConfiguration.runLangAfterScripts(sdkRepo, changedPackage);
  } else {
    await changedPackage.logger.logInfo('No run LangAfterScript');
  }
  return true;
}

export function getPropertyNameMatch(propertyName: string): string {
  return replaceAll(replaceAll(propertyName, '_', ''), '-', '')!.toLowerCase();
}

export function createCommandProperties(values: StringMap<unknown>): StringMap<string> {
  const result: StringMap<string> = {};
  for (const [propertyName, propertyValue] of Object.entries(values)) {
    if (typeof propertyValue === 'string' || typeof propertyValue === 'number' || typeof propertyValue === 'boolean') {
      const propertyNameMatch: string = getPropertyNameMatch(propertyName);
      result[propertyNameMatch] = propertyValue.toString();
    }
  }
  return result;
}

/**
 * Replace any variable references in the provided command.
 * @param command The command to replace variable references in.
 * @param options The properties to use to replace variable references in the command.
 */
export function replaceCommandVariables(command: Command, options: unknown, logger: Logger): void {
  const properties: StringMap<string> = createCommandProperties(options as StringMap<unknown>);

  command.executable = replaceStringVariables(command.executable, properties, logger);

  if (any(command.args)) {
    for (let i = 0; i < command.args.length; ++i) {
      command.args[i] = replaceStringVariables(command.args[i], properties, logger);
    }
  }
}

export function replaceStringVariables(value: string, properties: StringMap<string>, logger: Logger): string {
  const propertyReferenceRegex: RegExp = /\$\((.*?)\)/;

  let result = '';
  let remaining: string = value;
  // tslint:disable-next-line: no-constant-condition
  while (true) {
    const match: RegExpExecArray | null = propertyReferenceRegex.exec(remaining);
    if (!match) {
      result += remaining;
      break;
    } else {
      result += remaining.substring(0, match.index);

      const propertyReference: string = match[0];
      remaining = remaining.substring(match.index + propertyReference.length);

      const propertyReferenceName: string = match[1];
      const propertyNameMatch: string = getPropertyNameMatch(propertyReferenceName);
      let propertyValue: string | undefined = properties[propertyNameMatch];
      if (propertyValue === undefined) {
        for (const [matchPropertyName, matchPropertyValue] of Object.entries(properties)) {
          if (propertyNameMatch === getPropertyNameMatch(matchPropertyName)) {
            propertyValue = matchPropertyValue;
            break;
          }
        }
      }

      if (propertyValue === undefined) {
        result += propertyReference;
        // tslint:disable-next-line: no-floating-promises
        logger.logWarning(`Found no property replacement for "${propertyReferenceName}" in "${value}".`);
      } else {
        remaining = propertyValue + remaining;
      }
    }
  }

  return result;
}

function getPackagePrefix(storagePrefix: BlobStoragePrefix, packageName: string): BlobStoragePrefix {
  return storagePrefix.getPrefix(`${packageName}/`);
}
