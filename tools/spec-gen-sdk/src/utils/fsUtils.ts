import path from 'path';
import fs from 'fs';
import { WorkflowContext } from '../automation/workflow';
import { SimpleGit } from 'simple-git';
import { TreeType, gitTreeResultToStringArray } from './gitUtils';
import { sdkAutomationCliConfig } from '../cli/config';

export type FsSearchOptions = {
  searchFileRegex: RegExp
} & ({
  rootFolder: string;
} | {
  repo: SimpleGit;
  treeId: string;
  specFolder: string,
});

export const searchRelatedFolder = async (
  filePath: string,
  opts: FsSearchOptions
) => {
  let searchPath = filePath;

  while (searchPath !== '.') {
    const fileName = path.basename(searchPath);
    if (opts.searchFileRegex.exec(fileName)) {
      return searchPath;
    }
    searchPath = path.dirname(searchPath);
  }
  return undefined;
};

export const searchSharedLibrary = async (
  fileList: string[],
  opts: FsSearchOptions
) => {
  const result: { [relatedFolder: string]: string[] } = {};
  fileList.sort();
  let lastFolder: string | undefined = undefined;

  for (const filePath of fileList) {
    if (lastFolder !== undefined && filePath.startsWith(lastFolder)) {
      result[lastFolder].push(filePath);
    }
    const relatedFolder = await searchRelatedFolder(filePath, opts);
    if (relatedFolder === undefined) {
      continue;
    }
    if (result[relatedFolder] === undefined) {
      result[relatedFolder] = [];
    }
    result[relatedFolder].push(filePath);
    lastFolder = relatedFolder;
  }

  return result;
};

export const searchRelatedTypeSpecProjectBySharedLibrary = async (
  sharedLibraries: { [relatedFolder: string]: string[] },
  opts: FsSearchOptions
) => {
  const result: { [relatedFolder: string]: string[] } = {};
  for (const sharedLibrary of Object.keys(sharedLibraries)) {
    const parentFolder = path.dirname(sharedLibrary);
    const fileNames = await getFilesInFolder(parentFolder, opts);
    for (const fileName of fileNames) {
      const filePath = path.join(parentFolder, fileName);
      const subFileNames = await getFilesInFolder(filePath, opts);
      for (const subFileName of subFileNames) {
        if (opts.searchFileRegex.exec(subFileName)) {
          if (!result[filePath]) {
            result[filePath] = [];
          }
          result[filePath] = result[filePath].concat(sharedLibraries[sharedLibrary]);
        }
      }
    }
  }
  return result;
};

export const searchRelatedParentFolders = async (
  fileList: string[],
  opts: FsSearchOptions
) => {
  const result: { [relatedFolder: string]: string[] } = {};
  fileList.sort();
  
  for (const filePath of fileList) {
    const relatedParentFolder = await searchRelatedParentFolder(filePath, opts);
    if (relatedParentFolder === undefined) {
      continue;
    }
    if (result[relatedParentFolder] === undefined) {
      result[relatedParentFolder] = [];
    }
    result[relatedParentFolder].push(filePath);
  }

  return result;
};

export const searchRelatedParentFolder = async (
  filePath: string,
  opts: FsSearchOptions
) => {
  let searchPath = filePath;

  while (searchPath !== '.') {
    const fileNames = await getFilesInFolder(searchPath, opts);
    for (const fileName of fileNames) {
      if (opts.searchFileRegex.exec(fileName)) {
        return searchPath;
      }
    }
    searchPath = path.dirname(searchPath);
  }

  return undefined;
};

const getFilesInFolder = async (searchPath: string, opts: FsSearchOptions): Promise<string[]> => {
  if ('rootFolder' in opts) {
    const p = path.join(opts.rootFolder, searchPath);
    if (!fs.existsSync(p) || !fs.lstatSync(p).isDirectory()) {
      return [];
    }
    return fs.readdirSync(p);
  }

  const workingFolder = sdkAutomationCliConfig.workingFolder;
  const workPath = path.resolve(process.cwd(), workingFolder, opts.specFolder, searchPath);
  const tree = await opts.repo.raw(['ls-tree', `${opts.treeId}`, workPath]);
  const subTree = gitTreeResultToStringArray(tree);
  if (subTree.length === 0 || (subTree.length > 0 && subTree[0].type !== TreeType.TREE)) {
    return [];
  }
  const entryPath = path.join(workPath, '/');
  const treeEntry = await opts.repo.raw(['ls-tree', `${opts.treeId}`, entryPath]);
  const subTreeEntry = gitTreeResultToStringArray(treeEntry);
  return subTreeEntry.map(item => item.file.slice(searchPath.length + 1));
};

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
    context.logger.warn(`Warning: File ${filePath} not found to read. Please re-run the pipeline if the error is transitient error or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
    return undefined;
  }

  try {
    context.logger.info(`Read temp file ${filePath} with content:`);
    const contentString = fs.readFileSync(filePath).toString();
    const content = JSON.parse(contentString);
    context.logger.info(JSON.stringify(content, undefined, 2));
    return content;
  } catch (e) {
    context.logger.error(`IOError: Failed to read ${fileName}: ${e.message}. Please re-run the pipeline if the error is retryable or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
    return undefined;
  }
};

export const deleteTmpJsonFile = (context: WorkflowContext, fileName: string) => {
  const filePath = path.join(context.tmpFolder, fileName);
  if (fs.existsSync(filePath)) {
    fs.unlinkSync(filePath);
  }
};
