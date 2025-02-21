import _ from "lodash";
import YAML from "yaml";
import { getTypeSpecOutputFolder } from "./typespecUtils";
import { WorkflowContext } from "../automation/workflow";

export function diffStringArrays(left: string[], right: string[]): { diffResult: string[], hasDiff: boolean} {
  const unchanged = right.filter(item => left.includes(item));
  const added = right.filter(item => !left.includes(item));
  const diffResult: string[] = [];

  unchanged.forEach(item => {
    diffResult.push(`\t${item}`);
  });

  added.forEach(item => {
    diffResult.push(`+\t${item}`);
  });

  return {
    diffResult,
    hasDiff: added.length > 0
  };
  
}

export function getValueByKey<T>(
    arrayOfObjects: { [key: string]: T }[],
    targetKey: string
): T | undefined {
    const foundObject = _.find(arrayOfObjects, targetKey);
    return foundObject ? foundObject[targetKey] : undefined;
}

export function removeAnsiEscapeCodes<T extends string | string[]>(messages: T): T {
    // eslint-disable-next-line no-control-regex
    const ansiEscapeCodeRegex = /\x1b\[(\d{1,2}(;\d{0,2})*)?[A-HJKSTfimnsu]/g;
    if (typeof messages === "string") {
        return messages.replace(ansiEscapeCodeRegex, "") as T;
    }
    return messages.map((item) => item.replace(ansiEscapeCodeRegex, "")) as T;
}

export function extractServiceName(path: string): string {
    const match = path.match(/specification\/([^/]*)\//);
    return match ? match[1] : "";
}

// Flag of the readme.md under root of 'resource-manager' or 'data-plane'
const IsReadmeUnderRoot = /specification\/([^/]*)\/([^/]*)\/readme\.md/g;

export function removeDuplicatesFromRelatedFiles(
    relatedTypeSpecProjectFolder: string[] | undefined,
    relatedReadmeMdFiles: string[] | undefined,
    context: WorkflowContext
): string[] {
    const _relatedTypeSpecProjectFolder = relatedTypeSpecProjectFolder || [];
    const _relatedReadmeMdFiles = relatedReadmeMdFiles || [];
    const filteredReadmeMdFiles = _relatedReadmeMdFiles.filter((readmeFile) => {
        const readmeServiceName = extractServiceName(readmeFile);
        const isResourceManager = readmeFile.includes("/resource-manager/");
        const isDataPlane = readmeFile.includes("/data-plane/");
        let typespecOutputFolderRegExp: RegExp;

        return !_relatedTypeSpecProjectFolder.some((typespecFile) => {
            const typespecServiceName = extractServiceName(typespecFile);
            const isManagement = typespecFile.endsWith(".Management");

            if (readmeServiceName === typespecServiceName) {
                if (isManagement && isResourceManager) {
                    context.logger.info(
                        `Exclude the readmeFile from SDK generation: ${readmeFile} because it is duplicated with typespec: ${typespecFile}.`
                    );
                    return true;
                }

                if (isDataPlane && IsReadmeUnderRoot.test(readmeFile)) {
                    context.logger.info(
                        `Exclude the readmeFile from SDK generation: ${readmeFile} because it is under data-plane root and is duplicated with typespec: ${typespecFile}.`
                    );
                    return true;
                }

                const typespecOutputFolder = getTypeSpecOutputFolder(
                    typespecFile,
                    context
                );
                typespecOutputFolderRegExp =
                    typespecOutputFolderRegExp ||
                    new RegExp(typespecOutputFolder, "ig");
                if (
                    isDataPlane &&
                    typespecOutputFolderRegExp.test(readmeFile)
                ) {
                    context.logger.info(
                        `Exclude the readmeFile from SDK generation: ${readmeFile} because it is duplicated with typespec: ${typespecFile}, typespecOutputFolder: ${typespecOutputFolder}.`
                    );
                    return true;
                }
            }
            return false;
        });
    });

    return filteredReadmeMdFiles;
}

/**
 * @param yamlContent
 * @returns {result: string | object | undefined | null, message: string}
 * special return
 * if the content is empty, return {result: null, message: string
 * if the file parse error, return {result: undefined, message: string
 */
export function parseYamlContent(
    yamlContent: string,
    path: string
): {
    result: string | object | undefined | null;
    message: string;
} {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let content: any = undefined;
    // if yaml file is not a valid yaml, catch error and return undefined
    try {
        content = YAML.parse(yamlContent);
    } catch (error) {
        console.error(
            `The file parsing failed in the ${path}. Details: ${error}`
        );
        return {
            result: content,
            message: `The file parsing failed in the ${path}. Details: ${error}`,
        };
    }

    // if yaml file is empty, run yaml.safeload success but get undefined
    // to identify whether it is empty return null to distinguish.
    if (!content) {
        console.info(
            `The file in the ${path} has been successfully parsed, but it is an empty file.`
        );
        return {
            result: null,
            message: `The file in the ${path} has been successfully parsed, but it is an empty file.`,
        };
    }

    return {
        result: content,
        message: "The file has been successfully parsed.",
    };
}

/**
 * Replace all of the instances of searchValue in value with the provided replaceValue.
 * @param {string | undefined} value The value to search and replace in.
 * @param {string} searchValue The value to search for in the value argument.
 * @param {string} replaceValue The value to replace searchValue with in the value argument.
 * @returns {string | undefined} The value where each instance of searchValue was replaced with replacedValue.
 */
export function replaceAll(value: string | undefined, searchValue: string, replaceValue: string): string | undefined {
  return !value || !searchValue ? value : value.split(searchValue).join(replaceValue || "");
}

/**
 * Extract and format the prefix from tspConfigPath or readmePath.
 * @param {string | undefined} tspConfigPath The tspConfigPath to extract the prefix from.
 * @param {string | undefined} readmePath The readmePath to extract the prefix from.
 * @returns {string} The formatted prefix.
 */
export function extractPathFromSpecConfig(tspConfigPath: string | undefined, readmePath: string | undefined): string {
  let prefix = '';
  if (tspConfigPath) {
    const match = tspConfigPath.match(/specification\/(.+)\/tspconfig\.yaml$/);
    if (match) {
      const segments = match[1].split('/');
      prefix = segments.join('-').toLowerCase().replace(/\./g, '-');
    }
  } else if (readmePath) {
    const match = readmePath.match(/specification\/(.+?)\/readme\.md$/i);
    if (match) {
      const segments = match[1].split('/');
      prefix = segments.join('-').toLowerCase().replace(/\./g, '-');
    }
  }
  return prefix;
}
