import { BlobStorageBlockBlob, Command, getPathName, parseCommands, run } from '@ts-common/azure-js-dev-tools';
import { PackageCommandOptions } from './packageCommandOptions';
import { createRepositoryCommandOptions } from './repositoryCommand';
import { RepositoryCommandOptions } from './repositoryCommandOptions';
import { trimNewLine, errorToLog } from '../sdkAutomation';
import { replaceCommandVariables, SDKRepository } from '../sdkRepository';
import { SDKRepositoryPackage } from './sdkRepositoryPackage';

export type PackageCommand =
  | ((options: PackageCommandOptions) => void | string | string[] | Promise<void | string | string[]>)
  | string;

export function createPackageCommandOptions(
  repository: SDKRepository,
  changedPackage: SDKRepositoryPackage
): PackageCommandOptions {
  const rootedPackageFolderPath: string = changedPackage.getRootedPackageFolderPath();
  const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
    repository,
    changedPackage.repositoryFolderPath
  );
  const buildID = repository.buildID;
  const packageIndex = changedPackage.packageIndex;
  return {
    ...repositoryCommandOptions,
    relativePackageFolderPath: changedPackage.data.relativeFolderPath,
    rootedPackageFolderPath,
    logger: changedPackage.logger,
    changedFilePaths: changedPackage.data.changedFilePaths,
    executionFolderPath: rootedPackageFolderPath,
    captureOutput: (text: string) => changedPackage.logger.logInfo(trimNewLine(text)),
    captureError: (text: string) => changedPackage.logger.logError(trimNewLine(text)),
    capturePrefix: 'PkgCmd',
    throwOnError: true,
    log: (text: string) => changedPackage.logger.logInfo(trimNewLine(text)),
    packageData: changedPackage.data,
    buildID,
    packageIndex
  };
}

export async function runPackageCommands(
  changedPackage: SDKRepositoryPackage,
  packageCommands: PackageCommand | PackageCommand[],
  packageCommandOptions: PackageCommandOptions,
  isPrivatePackage?: boolean
): Promise<boolean> {
  let result = true;
  const packageCommandsArray: PackageCommand[] = Array.isArray(packageCommands) ? packageCommands : [packageCommands];
  try {
    for (const packageCommand of packageCommandsArray) {
      if (typeof packageCommand === 'string') {
        const parsedPackageCommands: Command[] = parseCommands(packageCommand);
        for (const parsedPackageCommand of parsedPackageCommands) {
          replaceCommandVariables(parsedPackageCommand, packageCommandOptions, changedPackage.logger);
          await run(parsedPackageCommand, undefined, {
            ...packageCommandOptions,
            capturePrefix: getPathName(parsedPackageCommand.executable)
          });
        }
      } else {
        const packageCommandResult: void | string | string[] = await Promise.resolve(
          packageCommand(packageCommandOptions)
        );
        if (packageCommandResult) {
          changedPackage.artifactFilePaths.push(
            ...(Array.isArray(packageCommandResult) ? packageCommandResult : [packageCommandResult])
          );
        }
      }
    }
  } catch (error) {
    await changedPackage.logger.logError(`Failed to create the package ${changedPackage.data.name}.`);
    await changedPackage.logger.logError(errorToLog(error, false));
    changedPackage.data.status = 'failed';
    result = false;
  }

  if (!changedPackage.data.artifactBlobUrls) {
    changedPackage.data.artifactBlobUrls = [];
  }
  if (changedPackage.artifactFilePaths.length > 0) {
    const artifactBlobUrls: string[] = changedPackage.data.artifactBlobUrls;

    for (const artifactFilePath of changedPackage.artifactFilePaths) {
      if (isPrivatePackage) {
        changedPackage.data.isPrivatePackage = true;
        artifactBlobUrls.push(artifactFilePath);
      } else {
        const artifactFileName: string = getPathName(artifactFilePath);

        const artifactFileBlob: BlobStorageBlockBlob = changedPackage.packagePrefix.getBlockBlob(
          artifactFileName
        );
        const artifactFileBlobUrl: string = changedPackage.context.getBlobProxyUrl(artifactFileBlob);
        await changedPackage.logger.logSection(`Uploading ${artifactFilePath} to ${artifactFileBlobUrl}...`);
        await artifactFileBlob.setContentsFromFile(artifactFilePath);
        await changedPackage.logger.logInfo(`Done uploading ${artifactFilePath} to ${artifactFileBlobUrl}.`);
        artifactBlobUrls.push(artifactFileBlobUrl);
        changedPackage.data.isPrivatePackage = false;
      }
    }

    await changedPackage.context.writeGenerationData();
  }

  return result;
}
