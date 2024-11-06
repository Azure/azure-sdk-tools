import {
  getChildFilePaths,
  getPathName,
  joinPath,
  PackageJson,
  readPackageJsonFileSync,
  npm
} from '@ts-common/azure-js-dev-tools';
import { InstallationInstructionsOptions } from '../installationInstructions';
import { LanguageConfiguration } from '../languageConfiguration';
import { PackageCommandOptions } from '../packageCommandOptions';

/**
 * A language configuration for JavaScript-based languages.
 */
export const javascript: LanguageConfiguration = {
  name: 'JavaScript',
  generatorPackageName: '@microsoft.azure/autorest.typescript',
  aliases: ['TypeScript', 'TS', 'JS', 'Node', 'Nodejs', 'Node.js'],
  packageRootFileName: 'package.json',
  packageNameCreator: getPackageName,
  packageCommands: createPackage,
  installationInstructions: getInstallationInstructions
};

/**
 * Get the name of the package from the package.json file found in the package folder.
 * @param rootedRepositoryFolderPath The rooted path to the repository folder.
 * @param relativePackageFolderPath The path to the package folder relative to the repository folder.
 */
export function getPackageName(rootedRepositoryFolderPath: string, relativePackageFolderPath: string): string {
  let packageName: string;
  const packageJsonFilePath: string = joinPath(rootedRepositoryFolderPath, relativePackageFolderPath, 'package.json');
  try {
    const packageJson: PackageJson = readPackageJsonFileSync(packageJsonFilePath);
    packageName = packageJson.name || relativePackageFolderPath;
  } catch (error) {
    packageName = relativePackageFolderPath;
  }
  return packageName;
}

/**
 * Create the package file for a package folder.
 * @param options The options for creating the package.
 */
export async function createPackage(options: PackageCommandOptions): Promise<string[]> {
  await npm(['pack'], {
    ...options,
    executionFolderPath: options.rootedPackageFolderPath,
    capturePrefix: 'npmPack'
  });
  return (await getChildFilePaths(options.rootedPackageFolderPath, {
    fileCondition: (filePath: string) => filePath.endsWith('.tgz')
  }))!;
}

/**
 * Get installation instructions for a package using the provided options.
 * @param options The options that can be used when generating installation instructions.
 */
export function getInstallationInstructions(options: InstallationInstructionsOptions): string[] {
  const result: string[] = [
    `## Installation Instructions`,
    `You can install the package \`${options.packageName}\` of this PR using the following command:`,
    `\`\`\`bash`
  ]
    .concat(
      options.artifactUrls.length === 0
        ? []
        : options.package.context.isPublic
        ? [`npm install ${options.artifactUrls[0]}`]
        : [
            options.artifactDownloadCommand(options.artifactUrls[0], getPathName(options.artifactUrls[0])),
            `npm install ${getPathName(options.artifactUrls[0])}`
          ]
    )
    .concat([`\`\`\``, `## Direct Download`, `The generated package can be directly downloaded from here:`]);
  for (const artifactUrl of options.artifactUrls) {
    result.push(`- [${getPathName(artifactUrl)}](${artifactUrl})`);
  }
  return result;
}
