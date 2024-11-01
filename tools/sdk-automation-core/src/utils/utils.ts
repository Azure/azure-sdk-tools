
import { SequenceMatcher } from 'difflib';
import _ from 'lodash';
import { getTypeSpecOutputFolder } from './typespecUtils';
import { WorkflowContext } from '../automation/workflow';

export function diffStringArrays(left: string[], right: string[]): {
  diffResult: string[],
  hasDiff: boolean
} {
  left.sort();
  right.sort();
  // tslint:disable-next-line: no-null-keyword
  const matcher = new SequenceMatcher(null, left, right);
  const diffResult: string[] = [];
  let hasDiff = false;
  for (const [op, i0, i1, j0, j1] of matcher.getOpcodes()) {
    if (op === 'equal') {
      for (let x = i0; x < i1; ++x) {
        diffResult.push(`\t${left[x]}`);
      }
    } else {
      if (op === "replace" || op === "insert") {
        hasDiff = true;
        for (let x = j0; x < j1; ++x) {
          diffResult.push(`+\t${right[x]}`);
        }
      }
    }
  }
  return {
    diffResult,
    hasDiff
  };
}

export function getValueByKey<T>(arrayOfObjects: { [key: string]: T }[], targetKey: string): T | undefined {
  const foundObject = _.find(arrayOfObjects, targetKey);
  return foundObject ? foundObject[targetKey] : undefined;
}

export function removeAnsiEscapeCodes(messages: string[] | string): string[] | string {
  const ansiEscapeCodeRegex = /\x1b\[(\d{1,2}(;\d{0,2})*)?[A-HJKSTfimnsu]/g;
  if (typeof messages === 'string') {
    return messages.replace(ansiEscapeCodeRegex, '');
  }
  return messages.map(item => item.replace(ansiEscapeCodeRegex, ''));
}

export function extractServiceName(path: string): string {
  const match = path.match(/specification\/([^\/]*)\//);
  return match ? match[1] : '';
}

// Flag of the readme.md under root of 'resource-manager' or 'data-plane'
const IsReadmeUnderRoot = /specification\/([^\/]*)\/([^\/]*)\/readme\.md/g;

export function removeDuplicatesFromRelatedFiles(relatedTypeSpecProjectFolder: string[] | undefined, relatedReadmeMdFiles: string[] | undefined, context: WorkflowContext): string[] {
  const _relatedTypeSpecProjectFolder = relatedTypeSpecProjectFolder || [];
  const _relatedReadmeMdFiles = relatedReadmeMdFiles || [];
  const filteredReadmeMdFiles = _relatedReadmeMdFiles.filter(readmeFile => {
    const readmeServiceName = extractServiceName(readmeFile);
    const isResourceManager = readmeFile.includes('/resource-manager/');
    const isDataPlane = readmeFile.includes('/data-plane/');
    let typespecOutputFolderRegExp: RegExp;

    return !_relatedTypeSpecProjectFolder.some(typespecFile => {
      const typespecServiceName = extractServiceName(typespecFile);
      const isManagement = typespecFile.endsWith('.Management');

      if (readmeServiceName === typespecServiceName) {
        if (isManagement && isResourceManager) {
          context.logger.info(`Exclude the readmeFile from SDK generation: ${readmeFile} because it is duplicated with typespec: ${typespecFile}.`);
          return true;
        }

        if (isDataPlane && IsReadmeUnderRoot.test(readmeFile)) {
          context.logger.info(`Exclude the readmeFile from SDK generation: ${readmeFile} because it is under data-plane root and is duplicated with typespec: ${typespecFile}.`);
          return true;
        }

        const typespecOutputFolder = getTypeSpecOutputFolder(typespecFile, context);
        typespecOutputFolderRegExp = typespecOutputFolderRegExp || new RegExp(typespecOutputFolder, 'ig');
        if (isDataPlane && typespecOutputFolderRegExp.test(readmeFile)) {
          context.logger.info(`Exclude the readmeFile from SDK generation: ${readmeFile} because it is duplicated with typespec: ${typespecFile}, typespecOutputFolder: ${typespecOutputFolder}.`);
          return true;
        }
      }
      return false;
    });
  });

  return filteredReadmeMdFiles;
}
