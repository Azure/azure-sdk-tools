import { any, getChildFilePaths, run, RunResult, getName } from '@ts-common/azure-js-dev-tools';
import { InstallationInstructionsOptions } from '../installationInstructions';
import { LanguageConfiguration } from '../languageConfiguration';
import { PackageCommandOptions } from '../packageCommandOptions';
import { RepositoryCommandOptions } from '../repositoryCommandOptions';

/**
 * A language configuration for Ruby.
 */
export const ruby: LanguageConfiguration = {
  name: 'Ruby',
  generatorPackageName: '@microsoft.azure/autorest.ruby',
  generationCommands: ['bundle install', rakeArmRegen],
  packageRootFileName: /.*\.gemspec/,
  packageCommands: createPackage,
  installationInstructions: getInstallationInstructions
};

/**
 * Run "rake arm:regen['package-name']" on each of the packages to regenerate.
 */
async function rakeArmRegen(options: RepositoryCommandOptions): Promise<void> {
  if (!options.specificationPullRequestHeadCommitId) {
    await options.logger.logError(
      `Can't generate ruby packages because no specification pull request head commit id was provided.`
    );
    options.repositoryData.status = 'failed';
  } else {
    const packageNamesToGenerate: string[] = await getPackageNamesToGenerate();
    for (const packageNameToGenerate of packageNamesToGenerate) {
      const result: RunResult = await run(
        `rake`,
        [`arm:regen['${packageNameToGenerate}','${options.specificationPullRequestHeadCommitId}','resource-manager']`],
        options
      );
      if (result.exitCode !== 0) {
        options.repositoryData.status = 'failed';
        break;
      }
    }
  }
}

export async function getPackageNamesToGenerate(): Promise<string[]> {
  const result: string[] = [];
  // for (const readmeMdFileUrlToGenerate of options.repositoryData.readmeMdFileUrlsToGenerate) {
  //   const readmeRubyMdFileUrlToGenerate: string = readmeMdFileUrlToGenerate.replace('readme.md', 'readme.ruby.md');
  //   const readmeRubyMdFileResponse: HttpResponse = await options.httpClient.sendRequest({
  //     method: 'GET',
  //     url: readmeRubyMdFileUrlToGenerate
  //   });
  //   if (readmeRubyMdFileResponse.statusCode === 200 && readmeRubyMdFileResponse.body) {
  //     const packageNameToGenerate: string | undefined = getPackageNameToGenerate(readmeRubyMdFileResponse.body);
  //     if (packageNameToGenerate) {
  //       result.push(packageNameToGenerate);
  //     }
  //   }
  // }
  return result;
}

export function getPackageNameToGenerate(readmeMdFileContents: string): string | undefined {
  let result: string | undefined;
  const packageNameRegExp: RegExp = /package-name:(.*)/i;
  const packageNameMatch: RegExpMatchArray | null = readmeMdFileContents.match(packageNameRegExp);
  if (packageNameMatch) {
    result = packageNameMatch[1].trim();
    const managementPrefix = 'azure_mgmt_';
    if (!result.startsWith(managementPrefix)) {
      result = undefined;
    } else {
      result = result.substring(managementPrefix.length);
    }
  }

  return result;
}

async function createPackage(options: PackageCommandOptions): Promise<string[]> {
  const result: string[] = [];

  const packageFolderPath: string = options.rootedPackageFolderPath;
  await options.logger.logInfo(`Looking for gemspec files in "${packageFolderPath}"...`);
  const gemspecFilePaths: string[] | undefined = await getChildFilePaths(packageFolderPath, {
    fileCondition: (filePath: string) => filePath.endsWith('.gemspec')
  });
  if (!any(gemspecFilePaths)) {
    await options.logger.logError(`Didn't find any gemspec files in "${packageFolderPath}".`);
  } else {
    for (const gemspecFilePath of gemspecFilePaths) {
      const gemBuildResult: RunResult = await run('gem', ['build', gemspecFilePath], options);
      if (gemBuildResult.error) {
        await options.logger.logError(gemBuildResult.error.toString());
        options.packageData.status = 'failed';
      } else if (gemBuildResult.exitCode !== 0) {
        await options.logger.logError(`Failed to create gem for "${gemspecFilePath}".`);
        options.packageData.status = 'failed';
      } else {
        const gemFilePaths: string[] | undefined = await getChildFilePaths(packageFolderPath, {
          fileCondition: (filePath: string) => filePath.endsWith('.gem')
        });
        if (!any(gemFilePaths)) {
          await options.logger.logError(`Didn't find any gem files in "${packageFolderPath}".`);
        } else {
          result.push(...gemFilePaths);
        }
      }
    }
  }

  return result;
}

/**
 * Get installation instructions for a package using the provided options.
 * @param options The options that can be used when generating installation instructions.
 */
export function getInstallationInstructions(options: InstallationInstructionsOptions): string[] {
  const artifactUrl: string = options.artifactUrls[0];
  const artifactName: string = getName(artifactUrl);
  const artifactNameDashIndex: number = artifactName.indexOf('-');
  const gemName: string = artifactName.substring(0, artifactNameDashIndex);
  const gemVersion: string = artifactName.substring(artifactNameDashIndex + 1, artifactName.lastIndexOf('.'));
  return [
    `## Installation Instructions`,
    `The Gem file for \`${gemName}\` can be downloaded [here](${artifactUrl}).`,
    `After downloading the gem file, you can add it to your Ruby project by running the following command:`,
    `\`\`\`gem '${gemName}', '${gemVersion}', '<download-folder-path>/${artifactName}'\`\`\``,
    `## Direct Download`,
    `The generated gem can be directly downloaded from here:`,
    `- [${artifactName}](${artifactUrl})`
  ];
}
