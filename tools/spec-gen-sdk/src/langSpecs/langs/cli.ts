import { getChildFilePaths, getPathName, run, getName } from '@ts-common/azure-js-dev-tools';
import { InstallationInstructionsOptions } from '../installationInstructions';
import { LanguageConfiguration } from '../languageConfiguration';
import { PackageCommandOptions } from '../packageCommandOptions';

/**
 * A language configuration for Python.
 */
export const cli: LanguageConfiguration = {
  name: 'Cli',
  generatorPackageName: 'autorest.cli',
  packageRootFileName: /^setup.cfg$/,
  packageNameCreator: getPackageName,
  packageCommands: createPackage,
  installationInstructions: getInstallationInstructions,
  liteInstallationInstruction: genLiteInstruction
};

/**
 * Get the name of the package from the relative package folder path.
 * @param rootedRepositoryFolderPath The rooted path to the repository folder.
 * @param relativePackageFolderPath The path to the package folder relative to the repository folder.
 */
export function getPackageName(_rootedRepositoryFolderPath: string, relativePackageFolderPath: string): string {
  return getPathName(relativePackageFolderPath);
}

export function genLiteInstruction(options: InstallationInstructionsOptions): string[] {
  let wheelFile: string = '';
  for (const artifactUrl of options.artifactUrls) {
    if (artifactUrl.endsWith('whl')) {
      wheelFile = artifactUrl;
    }
  }
  const result: string[] = options.package.context.isPublic
    ? [`az extension add --source=${wheelFile}`]
    : [
        options.artifactDownloadCommand(wheelFile, getName(wheelFile)),
        `az extension add --source=${getName(wheelFile)}`
      ];
  result.push(``);
  return result;
}

export async function createPackage(options: PackageCommandOptions): Promise<string[]> {
  await run(
    'python',
    [
      './scripts/automation/build_package.py',
      '--dest',
      options.rootedPackageFolderPath,
      options.relativePackageFolderPath
    ],
    {
      ...options,
      executionFolderPath: options.repositoryFolderPath,
      capturePrefix: 'build_package'
    }
  );
  return (await getChildFilePaths(options.rootedPackageFolderPath, {
    fileCondition: (filePath: string) => filePath.endsWith('.whl') || filePath.endsWith('.zip'),
    recursive: false
  }))!;
}

/**
 * Get installation instructions for a package using the provided options.
 * @param options The options that can be used when generating installation instructions.
 */
export function getInstallationInstructions(options: InstallationInstructionsOptions): string[] {
  let wheelFile: string = '';
  for (const artifactUrl of options.artifactUrls) {
    if (artifactUrl.endsWith('whl')) {
      wheelFile = artifactUrl;
    }
  }
  const result: string[] = [
    `(message created by the CI based on PR content)`,
    `# Installation instruction`,
    `## Package ${options.packageName}`,
    `You can install the package \`${options.packageName}\` of this PR using the following command:`,
    `Please install the latest Azure CLI and try this`,
    `<pre>`
  ]
    .concat(
      options.package.context.isPublic
        ? [``, `az extension add --source=${wheelFile}`]
        : [
            ``,
            options.artifactDownloadCommand(wheelFile, getName(wheelFile)),
            `az extension add --source=${getName(wheelFile)}`
          ]
    )
    .concat([`</pre>`, `# Direct download`, ``, `Your files can be directly downloaded here:`, ``]);
  for (const artifactUrl of options.artifactUrls) {
    result.push(`- [${getPathName(artifactUrl)}](${artifactUrl})`);
  }
  result.push(``);
  return result;
}
