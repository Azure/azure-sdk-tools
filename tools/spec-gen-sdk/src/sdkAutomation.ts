import {
  getCompositeLogger,
  Logger,
  LoggerOptions,
  splitLines,
  timestamps,
  wrapLogger,
  getDefaultLogger
} from '@azure/logger-js';
import {
  ArchiverCompressor,
  BlobStorageAppendBlob,
  BlobStorageBlockBlob,
  BlobStorageContainer,
  BlobStoragePrefix,
  Compressor,
  createTemporaryFolder,
  ExecutableGit,
  FakeGitHub,
  findFileInPath,
  findFolderInPath,
  getChildFilePaths,
  getRootPath,
  GitHub,
  HttpClient,
  isRooted,
  joinPath,
  map,
  NodeHttpClient,
  normalizePath,
  PackageJson,
  pathRelativeTo,
  readPackageJsonFileSync,
  Repository,
  Runner,
  toArray
} from '@ts-common/azure-js-dev-tools';
import { BlobProxy } from './blobProxy';
import { FakeBlobProxy } from './fakeBlobProxy';
import { pythonTrack2 } from './langSpecs/langs/pythonTrack2';
import { dotnetTrack2 } from './langSpecs/langs/dotnetTrack2';
import { dotnet } from './langSpecs/langs/dotnet';
import { go } from './langSpecs/langs/go';
import { java } from './langSpecs/langs/java';
import { javascript } from './langSpecs/langs/javascript';
import { LanguageConfiguration } from './langSpecs/languageConfiguration';
import { python } from './langSpecs/langs/python';
import { ruby } from './langSpecs/langs/ruby';
import { cli } from './langSpecs/langs/cli';
import { trenton } from './langSpecs/langs/trenton';
import { azureresourceschema } from './langSpecs/langs/azureresourceschema';

import {
  getSDKAutomationStateImageBlob,
  getSDKAutomationStateImageName,
  getSDKAutomationStates
} from './sdkAutomationState';
import { getSpecificationPullRequest } from './specificationPullRequest';
import { resolve } from 'path';

/**
 * The optional properties that can be provided when creating a new SDKAutomation object.
 */
export interface SDKAutomationOptions {
  /**
   * The main logger that will be used to log the automation's state. Typically this will be a
   * central telemetry database. This logger will be composed together with additional loggers as an
   * automation event is processed.
   */
  readonly logger?: Logger;
  /**
   * The client that will be used to interact with GitHub.
   */
  readonly github?: GitHub;
  /**
   * The client that will be used to interact with Git.
   */
  readonly git?: ExecutableGit;
  /**
   * The language configurations that will be supported.
   */
  readonly supportedLanguageConfigurations?:
    | LanguageConfiguration
    | LanguageConfiguration[]
    | ((defaultLanguages: LanguageConfiguration[]) => void | LanguageConfiguration | LanguageConfiguration[]);
  /**
   * A factory function that creates Compressor objects.
   */
  readonly compressorCreator?: () => Compressor;
  /**
   * The runner object that will be used to execute external-process commands.
   */
  readonly runner?: Runner;
  /**
   * The client that will be used to send HTTP requests.
   */
  readonly httpClient?: HttpClient;
  /**
   * Whether or not to delete the locally cloned repositories used for generation. Defaults to true.
   */
  readonly deleteClonedRepositories?: boolean;
  /**
   * An object that can be used to create and resolve blob proxy URLs.
   */
  readonly blobProxy?: BlobProxy;
  /**
   * Whether or not to generate each language in parallel. Defaults to false.
   */
  readonly generateLanguagesInParallel?: boolean;
  /**
   * Whether or not to add timestamps to the blob loggers.
   */
  readonly logTimestamps?: boolean;
  /**
   * Whether or not to create generation pull requests for SDK repositories.
   */
  readonly createGenerationPullRequests?: boolean;
  /**
   * Author name of the generated comment displayed on github. Add "[bot]" to
   * end of the name if we are github app.
   */
  readonly githubCommentAuthorName?: string;

  /**
   * Enable Build.BuildID in pipeline
   */
  readonly buildID?: string;

  readonly downloadCommandTemplate?: string;

  readonly isPublic?: boolean;
}

export interface SDKAutomationContext {
  /**
   * The main logger that will be used to log the automation's state. Typically this will be a
   * central telemetry database. This logger will be composed together with additional loggers as an
   * automation event is processed.
   */
  logger: Logger;
  /**
   * The client that will be used to interact with GitHub.
   */
  github: GitHub;
  /**
   * The client that will be used to interact with Git.
   */
  git: ExecutableGit;
  /**
   * The language configurations that will be supported.
   */
  readonly supportedLanguageConfigurations: LanguageConfiguration[];
  /**
   * A factory function that creates Compressor objects.
   */
  readonly createCompressor: () => Compressor;
  /**
   * The runner object that will be used to execute external-process commands.
   */
  readonly runner?: Runner;
  /**
   * The folder where SDKAutomation can create folders and clone repositories to.
   */
  readonly automationWorkingFolderPath: string;
  /**
   * Whether or not to delete the locally cloned repositories used for generation.
   */
  readonly deleteClonedRepositories: boolean;
  /**
   * An object that can be used to create and resolve blob proxy URLs.
   */
  readonly blobProxy: BlobProxy;
  /**
   * Whether or not to generate each language in parallel.
   */
  readonly generateLanguagesInParallel: boolean;
  /**
   * Whether or not to add timestamps to the blob loggers.
   */
  readonly logTimestamps: boolean;
  /**
   * Whether or not to create generation pull requests for SDK repositories.
   */
  readonly createGenerationPullRequests: boolean;
  /**
   * Author name of the generated comment displayed on github. Add "[bot]" to
   * end of the name if we are github app.
   */
  readonly githubCommentAuthorName: string;

  readonly getBlobLogger: (appendBlob: BlobStorageAppendBlob, options?: BlobLoggerOptions) => Logger;

  readonly artifactDownloadCommand: (url: string, filename: string) => string;

  readonly logContext: (logger?: Logger) => Promise<void>;

  readonly isPublic: boolean;

  sdkAutomationVersion?: string;

  isPipelineTriggered: boolean;

  buildID: string;
}

/**
 * The main class for the SDK Automation application.
 */
export class SDKAutomation {
  public context: SDKAutomationContext;

  /**
   * Create a new SDKAutomation object that will use the provided context to handle events.
   * @param automationWorkingFolderPath The folder where SDKAutomation will do its work. All folders and files
   * created will be done inside of this folder.
   * @param context The context that the SDKAutomation object will use to handle events.
   */
  constructor(automationWorkingFolderPath: string, options: SDKAutomationOptions = {}) {
    this.context = {
      logger: (options.logger && splitLines(options.logger)) || getDefaultLogger(),
      github: getGitHub(options.github, options.logger),
      git: getGit(options.git, options.logger),
      supportedLanguageConfigurations: getSupportedLanguages(options.supportedLanguageConfigurations, options.logger),
      createCompressor: getCompressorCreator(options.compressorCreator),
      getBlobLogger: this.getBlobLogger,
      runner: options.runner,
      automationWorkingFolderPath: resolve(automationWorkingFolderPath),
      deleteClonedRepositories:
        options.deleteClonedRepositories === undefined ? true : options.deleteClonedRepositories,
      blobProxy: getBlobProxy(options.blobProxy, options.logger),
      generateLanguagesInParallel: !!options.generateLanguagesInParallel,
      logTimestamps: !!options.logTimestamps,
      createGenerationPullRequests: !!options.createGenerationPullRequests,
      githubCommentAuthorName: options.githubCommentAuthorName || 'openapi-bot[bot]',
      buildID: options.buildID || '01',
      isPipelineTriggered: false,
      logContext: this.logContext,
      artifactDownloadCommand: options.downloadCommandTemplate
        ? (url, filename) => options.downloadCommandTemplate!.replace('{URL}', url).replace('{FILENAME}', filename)
        : (url) => `curl ${url}`,
      isPublic: !!options.isPublic
    };
  }

  /**
   * Log the context that a SpecificationPREvent will be handled with.
   */
  public logContext = async (logger?: Logger): Promise<void> => {
    if (!logger) {
      logger = this.context.logger;
    }

    if (logger) {
      this.context.sdkAutomationVersion = await getOpenAPISDKAutomationVersion(logger);

      if (this.context.github instanceof FakeGitHub) {
        await logger.logWarning(`Using FakeGitHub client.`);
      } else {
        await logger.logInfo(`Using non-FakeGitHub client.`);
      }
    }
  }

  public async pipelineTrigger(
    repo: Repository,
    pullRequestNumber: number,
    workingPrefix: BlobStoragePrefix,
    sdkRepoName: string,
    isTriggeredByUP: boolean
  ): Promise<void> {
    this.context.isPipelineTriggered = true;
    await executeAndLog(this.context.logger, 'Failed to run SDK Automation: ', async () => {
      await ensurePrefixContainerExists(workingPrefix, this.context.logger);
      const pullRequest = await this.context.github.getPullRequest(repo, pullRequestNumber);
      if (pullRequest.state === 'closed' && !pullRequest.merged) {
        throw new Error(`TriggerError: PR is closed: ${pullRequest.html_url}. Please re-open the PR if you want to trigger the SDK generation.`);
      }
      const specPR = await getSpecificationPullRequest(
        this.context,
        workingPrefix,
        pullRequest,
        true,
        isTriggeredByUP,
        sdkRepoName
      );
      await specPR.generateModifiedServices();
    });
  }

  public async filterSDKReposToTrigger(
    repo: Repository,
    pullRequestNumber: number,
    workingPrefix: BlobStoragePrefix,
    isTriggeredByUP: boolean
  ): Promise<void> {
    this.context.isPipelineTriggered = true;
    await executeAndLog(this.context.logger, 'Failed to run SDK Automation: ', async () => {
      await ensurePrefixContainerExists(workingPrefix, this.context.logger);
      const pullRequest = await this.context.github.getPullRequest(repo, pullRequestNumber);
      if (pullRequest.state === 'closed' && !pullRequest.merged) {
        throw new Error(`TriggerError: PR is closed: ${pullRequest.html_url}. Please re-open the PR if you want to trigger the SDK generation.`);
      }

      const specPR = await getSpecificationPullRequest(this.context, workingPrefix, pullRequest, true, isTriggeredByUP);
      const readmeMdRepositories = await specPR.getSDKReposToTrigger();
      const enabledJobs = ''.concat('|', readmeMdRepositories.join('|'), '|');

      console.log(`##vso[task.setVariable variable=EnabledJobs;isOutput=true]${enabledJobs}`);
    });
  }

  /**
   * Create a Logger that writes its logs to the provided blob.
   * @param appendBlob The blob to log to.
   * @param options Options for the new logger.
   */
  public getBlobLogger = (appendBlob: BlobStorageAppendBlob, options?: BlobLoggerOptions): Logger => {
    return getBlobLogger(appendBlob, {
      ...options,
      logTimestamps: (options && options.logTimestamps) || this.context.logTimestamps
    });
  }
}

/**
 * Get the version of the openapi-sdk-automation package that the application is using.
 */
export async function getOpenAPISDKAutomationVersion(logger?: Logger): Promise<string | undefined> {
  if (!logger) {
    logger = getDefaultLogger();
  }
  let result: string | undefined;
  const expectedPackageName = 'openapi-sdk-automation';
  const packageJsonFilePath: string | undefined = await findFileInPath('package.json', __dirname);
  if (!packageJsonFilePath) {
    await logger.logWarning(`No package.json file found for ${expectedPackageName}.`);
  } else {
    const packageJson: PackageJson | undefined = readPackageJsonFileSync(packageJsonFilePath);
    if (!packageJson) {
      await logger.logWarning(`Failed to parse package.json file at ${packageJsonFilePath}.`);
    } else {
      if (packageJson.name !== expectedPackageName) {
        await logger.logWarning(
          `Closest package.json file (${packageJsonFilePath}) was not for ${expectedPackageName}.`
        );
      } else {
        await logger.logInfo(`Using ${expectedPackageName} version ${packageJson.version}.`);
        result = packageJson.version;
      }
    }
  }
  return result;
}

// tslint:disable-next-line: no-any
export function errorToLog(error: any, withStack: boolean = false): string {
  return `${error.toString()}, ${JSON.stringify(error, undefined, 2)} ${(withStack && error.stack) || ''}`;
}

/**
 * Run the provided action and log any error that is thrown.
 * @param logger The logger to use to log the error.
 * @param errorMessagePrefix The prefix to prepend to the logged error message.
 * @param action The action to run.
 */
export async function executeAndLog<T>(
  logger: Logger | undefined,
  errorMessagePrefix: string | undefined,
  action: () => T | Promise<T>
): Promise<T> {
  let result: T;
  try {
    result = await Promise.resolve(action());
  } catch (error) {
    if (logger) {
      await logger.logError(`${errorMessagePrefix || ''}${errorToLog(error)}`);
    }
    throw error;
  }
  return result;
}

async function ensurePrefixContainerExists(prefix: BlobStoragePrefix, logger?: Logger): Promise<void> {
  if (!logger) {
    logger = getDefaultLogger();
  }
  const prefixContainer: BlobStorageContainer = prefix.getContainer();
  await logger.logInfo(`Ensuring that the container at ${prefixContainer.getURL()} exists...`);
  await prefixContainer.create();
}

const imagesFolderName = 'images';
/**
 * Ensure that the state images exist in the provided prefix. If they don't exist, then upload them.
 * @param prefix The BlobStoragePrefix to ensure that the state images exist in.
 */
export async function ensureStateImagesExist(prefix: BlobStoragePrefix, logger?: Logger): Promise<void> {
  if (!logger) {
    logger = getDefaultLogger();
  }
  const imagesFolderPath: string | undefined = await findFolderInPath(imagesFolderName, __dirname);
  if (!imagesFolderPath) {
    await logger.logWarning(`Could not find the "${imagesFolderName}" folder.`);
  } else {
    await logger.logInfo(`Found the "${imagesFolderName}" folder at "${imagesFolderPath}".`);

    await ensurePrefixContainerExists(prefix, logger);

    for (const state of getSDKAutomationStates()) {
      const imageBlob: BlobStorageBlockBlob = getSDKAutomationStateImageBlob(prefix, state);
      const imageName: string = getSDKAutomationStateImageName(state);
      const imageFilePath: string = joinPath(imagesFolderPath, imageName);
      await logger.logInfo(`Uploading ${imageFilePath} to ${imageBlob.getURL()}...`);
      await imageBlob.setContentsFromFile(imageFilePath, { contentType: `image/gif` });
    }
  }
}

const schemasFolderName = 'schemas';
/**
 * Ensure that the JSON schemas for the SDK Automation configuration files exist in the provided
 * prefix. If they don't exist, then upload them.
 * @param prefix The BlobStoragePrefix to ensure that the JSON schemas exist in.
 */
export async function ensureJSONSchemasExist(prefix: BlobStoragePrefix, logger?: Logger): Promise<void> {
  if (!logger) {
    logger = getDefaultLogger();
  }
  const schemasFolderPath: string | undefined = await findFolderInPath(schemasFolderName, __dirname);
  if (!schemasFolderPath) {
    await logger.logWarning(`Could not find the "${schemasFolderName}" folder.`);
  } else {
    await logger.logInfo(`Found the "${schemasFolderName}" folder at "${schemasFolderPath}".`);

    await ensurePrefixContainerExists(prefix, logger);

    const schemaFilePaths: string[] | undefined = await getChildFilePaths(schemasFolderPath, { recursive: true });
    if (!schemaFilePaths) {
      await logger.logWarning(`No "${schemasFolderName}" folder found.`);
    } else {
      if (logger) {
        await logger.logInfo(`Found ${schemaFilePaths.length} schema files:`);
        for (const schemaFilePath of schemaFilePaths) {
          await logger.logInfo(schemaFilePath);
        }
      }

      const schemaPrefix: BlobStoragePrefix = prefix.getPrefix(`${schemasFolderName}/`);
      for (const schemaFilePath of schemaFilePaths) {
        const schemaRelativeFilePath: string = pathRelativeTo(schemaFilePath, schemasFolderPath);
        const schemaBlob: BlobStorageBlockBlob = schemaPrefix.getBlockBlob(schemaRelativeFilePath);
        await logger.logInfo(`Uploading ${schemaFilePath} to ${schemaBlob.getURL()}...`);
        await schemaBlob.setContentsFromFile(schemaFilePath, { contentType: `application/json` });
      }
    }
  }
}

/**
 * All of the languages that are supported by SwaggerToSDK.
 */
export function getAllLanguages(): LanguageConfiguration[] {
  return [dotnetTrack2, pythonTrack2, dotnet, go, java, javascript, python, ruby, cli, trenton, azureresourceschema];
}

/**
 * Get the client that will be used by an SDKAutomation object to interact with GitHub.
 * @param github The GitHub client provided to the SDKAutomation constructor.
 */
export function getGitHub(github?: GitHub, logger?: Logger): GitHub {
  if (logger) {
    if (github) {
      // tslint:disable-next-line: no-floating-promises
      logger.logInfo(`Using provided GitHub.`);
    } else {
      // tslint:disable-next-line: no-floating-promises
      logger.logInfo(`No provided GitHub. Using FakeGitHub instance.`);
    }
  }
  return github || new FakeGitHub();
}

/**
 * Get the client that will be used by an SDKAutomation object to interact with Git.
 * @param git The Git client provided to the SDKAutomation constructor.
 */
export function getGit(git?: ExecutableGit, logger?: Logger): ExecutableGit {
  if (logger) {
    if (git) {
      // tslint:disable-next-line: no-floating-promises
      logger.logInfo(`Using provided Git.`);
    } else {
      // tslint:disable-next-line: no-floating-promises
      logger.logInfo(`No provided Git. Using ExecutableGit instance.`);
    }
  }
  return git || new ExecutableGit();
}

/**
 * Get the languages that are supported by an SDKAutomation object.
 * @param languages The languages that are supported by an SDKAutomation object.
 */
export function getSupportedLanguages(
  languages?:
    | LanguageConfiguration
    | LanguageConfiguration[]
    | ((defaultLanguages: LanguageConfiguration[]) => void | LanguageConfiguration | LanguageConfiguration[]),
  logger?: Logger
): LanguageConfiguration[] {
  let result: LanguageConfiguration[];
  if (Array.isArray(languages)) {
    result = languages;
  } else if (languages && typeof languages !== 'function') {
    result = [languages];
  } else {
    result = getAllLanguages();
    if (languages) {
      const functionResult: void | LanguageConfiguration | LanguageConfiguration[] = languages(result);
      if (functionResult) {
        result = Array.isArray(functionResult) ? functionResult : [functionResult];
      }
    }
  }

  if (logger) {
    // tslint:disable-next-line: no-floating-promises
    logger.logInfo(
      `Using supported languages: ${JSON.stringify(map(result, (language: LanguageConfiguration) => language.name))}`
    );
  }

  return result;
}

/**
 * Get the compressor creator that an SDKAutomation object will use to handle events.
 * @param compressorCreator The compressorCreator provided to the SDKAutomation constructor.
 */
export function getCompressorCreator(compressorCreator?: () => Compressor): () => Compressor {
  return compressorCreator || (() => new ArchiverCompressor());
}

/**
 * Get the HttpClient that an SDKAutomation object will use to handle events.
 * @param httpClient The HttpClient provided to the SDKAutomation constructor.
 */
export function getHttpClient(httpClient?: HttpClient, logger?: Logger): HttpClient {
  if (logger) {
    if (httpClient) {
      // tslint:disable-next-line: no-floating-promises
      logger.logInfo(`Using provided HttpClient.`);
    } else {
      // tslint:disable-next-line: no-floating-promises
      logger.logInfo(`No HttpClient provided. Using NodeHttpClient.`);
    }
  }
  return httpClient || new NodeHttpClient();
}

/**
 * Get the path to the current working directory's root.
 */
export function getRootFolderPath(): string {
  return normalizePath(getRootPath(process.cwd())!);
}

/**
 * Get the working folder path that SDK Automation should work under.
 * @param automationWorkingFolderPath The suggested folder path that SDK Automation should work
 * under. If this isn't specified, then the current working directory will be used.
 */
export function getAutomationWorkingFolderPath(automationWorkingFolderPath?: string): string {
  if (!automationWorkingFolderPath) {
    automationWorkingFolderPath = process.cwd();
  } else if (!isRooted(automationWorkingFolderPath)) {
    automationWorkingFolderPath = joinPath(process.cwd(), automationWorkingFolderPath);
  }
  return automationWorkingFolderPath;
}

/**
 * Create a temporary working folder in the provided base working folder path. If a temporary folder
 * cannot be created in the provided base working folder path, then this function will attempt to
 * create a temporary working folder in the current working directory.
 * @param automationWorkingFolderPath The ideal folder that working folders will be created in.
 */
export async function getGenerationWorkingFolderPath(automationWorkingFolderPath?: string): Promise<string> {
  const customAutomationWorkingFolderPath: boolean = !!automationWorkingFolderPath;
  automationWorkingFolderPath = getAutomationWorkingFolderPath(automationWorkingFolderPath);

  let generationWorkingFolderPath: string;
  try {
    generationWorkingFolderPath = await createTemporaryFolder(automationWorkingFolderPath);
  } catch (error) {
    if (!customAutomationWorkingFolderPath) {
      throw error;
    } else {
      // Error when trying to create the working folder path. Fall back to creating a temporary
      // folder in the current working directory.
      generationWorkingFolderPath = await createTemporaryFolder(process.cwd());
    }
  }

  return generationWorkingFolderPath;
}

/**
 * Get a blob that can be used to write logs to.
 * @param prefix The prefix that the logs will be written under.
 * @param blobName The name of the logs blob. Defaults to "logs.txt".
 */
export function getLogsBlob(prefix: BlobStoragePrefix, blobName: string = 'logs.txt'): BlobStorageAppendBlob {
  return prefix.getAppendBlob(blobName);
}

/**
 * The content type of log blobs.
 */
export const logsBlobContentType = 'text/plain';

/**
 * Create a blob that can be used to write logs to.
 * @param prefix The prefix that the logs will be written under.
 * @param blobName The name of the logs blob. Defaults to "logs.txt".
 */
export async function createLogsBlob(
  prefix: BlobStoragePrefix,
  blobName: string = 'logs.txt'
): Promise<BlobStorageAppendBlob> {
  const logsBlob: BlobStorageAppendBlob = getLogsBlob(prefix, blobName);
  await logsBlob.delete();
  await logsBlob.create({ contentType: logsBlobContentType });
  return logsBlob;
}

/**
 * Get a BlobStorageBlockBlob that can be used to write data under the provided prefix.
 * @param prefix The prefix that the data blob will be written under.
 */
export function getDataBlob(prefix: BlobStoragePrefix): BlobStorageBlockBlob {
  return prefix.getBlockBlob('data.json');
}

export interface BlobLoggerOptions extends LoggerOptions {
  /**
   * Whether or not to add timestamps to blob loggers.
   */
  readonly logTimestamps?: boolean;

  /**
   * Whether not to return instantly and defer the log action in background.
   */
  readonly blockingAsync?: boolean;
}

/**
 * Create a Logger that writes its logs to the provided blob.
 * @param appendBlob The blob to log to.
 * @param options Options for the new logger.
 */
export function getBlobLogger(appendBlob: BlobStorageAppendBlob, options?: BlobLoggerOptions): Logger {
  let contentBuffer = '';
  let flushingInProgress = false;

  const addToContents = async (contentsToAdd: string): Promise<unknown> => {
    try {
      return await appendBlob.addToContents(contentsToAdd);
    } catch (e) {
      await appendBlob.create({ contentType: 'text/plain' });
      return appendBlob.addToContents(contentsToAdd);
    }
  };

  const log = (text: string | string[]): Promise<unknown> => {
    let contentsToAdd = '';
    for (const textLine of toArray(text)) {
      contentsToAdd += `${textLine}\n`;
    }

    if (options && options.blockingAsync) {
      return addToContents(contentsToAdd);
    }

    // Deferred logging
    contentBuffer += contentsToAdd;
    if (flushingInProgress) {
      // Someone else is writing to log. Let's return in advance.
      return Promise.resolve(true);
    }

    // Flushing logs.
    flushingInProgress = true;
    // tslint:disable-next-line: no-floating-promises
    (async () => {
      while (contentBuffer !== '') {
        const contentToFlush = contentBuffer;
        contentBuffer = '';
        await addToContents(contentToFlush);
      }
      flushingInProgress = false;
    })();

    return Promise.resolve(true);
  };

  let result: Logger = {
    logInfo: log,
    logError: log,
    logWarning: log,
    logSection: log,
    logVerbose: log
  };
  if (options) {
    result = wrapLogger(result, options);
    if (options.logTimestamps) {
      result = timestamps(result);
    }
  }
  return result;
}

export function getBlobProxy(blobProxy?: BlobProxy, logger?: Logger): BlobProxy {
  if (logger) {
    if (blobProxy) {
      // tslint:disable-next-line: no-floating-promises
      logger.logInfo(`Using the provided BlobProxy.`);
    } else {
      // tslint:disable-next-line: no-floating-promises
      logger.logInfo(`No BlobProxy provided. Using a FakeBlobProxy.`);
    }
  }
  return blobProxy || new FakeBlobProxy();
}

/**
 * Trim a single newline sequence from the end of the provided value, if the newline sequence
 * exists on the value.
 * @param value The value to trim.
 */
export function trimNewLine(value: string): string {
  const trimEndLength: number = value.endsWith('\r\n') ? 2 : value.endsWith('\n') ? 1 : 0;
  return trimEndLength === 0 ? value : value.substring(0, value.length - trimEndLength);
}

/**
 * Get a composite logger that will split the provided text logs into individual lines before
 * passing them onto the composed loggers.
 */
export function getSplitLineCompositeLogger(...loggers: (Logger | undefined)[]): Logger {
  return splitLines(getCompositeLogger(...loggers));
}
