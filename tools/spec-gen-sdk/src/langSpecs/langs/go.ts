import {
  copyFolder,
  createFolder,
  deleteFolder,
  joinPath,
  replaceAll,
  where,
  pathRelativeTo,
  copyFile
} from '@ts-common/azure-js-dev-tools';
import { LanguageConfiguration } from '../languageConfiguration';
import { PackageCommandOptions } from '../packageCommandOptions';
import { InstallationInstructionsOptions } from '../installationInstructions';

/**
 * A language configuration for Go.
 */
export const go: LanguageConfiguration = {
  name: 'Go',
  generatorPackageName: '@microsoft.azure/autorest.go',
  packageRootFileName: 'client.go',
  packageNameCreator: getPackageName,
  packageCommands: createPackage,
  installationInstructions: getInstallationInstructions
};

/**
 * Get the name of the package from the relative package folder path.
 * @param rootedRepositoryFolderPath The rooted path to the repository folder.
 * @param relativePackageFolderPath The path to the package folder relative to the repository folder.
 */
function getPackageName(_rootedRepositoryFolderPath: string, relativePackageFolderPath: string): string {
  const firstSlashIndex: number = relativePackageFolderPath.indexOf('/');
  const lastSlashIndex: number = relativePackageFolderPath.lastIndexOf('/');
  return relativePackageFolderPath.substring(firstSlashIndex + 1, lastSlashIndex);
}

async function createPackage(options: PackageCommandOptions): Promise<string> {
  let zipFilePath: string;
  const zipFolderPath: string = await createGoZipFolder(options);
  try {
    await copyGoProfilesToZipFolder(options, zipFolderPath);
    await copyGoPackageToZipFolder(options, zipFolderPath);
    zipFilePath = await createGoPackageZipFile(options, zipFolderPath);
  } finally {
    await options.logger.logSection(`Deleting ${zipFolderPath}...`);
    await deleteFolder(zipFolderPath);
  }
  return zipFilePath;
}

/**
 * Create the zip folder that the Go package zip file will be created from.
 * @param options The package command options.
 */
async function createGoZipFolder(options: PackageCommandOptions): Promise<string> {
  const zipFolderPath: string = joinPath(options.repositoryFolderPath, 'zip');
  await options.logger.logSection(`Creating folder ${zipFolderPath}...`);
  await createFolder(zipFolderPath);
  return zipFolderPath;
}

/**
 * Copy the Go package source code folder to the zip folder.
 * @param options The package command options.
 * @param zipFolderPath The path to the zip folder.
 */
async function copyGoPackageToZipFolder(options: PackageCommandOptions, zipFolderPath: string): Promise<void> {
  const zipPackageFolderPath: string = joinPath(zipFolderPath, options.relativePackageFolderPath);
  await options.logger.logSection(`Copying ${options.rootedPackageFolderPath} to ${zipPackageFolderPath}...`);
  await copyFolder(options.rootedPackageFolderPath, zipPackageFolderPath);
}

/**
 * Copy the Go profiles folder to the zip folder.
 * @param options The package command options.
 * @param zipFolderPath The path to the zip folder.
 */
async function copyGoProfilesToZipFolder(options: PackageCommandOptions, zipFolderPath: string): Promise<void> {
  const profilesFolderPath: string = joinPath(options.repositoryFolderPath, 'profiles');
  const zipProfilesFolderPath: string = joinPath(zipFolderPath, 'profiles');
  const changedProfilesFilePaths: string[] = where(options.changedFilePaths, (changedFilePath: string) =>
    changedFilePath.startsWith(profilesFolderPath)
  );
  if (changedProfilesFilePaths.length === 0) {
    await options.logger.logSection(
      `Not copying anything from the profiles folder since no profile files were changed.`
    );
  } else {
    await options.logger.logSection(
      `Copying ${changedProfilesFilePaths.length} changed files ` +
        `from ${profilesFolderPath} to ${zipProfilesFolderPath}...`
    );
    let copyNumber = 0;
    for (const changedProfilesFilePath of changedProfilesFilePaths) {
      const changedProfilesRelativeFilePath: string = pathRelativeTo(changedProfilesFilePath, profilesFolderPath);
      const targetFilePath: string = joinPath(zipProfilesFolderPath, changedProfilesRelativeFilePath);
      await options.logger.logInfo(`${++copyNumber}. Copying ${changedProfilesFilePath} to ${targetFilePath}...`);
      await copyFile(changedProfilesFilePath, targetFilePath, true);
    }
  }
}

/**
 * Create the package zip file from the zip folder.
 * @param options The package command options.
 * @param zipFolderPath The path to the zip folder.
 */
async function createGoPackageZipFile(options: PackageCommandOptions, zipFolderPath: string): Promise<string> {
  const packageName: string = getPackageName(options.repositoryFolderPath, options.relativePackageFolderPath);
  const packageZipFilePath: string = joinPath(
    options.rootedPackageFolderPath,
    `${replaceAll(packageName, '/', '.')}.zip`
  );
  await options.logger.logSection(`Compressing ${zipFolderPath} to ${packageZipFilePath}...`);
  await options.compressor.zipFolder(zipFolderPath, packageZipFilePath);
  return packageZipFilePath;
}

/**
 * Get installation instructions for a package using the provided options.
 * @param options The options that can be used when generating installation instructions.
 */
export function getInstallationInstructions(options: InstallationInstructionsOptions): string[] {
  return [
    `## Installation Instructions`,
    `You can install the package \`${options.packageName}\` of this PR ` +
      `by downloading the [package](${options.artifactUrls}) and extracting it ` +
      `to the root of your azure-sdk-for-go directory.`,
    `## Direct Download`,
    `The generated package can be directly downloaded from here:`,
    `- [${options.packageName}](${options.artifactUrls})`
  ];
}
