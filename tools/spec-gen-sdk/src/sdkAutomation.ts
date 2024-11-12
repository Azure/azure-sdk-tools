import {
  getCompositeLogger,
  Logger,
  LoggerOptions,
  splitLines,
  timestamps,
  wrapLogger,
} from '@azure/logger-js';
import {
  BlobStorageAppendBlob,
  BlobStorageBlockBlob,
  BlobStoragePrefix,
  Compressor,
  createTemporaryFolder,
  ExecutableGit,
  GitHub,
  HttpClient,
  isRooted,
  joinPath,
  Runner,
  toArray
} from '@ts-common/azure-js-dev-tools';
import { BlobProxy } from './blobProxy';
import { FakeBlobProxy } from './fakeBlobProxy';
import { dotnetTrack2 } from './langSpecs/langs/dotnetTrack2';
import { dotnet } from './langSpecs/langs/dotnet';
import { go } from './langSpecs/langs/go';
import { java } from './langSpecs/langs/java';
import { javascript } from './langSpecs/langs/javascript';
import { LanguageConfiguration } from './langSpecs/languageConfiguration';
import { python } from './langSpecs/langs/python';

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

/**
 * All of the languages that are supported by SwaggerToSDK.
 */
export function getAllLanguages(): LanguageConfiguration[] {
  return [dotnetTrack2, dotnet, go, java, javascript, python];
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
