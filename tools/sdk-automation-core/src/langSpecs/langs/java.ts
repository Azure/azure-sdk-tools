import { getChildFilePaths, joinPath, mvnExecutable, run, RunResult, getPathName } from '@ts-common/azure-js-dev-tools';
import { InstallationInstructionsOptions } from '../installationInstructions';
import { LanguageConfiguration } from '../languageConfiguration';
import { PackageCommandOptions } from '../packageCommandOptions';

/**
 * A language configuration for Java.
 */
export const java: LanguageConfiguration = {
  name: 'Java',
  generatorPackageName: '@microsoft.azure/autorest.java',
  packageRootFileName: 'pom.xml',
  packageCommands: createPackage,
  installationInstructions: getInstallationInstructions
};

/**
 * Create the package file for a package folder.
 * @param options The options for creating the package.
 */
export async function createPackage(options: PackageCommandOptions): Promise<string[]> {
  const result: string[] = [];
  const mvnResult: RunResult = await run(
    mvnExecutable(),
    [
      'source:jar',
      'javadoc:jar',
      'package',
      '-f',
      options.rootedPackageFolderPath,
      '-DskipTests',
      '--batch-mode',
      '-q'
    ],
    {
      ...options,
      captureOutput: options.captureError, // As we use -q to output error only, all the output should be error.
      capturePrefix: 'mvn',
      throwOnError: false
    }
  );
  if (mvnResult.exitCode !== 0 || options.packageData.status === 'failed') {
    // Suppress error message as java sdk generation has a lot of problem
    options.packageData.status = 'warning';
  } else {
    result.push(joinPath(options.rootedPackageFolderPath, 'pom.xml'));

    const targetFolderPath: string = joinPath(options.rootedPackageFolderPath, 'target');
    const jarFilePaths: string[] | undefined = await getChildFilePaths(targetFolderPath, {
      fileCondition: (filePath: string) => filePath.endsWith('.jar')
    });
    if (jarFilePaths) {
      result.push(...jarFilePaths);
    }
  }
  return result;
}

/**
 * Get installation instructions for a package using the provided options.
 * @param options The options that can be used when generating installation instructions.
 */
export function getInstallationInstructions(options: InstallationInstructionsOptions): string[] {
  const result: string[] = [
    `## Installation Instructions`,
    `You can install the package \`${options.packageName}\` of this PR by downloading the artifact jar files. ` +
      `Then ensure that the jar files are on your project's classpath.`,
    `## Direct Download`,
    `The generated package artifacts can be directly downloaded from here:`
  ];
  for (const artifactUrl of options.artifactUrls) {
    result.push(`- [${getPathName(artifactUrl)}](${artifactUrl})`);
  }
  return result;
}
