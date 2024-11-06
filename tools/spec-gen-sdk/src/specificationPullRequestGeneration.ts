import { Logger, prefix } from '@azure/logger-js';
import {
  any,
  BlobStorageAppendBlob,
  BlobStorageBlob,
  BlobStorageBlockBlob,
  BlobStoragePrefix,
  Compressor,
  first,
  getRepositoryFullName,
  map,
  Repository,
  RepositoryConfiguration,
  Runner,
  getRepository,
  RealGitHub
} from '@ts-common/azure-js-dev-tools';
import { LanguageConfiguration } from './langSpecs/languageConfiguration';
import {
  createLogsBlob,
  getDataBlob,
  getGenerationWorkingFolderPath,
  getLogsBlob,
  getSplitLineCompositeLogger,
  logsBlobContentType,
  SDKAutomationContext
} from './sdkAutomation';
import { SDKRepository, SDKRepositoryContext, SDKRepositoryData } from './sdkRepository';
import { SDKRepositoryMapping } from './sdkRepositoryMapping';
import { SpecificationPullRequest } from './specificationPullRequest';
import { SpecificationReadmeMdFile } from './specificationReadmeMdFile';
import { getSDKRepositoryMapping, SpecificationRepositoryConfiguration } from './specificationRepositoryConfiguration';
import { SwaggerToSDKConfiguration } from './swaggerToSDKConfiguration';
import { getRunnerReportLogger } from './runnerReportLogger';

/**
 * The context that a SpecificationPullRequestGeneration needs to be able to run in.
 */
export interface SpecificationPullRequestGenerationContext extends SDKAutomationContext {
  generationWorkingFolderPath: string;

  /**
   * Get the proxy URL for the provided blob relative to the provided working prefix.
   * @param blob The blob to get the proxy URL for.
   * @returns The proxy URL for the provided blob relative to the provided working prefix.
   */
  getBlobProxyUrl(blob: BlobStorageBlob): string;
}

/**
 * The data that describes an OpenAPI specification pull request generation.
 */
export interface SpecificationPullRequestGenerationData {
  /**
   * The unique number that defines which version of the specification pull request this generation
   * is happening for. This will be an incremented integer. For example, the generation that occurs
   * when the pull request is created is 1. The generation that occurs when the next commit is added
   * to the pull request is 2.
   */
  readonly number: number | undefined;
  /**
   * The URL of the blob where this generation's logs will be written to.
   */
  readonly logsBlobUrl: string;
  /**
   * The URL of the blob where this generation's data will be written to.
   */
  readonly dataBlobUrl: string;
  /**
   * The URL of the blob where the generation's pull request comment body will be copied to.
   */
  readonly commentHtmlBlobUrl: string;
  /**
   * The SDK repositories that this generation will run AutoRest in.
   */
  readonly sdkRepositories: SDKRepositoryData[];
  /**
   * A message that should be displayed in the specification pull request comment.
   */
  message?: string;
}

/**
 * A generation that occurs for a specification pull request.
 */
export class SpecificationPullRequestGeneration {
  /**
   * The data that describes an OpenAPI specification pull request generation.
   */
  public data: SpecificationPullRequestGenerationData;
  /**
   * The logger that should be used to write logs related to this generation.
   */
  public readonly logger: Logger;
  /**
   * The blob where the generation's logs will be written to.
   */
  public readonly logsBlob: BlobStorageAppendBlob;
  /**
   * The blob where the generation's data will be written to.
   */
  public readonly dataBlob: BlobStorageBlockBlob;
  /**
   * The blob where the generation's pull request comment body will be copied to.
   */
  public readonly commentHtmlBlob: BlobStorageBlockBlob;
  /**
   * The SDK repositories that this generation will run AutoRest in.
   */
  public readonly sdkRepositories: SDKRepository[];
  /**
   * The SpecificationPullRequest object that this generation belongs to.
   */
  public specificationPullRequest?: SpecificationPullRequest;
  /**
   * The prefix that all of this generation's data will be written under.
   */
  private readonly prefix: BlobStoragePrefix;

  /**
   * Enable Build.BuildID in pipeline.
   */
  private readonly buildID: string;

  /**
   * Create a new SpecificationPullRequestGeneration from the provided details.
   * @param pullRequestPrefix The blob storage prefix for the pull request.
   * @param specificationPullRequestNumber The specification pull request's number.
   * @param generationNumber This generation's number.
   * @param buildID This pipeline build's id.
   */
  constructor(
    private pullRequestPrefix: BlobStoragePrefix,
    public readonly specificationPullRequestNumber: number,
    generationNumber: number | undefined,
    buildID: string,
    public readonly generationWorkingFolderPath: string,
    private readonly context: SpecificationPullRequestGenerationContext
  ) {
    this.prefix =
      generationNumber === undefined ? pullRequestPrefix : getGenerationPrefix(pullRequestPrefix, generationNumber);
    this.logsBlob = getLogsBlob(this.prefix);
    this.logger = getSplitLineCompositeLogger(context.logger, context.getBlobLogger(this.logsBlob));
    this.dataBlob = getDataBlob(this.prefix);
    this.commentHtmlBlob = getCommentHtmlBlob(this.prefix);
    this.sdkRepositories = [];
    this.data = {
      number: generationNumber,
      dataBlobUrl: context.getBlobProxyUrl(this.dataBlob),
      commentHtmlBlobUrl: context.getBlobProxyUrl(this.commentHtmlBlob),
      logsBlobUrl: context.getBlobProxyUrl(this.logsBlob),
      sdkRepositories: []
    };
    this.buildID = buildID;
  }

  /**
   * Generate new SDKs in each of the SDK repositories based on the changes from the specification
   * pull request.
   */
  public async generateSDKRepositories(): Promise<void> {
    if (!any(this.sdkRepositories)) {
      this.data.message =
        'No <b>readme.md</b> specification configuration files were found ' +
        'that are associated with the files modified in this pull request, ' +
        'or <b>swagger_to_sdk</b> section in readme.md is not configured';
      await this.writeGenerationData();
      return;
    }

    await this.forEachSDKRepository((sdkRepository: SDKRepository) => {
      return sdkRepository.generate();
    });
  }

  /**
   * Update the generation data blobs with the current state of the generation.
   */
  public writeGenerationData = async (generationData?: string, generationCommentHtml?: string): Promise<void> => {
    if (!generationData && !generationCommentHtml && this.specificationPullRequest) {
      await this.specificationPullRequest.writeGenerationData();
    } else {
      await this.dataBlob.setContentsFromString(generationData || '', { contentType: 'application/json' });
      await this.commentHtmlBlob.setContentsFromString(generationCommentHtml || '', { contentType: 'text/html' });
    }
  }

  /**
   * Get the runner object that will be used to execute external-process commands.
   */
  public getRunner(): Runner | undefined {
    return this.context.runner;
  }

  /**
   * Create a new Compressor object that can be use compress files and folders.
   */
  public createCompressor(): Compressor {
    return this.context.createCompressor();
  }

  /**
   * Get the SDKRepository object with the provided name. If one doesn't already exist, then one
   * will be created.
   * @param sdkRepositoryMapping The name of the SDKRepository.
   */
  public async getSDKRepository(
    sdkRepositoryMapping: SDKRepositoryMapping,
    sdkRepositoryName: string,
    supportedLanguages: LanguageConfiguration | LanguageConfiguration[]
  ): Promise<SDKRepository | undefined> {
    supportedLanguages = Array.isArray(supportedLanguages) ? supportedLanguages : [supportedLanguages];
    // const httpClient: HttpClient = this.context.httpClient;

    const mainSDKRepositoryName: string = sdkRepositoryMapping.mainRepository;
    const mainBranch: string = sdkRepositoryMapping.mainBranch;
    let sdkRepository: SDKRepository | undefined = first(
      this.sdkRepositories,
      (sdkRepo: SDKRepository) => sdkRepo.data.mainRepository === mainSDKRepositoryName
    );
    if (!sdkRepository) {
      const languageConfiguration: LanguageConfiguration | undefined = getLanguageConfigurationForRepository(
        sdkRepositoryName,
        supportedLanguages
      );
      if (!languageConfiguration) {
        const supportedLanguageNames: string[] = map(
          supportedLanguages,
          (language: LanguageConfiguration) => language.name
        );
        await this.logger.logError(
          `No supported programming language matches the repository ` +
          `${sdkRepositoryName}|${mainSDKRepositoryName} ` +
          `(supported languages: ${JSON.stringify(supportedLanguageNames)}).`
        );
      } else {
        await this.logger.logInfo(
          `SDK repository ${sdkRepositoryName}|${mainSDKRepositoryName} ` +
          `matches programming language ${languageConfiguration.name}.`
        );

        const swaggerToSDKConfig = await this.getSwaggerToSDKFileContent(
          mainSDKRepositoryName, mainBranch, sdkRepositoryMapping.configFilePath
        );

        if (swaggerToSDKConfig) {
          const sdkRepositoryPrefix = this.pullRequestPrefix;
          const sdkRepositoryLogsBlob: BlobStorageAppendBlob = await createLogsBlob(sdkRepositoryPrefix);

          const sdkRepositoryData: SDKRepositoryData = {
            ...sdkRepositoryMapping,
            languageName: languageConfiguration.name,
            readmeMdFileUrlsToGenerate: [],
            status: 'pending',
            messages: [],
            generationRepositoryUrl: `https://github.com/${sdkRepositoryMapping.generationRepository}`,
            integrationRepositoryUrl: `https://github.com/${sdkRepositoryMapping.integrationRepository}`,
            mainRepositoryUrl: `https://github.com/${sdkRepositoryMapping.mainRepository}`,
            secondaryRepositoryUrl: `https://github.com/${sdkRepositoryMapping.secondaryRepository}`
          };
          this.data.sdkRepositories.push(sdkRepositoryData);

          const sdkRepositoryBlobLogger: Logger = this.context.getBlobLogger(sdkRepositoryLogsBlob);
          const sdkRepositoryPrefixLogger: Logger = prefix(this.logger, `[${mainSDKRepositoryName}]`);
          const logger: Logger = getSplitLineCompositeLogger(
            sdkRepositoryPrefixLogger,
            sdkRepositoryBlobLogger,
            getRunnerReportLogger(sdkRepositoryData.messages)
          );
          const sdkRepositoryContext: SDKRepositoryContext = {
            ...this.context,
            useMergedRoutine: !!this.specificationPullRequest!.data.merged,
            specificationPullRequest: this.specificationPullRequest && this.specificationPullRequest.data,
            generationWorkingFolderPath: this.generationWorkingFolderPath,
            writeGenerationData: this.writeGenerationData
          };
          sdkRepository = new SDKRepository(
            sdkRepositoryLogsBlob,
            logger,
            languageConfiguration,
            swaggerToSDKConfig,
            sdkRepositoryPrefix,
            sdkRepositoryContext,
            sdkRepositoryData,
            this.buildID
          );
          this.sdkRepositories.push(sdkRepository);
        }
      }
    }
    return sdkRepository;
  }

  /**
   * Add an AutoRest readme.md configuration file that should be generated in the context of the
   * provided SDK repository.
   * @param sdkRepositoryConfiguration The configuration object in the specification's readme.md
   * file for this SDK's repository.
   * @param specificationReadmeMdFile The AutoRest readme.md configuration file that should be
   * generated for the provided sdkRepository.
   */
  public async addReadmeMdFileToGenerateForSDKRepository(
    sdkRepositoryConfiguration: string | RepositoryConfiguration,
    readmeMdFile: SpecificationReadmeMdFile | string,
    supportedLanguages: LanguageConfiguration | LanguageConfiguration[],
    specificationRepositoryConfiguration?: SpecificationRepositoryConfiguration
  ): Promise<void> {
    const sdkRepositoryName: string =
      typeof sdkRepositoryConfiguration === 'string' ? sdkRepositoryConfiguration : sdkRepositoryConfiguration.repo;
    const sdkRepositoryMapping: SDKRepositoryMapping = await getSDKRepositoryMapping(
      sdkRepositoryName,
      specificationRepositoryConfiguration,
      this.logger
    );
    const sdkRepository: SDKRepository | undefined = await this.getSDKRepository(
      sdkRepositoryMapping,
      sdkRepositoryName,
      supportedLanguages
    );
    if (sdkRepository) {
      const readmeMdFileUrl: string = typeof readmeMdFile === 'string' ? readmeMdFile : readmeMdFile.contentsPath;
      if (!sdkRepository.data.readmeMdFileUrlsToGenerate.includes(readmeMdFileUrl)) {
        await this.logger.logInfo(
          `Adding readme.md to generate to ${sdkRepository.data.mainRepository}: ${readmeMdFileUrl}`
        );
        sdkRepository.data.readmeMdFileUrlsToGenerate.push(readmeMdFileUrl);

        if (typeof sdkRepositoryConfiguration !== 'string' && sdkRepositoryConfiguration.after_scripts) {
          sdkRepository.addReadmdMdAfterScripts(readmeMdFileUrl, sdkRepositoryConfiguration.after_scripts);
        }
      }
    }
  }

  /**
   * Close (without merging) any SDK generation pull requests that were created by this
   * specification pull request.
   */
  public async closeSDKGenerationPullRequests(): Promise<void> {
    return this.forEachSDKRepository((sdkRepository: SDKRepository) => {
      return sdkRepository.closeGenerationPullRequests(this.context.generateLanguagesInParallel);
    });
  }

  public async getSwaggerToSDKFileContent(
    mainSDKRepositoryName: string, branchName: string, filePath: string
  ): Promise<SwaggerToSDKConfiguration|undefined> {
    const repo = getRepository(mainSDKRepositoryName);
    await this.logger.logInfo(
      `Getting swagger_to_sdk_config.json file from "${repo.owner}/${repo.name}/${branchName}/${filePath}"...`
    );

    const githubClient = await (this.context.github as RealGitHub).getClient(repo);
    const { data: configFileContent } = await githubClient.repos.getContent({
      owner: repo.owner,
      repo: repo.name,
      path: filePath,
      ref: branchName
    });
    let rawContent: string | undefined;
    let result: SwaggerToSDKConfiguration | undefined;
    if (configFileContent && !Array.isArray(configFileContent)) {
      rawContent = configFileContent.content;
      if (rawContent && configFileContent.encoding === 'base64') {
        rawContent = Buffer.from(rawContent, 'base64').toString();
      }
      if (rawContent) {
        result = JSON.parse(rawContent);
      }
    }
    return result;
  }

  /**
   * Run the provided action on each of the SDKRepositories. The context.generateLanguagesInParallel
   * property will determine whether or not the action will be run sequentially or in parallel.
   * @param action The action to run on each of the SDKRepositories.
   */
  private async forEachSDKRepository(action: (sdkRepository: SDKRepository) => Promise<void>): Promise<void> {
    if (this.context.generateLanguagesInParallel) {
      await Promise.all(
        map(this.sdkRepositories, (sdkRepository: SDKRepository) => {
          return action(sdkRepository);
        })
      );
    } else {
      for (const sdkRepository of this.sdkRepositories) {
        await action(sdkRepository);
      }
    }
  }
}

/**
 * Get the language configuration that matches the provided repository based on its name.
 * @param repository The name of the repository.
 */
export function getLanguageConfigurationForRepository(
  repository: string | Repository,
  supportedLanguages: LanguageConfiguration[]
): LanguageConfiguration | undefined {
  const lowerCasedRepositoryFullName: string = getRepositoryFullName(repository).toLowerCase();
  return first(supportedLanguages, (language: LanguageConfiguration) => {
    let matches: boolean = lowerCasedRepositoryFullName.includes(language.name.toLowerCase());
    if (!matches && language.aliases) {
      for (const alias of language.aliases) {
        matches = lowerCasedRepositoryFullName.includes(alias.toLowerCase());
        if (matches) {
          break;
        }
      }
    }
    return matches;
  });
}

/**
 * Get a new SpecificationPullRequestGeneration object based on the provided details.
 * @param pullRequestNumber The specification pull request's number.
 * @param pullRequestPrefix The blob storage prefix that all data related to the pull request will
 * be under.
 * @param context The context that the SpecificationPullRequestGeneration object should operate
 * under.
 */
export async function getSpecificationPullRequestGeneration(
  pullRequestNumber: number,
  pullRequestPrefix: BlobStoragePrefix,
  context: SpecificationPullRequestGenerationContext,
  buildID: string,
  generationNumber?: number
): Promise<SpecificationPullRequestGeneration> {
  if (generationNumber === undefined && !context.isPipelineTriggered) {
    generationNumber = await claimGenerationNumber(pullRequestPrefix);
  }
  const generationWorkingFolderPath: string = context.generationWorkingFolderPath
    ? context.generationWorkingFolderPath
    : await getGenerationWorkingFolderPath(context.automationWorkingFolderPath);
  return new SpecificationPullRequestGeneration(
    pullRequestPrefix,
    pullRequestNumber,
    generationNumber,
    buildID,
    generationWorkingFolderPath,
    context
  );
}

/**
 * Claim a generation number for the provided pull request by creating the logs blob.
 * @param pullRequestPrefix The prefix of the pull request that this generation belongs to.
 */
export async function claimGenerationNumber(pullRequestPrefix: BlobStoragePrefix): Promise<number> {
  let generationNumber = 1;
  let generationInstancePrefix: BlobStoragePrefix = getGenerationPrefix(pullRequestPrefix, generationNumber);
  while (!(await getLogsBlob(generationInstancePrefix).create({ contentType: logsBlobContentType })).created) {
    ++generationNumber;
    generationInstancePrefix = getGenerationPrefix(pullRequestPrefix, generationNumber);
  }

  return generationNumber;
}

/**
 * Get the prefix that all blobs related to this generation will be put in.
 * @param pullRequestPrefix The prefix that the pull request operates under.
 * @param generationNumber The number of generations that have occurred for the pull request.
 */
export function getGenerationPrefix(pullRequestPrefix: BlobStoragePrefix, generationNumber: number): BlobStoragePrefix {
  return pullRequestPrefix.getPrefix(generationNumber.toString() + '/');
}

/**
 * Get the blob that will be used to write the pull request's comment content to.
 * @param generationPrefix The prefix that the pull request generation operates under.
 */
export function getCommentHtmlBlob(generationPrefix: BlobStoragePrefix): BlobStorageBlockBlob {
  return generationPrefix.getBlockBlob('comment.html');
}

export function getSDKRepositoryPrefix(parentPrefix: BlobStoragePrefix, sdkRepositoryName: string): BlobStoragePrefix {
  return parentPrefix.getPrefix(`${sdkRepositoryName}/`);
}
