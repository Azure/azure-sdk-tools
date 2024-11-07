import {
    commandToString,
    Command,
    parseCommands,
    run,
    findEntryInPath,
    getParentFolderPath,
    getName
  } from '@ts-common/azure-js-dev-tools';
import path from 'path';
import { LanguageConfiguration } from '../languageConfiguration';
import { PackageCommandOptions } from '../packageCommandOptions';
import { InstallationInstructionsOptions } from '../installationInstructions';
import {
    SDKRepository,
    resolveEnvironmentVariables,
    replaceCommandVariables
  } from '../../sdkRepository';
import { getConfigMeta } from '../../swaggerToSDKConfiguration';
import { createRepositoryCommandOptions } from '../repositoryCommand';
import { SDKRepositoryPackage } from '../sdkRepositoryPackage';
import { existsSync } from 'fs';
import { Logger } from '@azure/logger-js';

/**
  * A language configuration for Trenton.
  */
export const trenton: LanguageConfiguration = {
  name: 'Trenton',
  generatorPackageName: '@microsoft.azure/autorest.trenton',
  packageRootFileName: 'registration.go',
  isPrivatePackage: true,
  runLangAfterScripts : runAfterScriptsInRepoWithService,
  getExtraRelativeFolderPaths,
  packageNameCreator: getPackageName,
  packageCommands: createPackage,
  liteInstallationInstruction: genLiteInstruction
  // installationInstructions: getInstallationInstructions
};

/**
 * Get the name of the package from the relative package folder path.
 * @param rootedRepositoryFolderPath The rooted path to the repository folder.
 * @param relativePackageFolderPath The path to the package folder relative to the repository folder.
 */
function getPackageName(_rootedRepositoryFolderPath: string, relativePackageFolderPath: string): string {
  return getName(relativePackageFolderPath);
}

/**
 * Function that run specific script for Lang scope.
 * For Terraform, it mainly focuses on compile terraform code and modify files.
 * @param sdkRepo The context in which the scripts should run.
 * @param changedPackageName Used to determine the service name.
 */
async function runAfterScriptsInRepoWithService(
  sdkRepo: SDKRepository, changedPackage: SDKRepositoryPackage
): Promise<boolean> {
  const changedPackageName = changedPackage.data.name;
  const service = changedPackageName.split('/').slice(-1)[0];
  const scripts = getConfigMeta(sdkRepo.swaggerToSDKConfiguration, 'after_scripts_in_repo_with_service') || [];
  if (scripts.length === 0) {
    return true;
  }

  const commandOptions = createRepositoryCommandOptions(sdkRepo, sdkRepo.repoPath);
  const envConfig = getConfigMeta(sdkRepo.swaggerToSDKConfiguration, 'envs') || {};
  const envs = resolveEnvironmentVariables(envConfig, sdkRepo.repoPath);

  const commands: Command[] = [];
  scripts.forEach(script => commands.push(...parseCommands(script)));
  for (const command of commands) {
    command.args?.push(service);
    replaceCommandVariables(command, commandOptions, changedPackage.logger);
    try {
      await run(command, undefined, {
        ...commandOptions,
        runner: sdkRepo.context.runner,
        executionFolderPath: sdkRepo.repoPath,
        showCommand: true,
        showResult: true,
        log: sdkRepo.logger.logInfo,
        environmentVariables: envs,
        capturePrefix: `after_scripts_in_repo|${command.executable}`,
        throwOnError: true
      });

    } catch (e) {
      await sdkRepo.logger.logError(`Fail to run sdk specified script: ${commandToString(command)}`);
      sdkRepo.data.status = 'warning';
      return false;
    }
  }
  await sdkRepo.logger.logInfo(`Run sdk specified script successfully for ${changedPackageName}`);

  return true;
}

async function getExtraRelativeFolderPaths(
  relativeFolderPath: string, fileChanged: string[], logger: Logger
): Promise<string[]> {
  const result: string[] = [];

  for (const filePath of fileChanged) {
    const packageRootFolderFile =
      await findEntryInPath('client.go', filePath, (p) => existsSync(p));
    if (packageRootFolderFile === undefined) {
      continue;
    }
    const packageRootFolder = getParentFolderPath(packageRootFolderFile);
    if (packageRootFolder !== relativeFolderPath
      && !result.includes(packageRootFolder)
    ) {
      await logger.logInfo(`Add ${packageRootFolder} to package extra folders`);
      result.push(packageRootFolder);
    }
  }

  return result;
}

export async function createPackage(options: PackageCommandOptions): Promise<string> {
  const repoDir = options.repositoryFolderPath;
  const packageFolder = path.join(repoDir, 'tmp_gopath', 'bin');
  const packageName = 'terraform-provider-azurerm';
  const version = `0.${options.buildID}.${options.packageIndex}`;
  const result = await run(
    'sh',
    [
      './scripts/package.sh',
      packageFolder,
      packageName,
      version,
      options.rootedPackageFolderPath
    ],
    {
      ...options,
      executionFolderPath: options.repositoryFolderPath,
      capturePrefix: 'build_package'
    }
  );
  if (result.error?.message === undefined) {
    return `${packageName}:${version}`;
  }
  return undefined!;
}

export function genLiteInstruction(options: InstallationInstructionsOptions): string[] {
  const result: string[] = [ `` ];
  for (const artifactUrl of options.artifactUrls) {
    const packageinfos = artifactUrl.split(':');
    const packageName = packageinfos[0];
    const version = packageinfos[1];
    const ins = `az artifacts universal download \
        --organization "https://dev.azure.com/azure-sdk/" \
        --project "internal" \
        --scope project \
        --feed "sdk-automation-test" \
        --name ${packageName} \
        --version ${version} \
        --path .`;
    result.push(ins);
  }
  result.push(``);
  return result;

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
