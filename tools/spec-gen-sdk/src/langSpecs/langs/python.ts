import { getChildFilePaths, getPathName, run } from '@ts-common/azure-js-dev-tools';
import { InstallationInstructionsOptions } from '../installationInstructions';
import { LanguageConfiguration } from '../languageConfiguration';
import { PackageCommandOptions } from '../packageCommandOptions';
import * as glob from 'glob';
import { BreakingChangeReportOptions } from '../generateBreakingChnageReport';
import { SDKRepository } from '../../sdkRepository';
import { SDKRepositoryPackage } from '../sdkRepositoryPackage';
import { createPackageCommandOptions } from '../packageCommand';
import { sdkLabels } from '@azure/swagger-validation-common';
/**
 * A language configuration for Python.
 */
export const python: LanguageConfiguration = {
  name: 'Python',
  generatorPackageName: '@microsoft.azure/autorest.python',
  packageRootFileName: /^setup.py$|^azure$/,
  packageNameCreator: getPackageName,
  packageCommands: createPackage,
  installationInstructions: getInstallationInstructions,
  generateBreakingChangeReport: generateBreakingChangeReport,
  runLangAfterScripts : initSetupPy,
  breakingChangeLabel: { name: sdkLabels['azure-sdk-for-python'].deprecatedBreakingChange as string, color: 'dc1432' },
  breakingChangesLabel: { name: sdkLabels['azure-sdk-for-python'].breakingChange as string, color: 'dc1432' }
};

export async function initSetupPy(sdkRepo: SDKRepository, changedPackage: SDKRepositoryPackage): Promise<boolean> {
  const packageName = changedPackage.data.name;
  const commandOptions = createPackageCommandOptions(sdkRepo, changedPackage);
  try {
    await run('pip', ['install', '-e' , './tools/azure-sdk-tools/'], {
      ...commandOptions,
      executionFolderPath: changedPackage.repositoryFolderPath,
      capturePrefix: 'build_conf_init',
      throwOnError: true
    });

    await run('python', ['-m', 'packaging_tools' , '--build-conf', packageName], {
      ...commandOptions,
      executionFolderPath: changedPackage.getRootedPackageFolderPath() + '/../',
      capturePrefix: 'build_conf',
      throwOnError: true
    });

    return true;
  } catch (e) {
    return false;
  }
}

export async function generateBreakingChangeReport(option: BreakingChangeReportOptions): Promise<boolean> {
  const { changedPackage } = option;
  const packagePath = changedPackage.getRootedPackageFolderPath();
  const packageName = getPathName(packagePath);
  option.executionFolderPath = changedPackage.repositoryFolderPath;

  // Setup tools
  await run('python', ['./scripts/dev_setup.py', '-p', packageName], {
    ...option,
    capturePrefix: 'breaking_change_setup'
  });

  // Get latest report
  await run('python', ['-m', 'packaging_tools.code_report', packageName], {
    ...option,
    captureError: option.captureOutput,
    capturePrefix: 'breaking_change_report_latest'
  });

  // Get current pypi package report
  await run('python', ['-m', 'packaging_tools.code_report', packageName, '--last-pypi'], {
    ...option,
    captureError: option.captureOutput,
    capturePrefix: 'breaking_change_report_pypi'
  });

  // Get breaking change report
  let reportPattern = `code_reports/**/merged_report.json`;
  let reportFiles = glob.sync(reportPattern, { cwd: packagePath });
  if (reportFiles.length !== 2) {
    reportPattern = `code_reports/**/*.json`;
    reportFiles = glob.sync(reportPattern, { cwd: packagePath });
    if (reportFiles.length !== 2) {
      option.captureChangeLog('Not exact 2 reports found:');
      reportFiles.forEach(filePath => option.captureChangeLog(filePath));
      option.captureChangeLog('Not generating changelog.');
      return false;
    }
  }

  if (reportFiles[0].startsWith('code_reports/latest')) {
    const tmp = reportFiles[0];
    reportFiles[0] = reportFiles[1];
    reportFiles[1] = tmp;
  }

  await run('python', ['-m', 'packaging_tools.change_log', reportFiles[0], reportFiles[1]], {
    ...option,
    captureOutput: (outputLine) => {
      if (outputLine.trim() === '') {
        return;
      }
      option.captureChangeLog(outputLine, outputLine.indexOf('Breaking changes') !== -1);
    },
    captureError: option.captureChangeLog,
    executionFolderPath: packagePath,
    capturePrefix: 'ChangeLog'
  });

  return true;
}

/**
 * Get the name of the package from the relative package folder path.
 * @param rootedRepositoryFolderPath The rooted path to the repository folder.
 * @param relativePackageFolderPath The path to the package folder relative to the repository folder.
 */
export function getPackageName(_rootedRepositoryFolderPath: string, relativePackageFolderPath: string): string {
  return getPathName(relativePackageFolderPath);
}

export async function createPackage(options: PackageCommandOptions): Promise<string[]> {
  await run(
    'python',
    ['./build_package.py', '--dest', options.rootedPackageFolderPath, options.relativePackageFolderPath],
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
  const result: string[] = [
    `(message created by the CI based on PR content)`,
    `# Installation instruction`,
    `## Package ${options.packageName}`,
    `You can install the package \`${options.packageName}\` of this PR using the following command:`,
    `\t\`pip install ` +
      `"git+${options.generationRepositoryUrl}@${options.sdkRepositoryGenerationPullRequestHeadBranch}` +
      `#egg=${options.packageName}&subdirectory=${options.packageName}"\``,
    ``,
    `You can build a wheel to distribute for test using the following command:`,
    `\t\`pip wheel --no-deps ` +
      `"git+${options.generationRepositoryUrl}@${options.sdkRepositoryGenerationPullRequestHeadBranch}` +
      `#egg=${options.packageName}&subdirectory=${options.packageName}"\``,
    ``,
    `If you have a local clone of this repository, you can also do:`,
    ``,
    `- \`git checkout ${options.sdkRepositoryGenerationPullRequestHeadBranch}\``,
    `- \`pip install -e ./${options.packageName}\``,
    ``,
    ``,
    `Or build a wheel file to distribute for testing:`,
    ``,
    `- \`git checkout ${options.sdkRepositoryGenerationPullRequestHeadBranch}\``,
    `- \`pip wheel --no-deps ./${options.packageName}\``,
    ``,
    ``,
    `# Direct download`,
    ``,
    `Your files can be directly downloaded here:`,
    ``
  ];
  for (const artifactUrl of options.artifactUrls) {
    result.push(`- [${getPathName(artifactUrl)}](${artifactUrl})`);
  }
  result.push(``);
  return result;
}
