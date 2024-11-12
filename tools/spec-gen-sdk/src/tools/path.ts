/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

import * as path from "path";

/**
 * Join the provided path segments using a forward slash (/) as a path separator.
 * @param pathSegments The path segments to resolve.
 * @returns The resolved path.
 */
export function joinPath(...pathSegments: string[]): string {
  return normalizePath(path.posix.join(...pathSegments));
}

/**
 * Resolve the provided path segments using a forward slash (/) as a path separator.
 * @param pathSegments The path segments to resolve.
 * @returns The resolved path.
 */
export function resolvePath(...pathSegments: string[]): string {
  return normalizePath(path.posix.resolve(...pathSegments));
}

/**
 * Get the relative path from the provided basePath to the provided absolutePath. For example,
 * pathRelativeTo("/my/path", "/") will return "my/path".
 */
export function pathRelativeTo(absolutePath: string, basePath: string): string {
  let result: string = normalizePath(path.relative(normalizePath(basePath), normalizePath(absolutePath)));
  if (result.endsWith("/..")) {
    result += "/";
  }
  return result;
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
 * Return the provided path without its file extension.
 * @param path The path.
 */
export function pathWithoutFileExtension(path: string): string {
  const lastDot: number = path.lastIndexOf(".");
  return lastDot === -1 ? path : path.substring(0, lastDot);
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

/**
 * Get the path to the parent folder of the provided path string.
 * @param pathString The path to the get the parent folder path of.
 * @returns The path to the parent folder of the provided path string.
 */
export function getParentFolderPath(pathString: string): string {
  return path.dirname(pathString);
}
