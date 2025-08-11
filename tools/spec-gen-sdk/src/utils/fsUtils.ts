import path from 'path';
import fs from 'fs';
import { WorkflowContext } from '../types/Workflow';
import { toolError } from './messageUtils';

export const writeTmpJsonFile = (context: WorkflowContext, fileName: string, content: unknown) => {
  const filePath = path.join(context.tmpFolder, fileName);
  const contentString = JSON.stringify(content, undefined, 2);
  context.logger.info(`Write temp file ${filePath} with content:`);
  context.logger.info(JSON.stringify(content, undefined, 2));
  fs.writeFileSync(filePath, contentString);
};

export const readTmpJsonFile = (context: WorkflowContext, fileName: string): unknown | undefined => {
  const filePath = path.join(context.tmpFolder, fileName);

  if (!fs.existsSync(filePath)) {
    context.logger.warn(`Warning: File ${filePath} not found to read. Re-run if the error is transient or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
    return undefined;
  }

  try {
    context.logger.info(`Read temp file ${filePath} with content:`);
    const contentString = fs.readFileSync(filePath).toString();
    const content = JSON.parse(contentString);
    context.logger.info(JSON.stringify(content, undefined, 2));
    return content;
  } catch (e) {
    const message = toolError(`Failed to read ${fileName}: ${e.message}. Re-run if the error is retryable or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
    context.logger.error(message);
    return undefined;
  }
};

export const deleteTmpJsonFile = (context: WorkflowContext, fileName: string) => {
  const filePath = path.join(context.tmpFolder, fileName);
  if (fs.existsSync(filePath)) {
    fs.unlinkSync(filePath);
  }
};

/**
 * Join the provided path segments using a forward slash (/) as a path separator.
 * @param pathSegments The path segments to resolve.
 * @returns The resolved path.
 */
export function joinPath(...pathSegments: string[]): string {
    return normalizePath(path.posix.join(...pathSegments));
  }
  
  /**
   * Replace all of the backslashes (\) with forward slashes (/), unless the provided osPlatform is
   * win32. If the osPlatform is win32, then all forward slashes (/) will be replaced with backslahes
   * (\).
   * @param pathString The path to normalize.
   * @returns The normalized path.
   */
  export function normalizePath(pathString: string, osPlatform?: string): string {
    let result: string;
    if (!pathString) {
      result = pathString;
    } else if (osPlatform === "win32") {
      result = pathString.replace(/\//g, "\\");
    } else {
      result = pathString.replace(/\\/g, "/");
    }
    return result;
  }
  
  /**
   * Get the root path of the provided path string. If the provided path string is relative (not
   * rooted), then undefined will be returned.
   * @param pathString The path to get the root of.
   */
  export function getRootPath(pathString: string): string | undefined {
    let result: string | undefined;
    if (pathString) {
      result = path.win32.parse(pathString).root || undefined;
      if (!result) {
        result = path.posix.parse(pathString).root || undefined;
      }
    }
    return result;
  }
  
  /**
   * Check whether or not the provided pathString is rooted (absolute).
   * @param pathString The path to check.
   * @returns Whether or not the provided pathString is rooted (absolute).
   */
  export function isRooted(pathString: string): boolean {
    return !!getRootPath(pathString);
  }
  
  /**
   * Get the name/last segment of the provided path string.
   * @param pathString The path to get the name/last segment of.
   * @returns The name/last segment of the provided path string.
   */
  export function getName(pathString: string): string {
    return getPathName(pathString);
  }
  
  /**
   * Get the name/last segment of the provided path string.
   * @param pathString The path to get the name/last segment of.
   * @returns The name/last segment of the provided path string.
   */
  export function getPathName(pathString: string): string {
    const lastSlashIndex: number = Math.max(pathString.lastIndexOf("/"), pathString.lastIndexOf("\\"));
    return lastSlashIndex === -1 ? pathString : pathString.substring(lastSlashIndex + 1);
  }