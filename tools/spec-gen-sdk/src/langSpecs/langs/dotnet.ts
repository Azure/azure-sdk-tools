import {
  getChildFilePaths,
  getParentFolderPath,
  getPathName,
  run,
  RunOptions,
  RunResult
} from '@ts-common/azure-js-dev-tools';
import { InstallationInstructionsOptions } from '../installationInstructions';
import { LanguageConfiguration } from '../languageConfiguration';
import { PackageCommandOptions } from '../packageCommandOptions';

/**
 * A language configuration for C#.
 */
export const dotnet: LanguageConfiguration = {
  name: '.NET',
  generatorPackageName: '@microsoft.azure/autorest.csharp',
  aliases: ['C#', 'CSharp', 'C-Sharp', 'CS', 'NET', 'DotNET', 'Dot-NET'],
  packageRootFileName: /.*\.sln|^src$/,
  packageNameCreator: getPackageName,
  afterGenerationCommands: createNugetPackage,
  packageCommands: findPackage,
  installationInstructions: getInstallationInstructions
};

/**
 * Get the name of the package from the relative package folder path.
 * @param rootedRepositoryFolderPath The rooted path to the repository folder.
 * @param relativePackageFolderPath The path to the package folder relative to the repository folder.
 */
export function getPackageName(_rootedRepositoryFolderPath: string, relativePackageFolderPath: string): string {
  return getPathName(relativePackageFolderPath);
}

export async function createNugetPackage(options: PackageCommandOptions): Promise<void> {
  const runOptions: RunOptions = {
    ...options,
    capturePrefix: 'MSBuild',
    executionFolderPath: options.repositoryFolderPath
  };
  const scope: string = getPathName(getParentFolderPath(options.relativePackageFolderPath));
  const dotnetResult: RunResult = await run(
    'dotnet',
    ['msbuild', 'build.proj', '/t:CreateNugetPackage', `/p:Scope=${scope}`, '/v:n', '/p:SkipTests=true'],
    runOptions
  );
  if (dotnetResult.error) {
    await options.logger.logError(dotnetResult.error.toString());
  }
}

export async function findPackage(options: PackageCommandOptions): Promise<string[]> {
  const packagePrefix: string = getPathName(options.relativePackageFolderPath);
  const packageSuffix = 'nupkg';
  await options.logger.logInfo(
    `Looking for files that match "${packagePrefix}*${packageSuffix}" in "${options.repositoryFolderPath}...`
  );
  return (
    (await getChildFilePaths(options.repositoryFolderPath, {
      recursive: true,
      fileCondition: (filePath: string) => {
        const fileName: string = getPathName(filePath);
        return fileName.startsWith(packagePrefix) && fileName.endsWith(packageSuffix);
      }
    })) || []
  );
}

/**
 * Get installation instructions for a package using the provided options.
 * @param options The options that can be used when generating installation instructions.
 */
export function getInstallationInstructions(options: InstallationInstructionsOptions): string[] {
  const localFeedsUrl = `https://docs.microsoft.com/en-us/nuget/hosting-packages/local-feeds`;
  return [
    `## Installation Instructions`,
    `In order to use the [generated nuget package](${options.artifactUrls[0]}) in your app, \
    you will have to use it from a private feed.`,
    `To create a private feed, see the following link:`,
    `[${localFeedsUrl}](${localFeedsUrl})`,
    `This will allow you to create a new local feed and add the location of the new feed as one of the sources.`,
    `## Direct Download`,
    `The generated package can be directly downloaded from here:`,
    `- [${options.packageName}](${options.artifactUrls[0]})`
  ];
}
