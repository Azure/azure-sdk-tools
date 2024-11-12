/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

import * as fs from "fs";
import { any } from "./arrays";
import { getParentFolderPath, getPathName, joinPath, isRooted } from "./path";

async function _entryExists(entryPath: string, condition?: (stats: fs.Stats) => (boolean | Promise<boolean>)): Promise<boolean> {
  return new Promise((resolve, reject) => {
    fs.lstat(entryPath, (error: NodeJS.ErrnoException | null, stats: fs.Stats) => {
      if (error) {
        if (error.code === "ENOENT" || error.code === "ENOTDIR") {
          resolve(false);
        } else {
          reject(error);
        }
      } else {
        resolve(!condition || condition(stats));
      }
    });
  });
}

function _entryExistsSync(entryPath: string, condition?: (stats: fs.Stats) => boolean): boolean {
  let result = false;
  try {
    const stat: fs.Stats = fs.lstatSync(entryPath);
    result = !!(!condition || condition(stat));
  } catch (error) {
  }
  return result;
}

/**
 * Get whether or not a file entry (file or folder) exists at the provided entryPath.
 * @param entryPath The path to the file entry to check.
 * @returns Whether or not a file entry (file or folder) exists at the provided entryPath.
 */
export function entryExists(entryPath: string): Promise<boolean> {
  return _entryExists(entryPath);
}

/**
 * Check whether or not a symbolic link exists at the provided path.
 * @param symbolicLinkPath The path to check.
 * @returns Whether or not a symbolic link exists at the provided path.
 */
export function symbolicLinkExists(symbolicLinkPath: string): Promise<boolean> {
  return _entryExists(symbolicLinkPath, (stats: fs.Stats) => stats.isSymbolicLink());
}


/**
 * Check whether or not a file exists at the provided filePath.
 * @param filePath The path to check.
 * @returns Whether or not a file exists at the provided filePath.
 */
export function fileExists(filePath: string): Promise<boolean> {
  return _entryExists(filePath, (stats: fs.Stats) => stats.isFile());
}

/**
 * Check whether or not a file exists at the provided filePath.
 * @param filePath The path to check.
 * @returns Whether or not a file exists at the provided filePath.
 */
export function fileExistsSync(filePath: string): boolean {
  return _entryExistsSync(filePath, (stats: fs.Stats) => stats.isFile());
}

/**
 * Check whether or not a folder exists at the provided folderPath.
 * @param folderPath The path to check.
 * @returns Whether or not a folder exists at the provided folderPath.
 */
export function folderExists(folderPath: string): Promise<boolean> {
  return _entryExists(folderPath, (stats: fs.Stats) => stats.isDirectory());
}

export function _createFolder(folderPath: string): Promise<boolean> {
  return new Promise((resolve, reject) => {
    fs.mkdir(folderPath, (error: NodeJS.ErrnoException | null) => {
      if (error) {
        if (error.code === "EEXIST") {
          resolve(false);
        } else {
          reject(error);
        }
      } else {
        resolve(true);
      }
    });
  });
}

/**
 * Create a folder at the provided folderPath. If the folder is successfully created, then true will
 * be returned. If the folder already exists, then false will be returned.
 * @param folderPath The path to create a folder at.
 */
export async function createFolder(folderPath: string): Promise<boolean> {
  let result: boolean | Promise<boolean> | undefined;
  try {
    result = await _createFolder(folderPath);
  } catch (createFolderError) {
    if (createFolderError.code !== "ENOENT") {
      result = Promise.reject(createFolderError);
    } else {
      try {
        await createFolder(getParentFolderPath(folderPath));
        try {
          result = await _createFolder(folderPath);
        } catch (createFolderError2) {
          result = Promise.reject(createFolderError2);
        }
      } catch (createParentFolderError) {
        result = Promise.reject(createFolderError);
      }
    }
  }
  return result;
}

/**
 * Create a temporary folder and return the absolute path to the folder. If a parentFolderPath is
 * provided, then the temporary folder will be created as a child of the parentFolderPath. If
 * parentFolderPath is not provided, then the current working folder will be used as the
 * parentFolderPath.
 * @param The folder that the temporary folder will be created as a child of. Defaults to the
 * current working folder.
 */
export async function createTemporaryFolder(parentFolderPath?: string): Promise<string> {
  if (!parentFolderPath) {
    parentFolderPath = process.cwd();
  } else if (!isRooted(parentFolderPath)) {
    parentFolderPath = joinPath(process.cwd(), parentFolderPath);
  }

  let result = "";
  let i = 1;
  while (!result) {
    const tempFolderPath: string = joinPath(parentFolderPath, i.toString());
    if (await createFolder(tempFolderPath)) {
      result = tempFolderPath;
      break;
    } else {
      ++i;
    }
  }
  return result;
}

/**
 * Copy the entry at the source entry path to the destination entry path.
 * @param sourceEntryPath The path to the entry to copy from.
 * @param destinationEntryPath The path to entry to copy to.
 */
export async function copyEntry(sourceEntryPath: string, destinationEntryPath: string): Promise<void> {
  let result: Promise<void>;
  if (await fileExists(sourceEntryPath)) {
    result = copyFile(sourceEntryPath, destinationEntryPath);
  } else if (await folderExists(sourceEntryPath)) {
    result = copyFolder(sourceEntryPath, destinationEntryPath);
  } else {
    const error: NodeJS.ErrnoException = new Error(`ENOENT: no such file or directory: ${sourceEntryPath}`);
    error.code = "ENOENT";
    error.path = sourceEntryPath;
    result = Promise.reject(error);
  }
  return result;
}

/**
 * Copy the file at the source file path to the destination file path.
 * @param sourceFilePath The path to the file to copy from.
 * @param destinationFilePath The path to file to copy to.
 * @param createDestinationFolder Whether or not the destination parent folder will be created if it
 * doesn't exist.
 */
export async function copyFile(sourceFilePath: string, destinationFilePath: string, createDestinationFolder = true): Promise<void> {
  return new Promise((resolve, reject) => {
    fs.copyFile(sourceFilePath, destinationFilePath, async (error: NodeJS.ErrnoException | null) => {
      if (!error) {
        resolve();
      } else if (error.code !== "ENOENT" || !(await fileExists(sourceFilePath)) || !createDestinationFolder) {
        reject(error);
      } else {
        const destinationFolderPath: string = getParentFolderPath(destinationFilePath);
        if (await folderExists(destinationFolderPath)) {
          reject(error);
        } else {
          try {
            await createFolder(destinationFolderPath);
            await copyFile(sourceFilePath, destinationFilePath, false);
            resolve();
          } catch (error2) {
            reject(error);
          }
        }
      }
    });
  });
}

/**
 * Copy the folder at the source folder path to the destination folder path.
 * @param sourceFolderPath The path to the folder to copy from.
 * @param destinationFolderPath The path to the folder to copy to. This folder and its parent
 * folders will be created if they don't already exist.
 */
export async function copyFolder(sourceFolderPath: string, destinationFolderPath: string): Promise<void> {
  let result: Promise<void>;
  const childEntryPaths: string[] | undefined = await getChildEntryPaths(sourceFolderPath);
  if (!childEntryPaths) {
    const error: NodeJS.ErrnoException = new Error(`ENOENT: no such file or directory: ${sourceFolderPath}`);
    error.code = "ENOENT";
    error.path = sourceFolderPath;
    result = Promise.reject(error);
  } else {
    for (const childEntryPath of childEntryPaths) {
      const childEntryName: string = getPathName(childEntryPath);
      await copyEntry(childEntryPath, joinPath(destinationFolderPath, childEntryName));
    }
    result = Promise.resolve();
  }
  return result;
}

/**
 * Get whether or not the provided string completely matches the provided regularExpression.
 */
function matches(regularExpression: RegExp, possibleMatch: string): boolean {
  const matchResult = possibleMatch.match(regularExpression);
  return !!(matchResult && matchResult[0].length === possibleMatch.length);
}

export async function findEntryInPath(entryName: string | RegExp, startFolderPath: string | undefined, condition: (entryPath: string) => (boolean | Promise<boolean>)): Promise<string | undefined> {
  let result: string | undefined;
  let folderPath: string = startFolderPath || process.cwd();

  searchLoop:
  while (true) {
    if (typeof entryName === "string") {
      const folderEntryPath: string = joinPath(folderPath, entryName);
      if (await Promise.resolve(condition(folderEntryPath))) {
        result = folderEntryPath;
        break searchLoop;
      }
    } else {
      const folderEntryPaths: string[] | undefined = await getChildEntryPaths(folderPath);
      if (any(folderEntryPaths)) {
        for (const folderEntryPath of folderEntryPaths) {
          if (matches(entryName, folderEntryPath)) {
            if (await Promise.resolve(condition(folderEntryPath))) {
              result = folderEntryPath;
              break searchLoop;
            }
          } else {
            const folderEntryName: string = getPathName(folderEntryPath);
            if (matches(entryName, folderEntryName)) {
              if (await Promise.resolve(condition(folderEntryPath))) {
                result = folderEntryPath;
                break searchLoop;
              }
            }
          }
        }
      }
    }

    const parentFolderPath: string = getParentFolderPath(folderPath);
    if (!parentFolderPath || folderPath === parentFolderPath) {
      break searchLoop;
    } else {
      folderPath = parentFolderPath;
    }
  }

  return result;
}

function findEntryInPathSync(entryName: string, startFolderPath: string | undefined, condition: (entryPath: string) => boolean): string | undefined {
  let result: string | undefined;
  let folderPath: string = startFolderPath || process.cwd();
  while (folderPath) {
    const possibleResult: string = joinPath(folderPath, entryName);
    if (condition(possibleResult)) {
      result = possibleResult;
      break;
    } else {
      const parentFolderPath: string = getParentFolderPath(folderPath);
      if (!parentFolderPath || folderPath === parentFolderPath) {
        break;
      } else {
        folderPath = parentFolderPath;
      }
    }
  }
  return result;
}

/**
 * Find the closest file with the provided name by searching the immediate child folders of the
 * folder at the provided startFolderPath. If no file is found with the provided fileName, then the
 * search will move up to the parent folder of the startFolderPath. This will continue until either
 * the file is found, or the folder being searched does not have a parent folder (if it is a root
 * folder).
 * @param fileName The name of the file to look for.
 * @param startFolderPath The path to the folder where the search will begin.
 * @returns The path to the closest file with the provided fileName, or undefined if no file could
 * be found.
 */
export function findFileInPath(fileName: string | RegExp, startFolderPath?: string): Promise<string | undefined> {
  return findEntryInPath(fileName, startFolderPath, fileExists);
}

/**
 * Find the closest file with the provided name by searching the immediate child folders of the
 * folder at the provided startFolderPath. If no file is found with the provided fileName, then the
 * search will move up to the parent folder of the startFolderPath. This will continue until either
 * the file is found, or the folder being searched does not have a parent folder (if it is a root
 * folder).
 * @param fileName The name of the file to look for.
 * @param startFolderPath The path to the folder where the search will begin.
 * @returns The path to the closest file with the provided fileName, or undefined if no file could
 * be found.
 */
export function findFileInPathSync(fileName: string, startFolderPath?: string): string | undefined {
  return findEntryInPathSync(fileName, startFolderPath, fileExistsSync);
}

/**
 * Find the closest folder with the provided name by searching the immediate child folders of the
 * folder at the provided startFolderPath. If no folder is found with the provided folderName, then
 * the search will move up to the parent folder of the startFolderPath. This will continue until
 * either the folder is found, or the folder being searched does not have a parent folder (it is a
 * root folder).
 * @param folderName The name of the folder to look for.
 * @param startFolderPath The path to the folder where the search will begin.
 * @returns The path to the closest folder with the provided folderName, or undefined if no folder
 * could be found.
 */
export function findFolderInPath(folderName: string | RegExp, startFolderPath?: string): Promise<string | undefined> {
  return findEntryInPath(folderName, startFolderPath, folderExists);
}

/**
 * Optional parameters to the getChildFilePaths() function.
 */
export interface GetChildEntriesOptions {
  /**
   * Whether or not to search sub-folders of the provided folderPath.
   */
  recursive?: boolean | ((folderPath: string) => (boolean | Promise<boolean>));

  /**
   * A condition that a child entry path must pass before it will be added to the result.
   */
  condition?: (entryPath: string) => (boolean | Promise<boolean>);

  /**
   * A condition that a child file path must pass before it will be added to the result.
   */
  fileCondition?: (filePath: string) => (boolean | Promise<boolean>);

  /**
   * A condition that a child folder path must pass before it will be added to the result.
   */
  folderCondition?: (folderPath: string) => (boolean | Promise<boolean>);

  /**
   * The array where the matching child folder paths will be added.
   */
  result?: string[];
}

/**
 * Get the child entries of the folder at the provided folderPath. If the provided folder doesn't
 * exist, then undefined will be returned.
 * @param folderPath The path to the folder.
 * @returns The paths to the child entries of the folder at the provided folder path, or undefined
 * if the folder at the provided folder path doesn't exist.
 */
export function getChildEntryPaths(folderPath: string, options: GetChildEntriesOptions = {}): Promise<string[] | undefined> {
  return new Promise((resolve, reject) => {
    fs.readdir(folderPath, async (error: NodeJS.ErrnoException | null, entryNames: string[]) => {
      if (error) {
        if (error.code === "ENOENT" || error.code === "ENOTDIR") {
          resolve(undefined);
        } else {
          reject(error);
        }
      } else {
        const result: string[] = options.result || [];
        for (const entryName of entryNames) {
          const entryPath: string = joinPath(folderPath, entryName);
          try {
            if (await fileExists(entryPath)) {
              const addFile: boolean =
                (!options.condition || await Promise.resolve(options.condition(entryPath))) &&
                (!options.fileCondition || await Promise.resolve(options.fileCondition(entryPath)));
              if (addFile) {
                result.push(entryPath);
              }
            } else if (await folderExists(entryPath)) {
              const addFolder: boolean =
                (!options.condition || await Promise.resolve(options.condition(entryPath))) &&
                (!options.folderCondition || await Promise.resolve(options.folderCondition(entryPath)));
              if (addFolder) {
                result.push(entryPath);
              }

              if (options.recursive && (typeof options.recursive !== "function" || await Promise.resolve(options.recursive(entryPath)))) {
                options.result = result;
                await getChildEntryPaths(entryPath, options);
              }
            }
          } catch (error) {
            // If an error occurs while trying to get information about an entry, then just skip the
            // entry. It's most likely a permissions problem, which means we shouldn't be dealing
            // with that entry anyways.
          }
        }
        resolve(result);
      }
    });
  });
}

/**
 * Get the child folders of the folder at the provided folderPath. If the provided folder doesn't
 * exist, then undefined will be returned.
 * @param folderPath The path to the folder.
 * @returns The paths to the child folders of the folder at the provided folder path, or undefined
 * if the folder at the provided folder path doesn't exist.
 */
export async function getChildFolderPaths(folderPath: string, options: GetChildEntriesOptions = {}): Promise<string[] | undefined> {
  return getChildEntryPaths(folderPath, {
    ...options,
    fileCondition: () => false,
  });
}

/**
 * Get the child folders of the folder at the provided folderPath. If the provided folder doesn't
 * exist, then undefined will be returned.
 * @param folderPath The path to the folder.
 * @returns The paths to the child folders of the folder at the provided folder path, or undefined
 * if the folder at the provided folder path doesn't exist.
 */
export function getChildFilePaths(folderPath: string, options: GetChildEntriesOptions = {}): Promise<string[] | undefined> {
  return getChildEntryPaths(folderPath, {
    ...options,
    folderCondition: () => false,
  });
}

/**
 * Read the contents of the provided file.
 * @param filePath The path to the file to read.
 */
export function readFileContents(filePath: string): Promise<string | undefined> {
  return new Promise((resolve, reject) => {
    fs.readFile(filePath, { encoding: "utf8" }, (error: NodeJS.ErrnoException | null, content: string) => {
      if (error) {
        if (error.code === "ENOENT") {
          resolve(undefined);
        } else {
          reject(error);
        }
      } else {
        resolve(content);
      }
    });
  });
}

/**
 * Write the provided contents to the file at the provided filePath.
 * @param filePath The path to the file to write.
 * @param contents The contents to write to the file.
 */
export function writeFileContents(filePath: string, contents: string): Promise<void> {
  return new Promise((resolve, reject) => {
    fs.writeFile(filePath, contents, (error: NodeJS.ErrnoException | null) => {
      if (error) {
        reject(error);
      } else {
        resolve();
      }
    });
  });
}

export async function deleteEntry(path: string): Promise<boolean> {
  return await folderExists(path)
    ? await deleteFolder(path)
    : await deleteFile(path);
}

/**
 * Delete the file at the provided file path.
 * @param {string} filePath The path to the file to delete.
 */
export function deleteFile(filePath: string): Promise<boolean> {
  return new Promise((resolve, reject) => {
    fs.unlink(filePath, (error: NodeJS.ErrnoException | null) => {
      if (error) {
        if (error.code === "ENOENT") {
          resolve(false);
        } else {
          reject(error);
        }
      } else {
        resolve(true);
      }
    });
  });
}

/**
 * Delete each of the provided file paths.
 * @param filePaths The file paths that should be deleted.
 */
export async function deleteFiles(...filePaths: string[]): Promise<void> {
  if (filePaths && filePaths.length > 0) {
    for (const filePath of filePaths) {
      await deleteFile(filePath);
    }
  }
}

function _deleteFolder(folderPath: string): Promise<boolean> {
  return new Promise((resolve, reject) => {
    fs.rmdir(folderPath, (error: NodeJS.ErrnoException | null) => {
      if (error) {
        if (error.code === "ENOENT") {
          resolve(false);
        } else {
          reject(error);
        }
      } else {
        resolve(true);
      }
    });
  });
}

/**
 * Delete the folder at the provided folder path.
 * @param {string} folderPath The path to the folder to delete.
 */
export async function deleteFolder(folderPath: string): Promise<boolean> {
  let result: boolean | Error;
  try {
    result = await _deleteFolder(folderPath);
  } catch (deleteFolderError) {
    if (deleteFolderError.code === "ENOENT") {
      result = false;
    } else if (deleteFolderError.code !== "ENOTEMPTY") {
      result = deleteFolderError;
    } else {
      try {
        const childEntryPaths: string[] = (await getChildEntryPaths(folderPath))!;
        for (const childEntryPath of childEntryPaths) {
          await deleteEntry(childEntryPath);
        }
        try {
          result = await _deleteFolder(folderPath);
        } catch (deleteFolderError2) {
          result = deleteFolderError2;
        }
      } catch (deleteChildEntryError) {
        result = deleteChildEntryError;
      }
    }
  }
  return typeof result === "boolean"
    ? Promise.resolve(result)
    : Promise.reject(result);
}
