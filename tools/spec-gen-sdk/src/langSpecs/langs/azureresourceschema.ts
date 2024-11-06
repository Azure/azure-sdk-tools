import path from 'path';
import { ReadmeMdFileProcessMod, LanguageConfiguration } from '../languageConfiguration';
import { toArray } from '@ts-common/azure-js-dev-tools';
import { Logger } from '@azure/logger-js';

/**
 * A language configuration for AzureResourceSchema.
 */
export const azureresourceschema: LanguageConfiguration = {
  name: 'AzureResourceSchema',
  generatorPackageName: '@microsoft.azure/autorest.azureresourceschema',
  aliases: ['azure-resource-manager-schemas'],
  packageRootFileName: 'common',
  readmeMdFileProcessMod: ReadmeMdFileProcessMod.Sequencial,
  keepReleasePROpen: true,
  packageNameCreator: getPackageName,
  RunnerReportLoggerCreator: getRunnerReportLogger
};

/**
 * Get the name of the package from the package.json file found in the package folder.
 * @param rootedRepositoryFolderPath The rooted path to the repository folder.
 * @param relativePackageFolderPath The path to the package folder relative to the repository folder.
 */
export function getPackageName(rootedRepositoryFolderPath: string,
  relativePackageFolderPath: string, readmeMdFileUrl: string): string {
  return path.basename(path.dirname(path.dirname(readmeMdFileUrl)));
}

export function getRunnerReportLogger(output: string[]): Logger {
  const logFn = (text: string | string[]) => {
    if (text instanceof Array && (!text.join('.').includes('node')
    || text.join('.').includes('PostProcessor'))) {
      output.push(...toArray(text));
    }
    return Promise.resolve();
  };

  const logRes = (text: string | string[]) => {
    if (text instanceof Array && (text.join('.').includes('PostProcessor')
    || text.join('.').includes('passing') || text.join('.').includes('failing'))) {
      output.push(...toArray(text));
    }
    return Promise.resolve();
  };

  const dummyFn = () => Promise.resolve();

  return {
    logError: logFn,
    logWarning: logFn,
    logInfo: logRes,
    logSection: dummyFn,
    logVerbose: dummyFn
  };
}
