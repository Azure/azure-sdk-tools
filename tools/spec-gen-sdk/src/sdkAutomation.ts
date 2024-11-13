import { getCompositeLogger, Logger, splitLines } from '@azure/logger-js';
import { createTemporaryFolder, isRooted, joinPath } from './utils/fsUtils';


// eslint-disable-next-line @typescript-eslint/no-explicit-any
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
