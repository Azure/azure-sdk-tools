import { AutoRestOptions, AutoRestOptionValue } from '@ts-common/azure-js-dev-tools';
import path from 'path';
import * as glob from 'glob';
import { InstallationInstructions } from '../langSpecs/installationInstructions';
import { SDKRepositoryPackage } from '../langSpecs/sdkRepositoryPackage';
import { getResolvedAutoRestOptions } from '../sdkRepository';
import { SwaggerToSDKConfiguration as LegacySwaggerToSDKConfig } from '../swaggerToSDKConfiguration';
import { InstallInstructionScriptOutput } from '../types/InstallInstructionScriptOutput';
import { PackageData } from '../types/PackageData';
import { repoKeyToString } from '../utils/githubUtils';
import { runSdkAutoCustomScript, setSdkAutoStatus } from '../utils/runScript';
import { WorkflowContext } from './workflow';
import { SyncConfig } from './workflowPackage';
import { CommentCaptureTransport } from './logging';
import { SDKAutomationState } from '../sdkAutomationState';

const switchToNode12 = async (context: WorkflowContext) => {
  context.scriptEnvs.N_PREFIX = path.resolve(context.tmpFolder);
  await runSdkAutoCustomScript(
    context,
    { path: `npx n 12` },
    {
      cwd: context.workingFolder,
      statusContext: context
    }
  );
  context.scriptEnvs.PATH = `${path.join(context.scriptEnvs.N_PREFIX, 'bin')}:${context.scriptEnvs.PATH}`;
};

export const legacyInit = async (context: WorkflowContext) => {
  if (context.skipLegacy) {
    return;
  }
  switch (context.config.sdkName) {
    case 'azure-resource-manager-schemas':
      await switchToNode12(context);
      break;

    case 'azure-sdk-for-net':
      await runSdkAutoCustomScript(
        context,
        { path: `sudo apt-get install -y dotnet-sdk-6.0`, logPrefix: 'apt-get' },
        { cwd: context.tmpFolder, statusContext: context }
      );
      break;

    case 'azure-sdk-for-python':
    case 'azure-sdk-for-python-track2':
    case 'azure-cli-extensions':
      context.logger.info('Legacy python init');
      const options = { cwd: context.workingFolder, statusContext: context };

      await switchToNode12(context);

      await runSdkAutoCustomScript(
        context,
        { path: `python3 -m venv venv` },
        {
          cwd: context.tmpFolder,
          statusContext: context
        }
      );
      const venvPath = path.join(context.tmpFolder, 'venv');
      context.scriptEnvs.VIRTUAL_ENV = path.resolve(venvPath);
      context.scriptEnvs.PATH = `${path.join(path.resolve(venvPath), 'bin')}:${context.scriptEnvs.PATH}`;

      await runSdkAutoCustomScript(
        context,
        { path: `pip install --index-url https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-python/pypi/simple/ --upgrade setuptools wheel pip`, logPrefix: 'pip install' },
        options
      );
      await runSdkAutoCustomScript(
        context,
        { path: `pip install pathlib jinja2 msrestazure --index-url https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-python/pypi/simple/`, logPrefix: 'pip install' },
        options
      );

      break;
  }
};

export const legacyGenerate = async (
  context: WorkflowContext,
  readmeMd: string[],
  statusContext: { status: SDKAutomationState }
) => {
  if (context.skipLegacy) {
    return;
  }
  context.logger.log('section', `Call legacy generate`);

  const config = context.swaggerToSdkConfig as LegacySwaggerToSDKConfig;

  for (const readmeMdPath of readmeMd) {
    const runGen = async (options: AutoRestOptions) => {
      if (typeof options.use !== 'string' || !options.use) {
        options.use = context.legacyLangConfig?.generatorPackageName;
      }

      const args: string[] = [];
      if (options) {
        for (const optionName of Object.keys(options)) {
          let argument = optionName;
          if (!argument.startsWith('--')) {
            argument = `--${argument}`;
          }

          const optionValue: AutoRestOptionValue = options[optionName];
          if (Array.isArray(optionValue)) {
            argument += `=${optionValue.join(',')}`;
          } else if (optionValue !== undefined && optionValue !== '') {
            argument += `=${optionValue}`;
          }

          args.push(argument);
        }
      }

      args.push(path.relative(context.sdkFolder, path.join(context.specFolder, readmeMdPath)));

      await runSdkAutoCustomScript(
        context,
        {
          path: 'autorest',
          stdout: {
            showInComment: /(warning|error)/i
          },
          stderr: {
            showInComment: true,
            scriptWarning: /(warning|error)/i
          },
          exitCode: {
            showInComment: true,
            result: 'error'
          }
        },
        {
          cwd: context.sdkFolder,
          fallbackName: 'Autorest',
          argList: args,
          statusContext
        }
      );
    };

    const autorestOptions = await getResolvedAutoRestOptions(
      path.resolve(context.sdkFolder),
      config,
      readmeMdPath,
      'autorest_options'
    );

    const autorestOptionsForOtherSDK = await getResolvedAutoRestOptions(
      path.resolve(context.sdkFolder),
      config,
      readmeMdPath,
      'autorest_options_for_otherSDK'
    );

    if (Object.keys(autorestOptionsForOtherSDK).length > 0 && !(autorestOptionsForOtherSDK instanceof Array)) {
      await runGen(autorestOptionsForOtherSDK);
      context.logger.info('Finish autorest_option_for_otherSDK');
    }

    if (!(autorestOptions instanceof Array)) {
      await runGen(autorestOptions);
    } else {
      for (const autorestOption of autorestOptions) {
        await runGen(autorestOption);
      }
    }
  }

  context.logger.log('endsection', `Call legacy generate`);
};

export const legacyBuildPackage = async (context: WorkflowContext, pkg: PackageData) => {
  if (context.skipLegacy) {
    return;
  }
  const legacyAfterScripts = (context.swaggerToSdkConfig as LegacySwaggerToSDKConfig).meta?.after_scripts ?? [];
  context.legacyAfterScripts.push(...legacyAfterScripts);

  const commonRunOptions = {
    exitCode: {
      showInComment: true,
      result: 'error'
    },
    stderr: {
      showInComment: true
    }
  } as const;
  const commonOptions = {
    cwd: context.sdkFolder,
    statusContext: pkg
  } as const;

  if (context.legacyAfterScripts.length > 0) {
    context.logger.log('section', 'Call legacy after_scripts');

    for (const scriptCmd of context.legacyAfterScripts) {
      await runSdkAutoCustomScript(context, { path: scriptCmd, ...commonRunOptions }, commonOptions);
    }

    context.logger.log('endsection', 'Call legacy after_scripts');
  }

  switch (context.config.sdkName) {
    case 'azure-sdk-for-net':
      context.logger.log('section', `Call legacy build for azure-sdk-for-net`);

      const scope = path.basename(path.dirname(pkg.relativeFolderPath));
      await runSdkAutoCustomScript(
        context,
        { ...commonRunOptions, path: 'dotnet', logPrefix: 'msbuild', stdout: { scriptWarning: /\: error / } },
        {
          ...commonOptions,
          cwd: context.sdkFolder,
          argList: ['msbuild', 'eng/mgmt.proj', '/t:CreateNugetPackage', `/p:Scope=${scope}`, '/v:n', '/p:SkipTests=true']
        }
      );

      context.logger.log('endsection', `Call legacy build for azure-sdk-for-net`);
      break;

    case 'azure-sdk-for-js':
      context.logger.log('section', `Call legacy build for azure-sdk-for-js`);

      await runSdkAutoCustomScript(
        context,
        { ...commonRunOptions, path: 'npm pack --silent', logPrefix: 'npmPack' },
        { ...commonOptions, cwd: path.join(context.sdkFolder, pkg.relativeFolderPath) }
      );

      context.logger.log('endsection', `Call legacy build for azure-sdk-for-js`);
      break;

    case 'azure-cli-extensions':
      context.logger.log('section', `Call legacy build for azure-cli-extensions`);

      await runSdkAutoCustomScript(
        context,
        { ...commonRunOptions, path: 'python', logPrefix: 'pkgBuild' },
        {
          ...commonOptions,
          cwd: context.sdkFolder,
          argList: ['./scripts/automation/build_package.py', '--dest', pkg.relativeFolderPath, pkg.relativeFolderPath]
        }
      );

      context.logger.log('endsection', `Call legacy build for azure-cli-extensions`);
      break;

    case 'azure-sdk-for-python':
    case 'azure-sdk-for-python-track2':
      context.logger.log('section', `Call legacy build for azure-sdk-for-python`);

      await runSdkAutoCustomScript(
        context,
        { ...commonRunOptions, path: 'pip', logPrefix: 'buildConfInit' },
        { ...commonOptions, argList: ['install', '-e', './tools/azure-sdk-tools/'] }
      );
      let pkgName = pkg.name;
      const prefix = context.legacyLangConfig?.packageNameAltPrefix;
      if (prefix !== undefined) {
        pkgName = pkgName.substr(prefix.length);
      }
      await runSdkAutoCustomScript(
        context,
        { ...commonRunOptions, path: 'python', logPrefix: 'buildConf' },
        {
          ...commonOptions,
          cwd: path.join(context.sdkFolder, path.dirname(pkg.relativeFolderPath)),
          argList: ['-m', 'packaging_tools', '--build-conf', pkgName]
        }
      );
      await runSdkAutoCustomScript(
        context,
        { ...commonRunOptions, path: 'python', logPrefix: 'buildPackage' },
        { ...commonOptions, argList: ['./build_package.py', '--dest', pkg.relativeFolderPath, pkg.relativeFolderPath] }
      );
      await runSdkAutoCustomScript(
        context,
        {
          ...commonRunOptions,
          path: 'python',
          logPrefix: 'changelogSetup',
          exitCode: { showInComment: true, result: 'warning' }
        },
        { ...commonOptions, argList: ['./scripts/dev_setup.py', '-p', pkgName] }
      );
      await runSdkAutoCustomScript(
        context,
        {
          ...commonRunOptions,
          path: 'python',
          logPrefix: 'changelogReport',
          exitCode: { showInComment: true, result: 'warning' }
        },
        { ...commonOptions, argList: ['-m', 'packaging_tools.code_report', pkgName] }
      );
      await runSdkAutoCustomScript(
        context,
        {
          ...commonRunOptions,
          exitCode: { showInComment: true, result: 'warning' },
          path: 'python',
          logPrefix: 'changelogReportLatest'
        },
        { ...commonOptions, argList: ['-m', 'packaging_tools.code_report', pkgName, '--last-pypi'] }
      );

      let reportPattern = `code_reports/**/merged_report.json`;
      let reportFiles = glob.sync(reportPattern, { cwd: path.join(context.sdkFolder, pkg.relativeFolderPath) });
      if (reportFiles.length !== 2) {
        reportPattern = `code_reports/**/*.json`;
        reportFiles = glob.sync(reportPattern, { cwd: path.join(context.sdkFolder, pkg.relativeFolderPath) });
        if (reportFiles.length !== 2) {
          context.logger.warn('Not exact 2 reports found:');
          for (const filePath of reportFiles) {
            context.logger.warn(`\t${filePath}`);
          }
          context.logger.warn('Not generating changelog.');
          setSdkAutoStatus(context, 'warning');
          break;
        }
      }

      if (reportFiles[0].startsWith('code_reports/latest')) {
        const tmp = reportFiles[0];
        reportFiles[0] = reportFiles[1];
        reportFiles[1] = tmp;
      }

      const changelogs: string[] = [];
      const captureTransport = new CommentCaptureTransport({
        output: changelogs,
        extraLevelFilter: ['cmderr', 'cmdout']
      });
      context.logger.add(captureTransport);
      await runSdkAutoCustomScript(
        context,
        {
          ...commonRunOptions,
          exitCode: { showInComment: true, result: 'warning' },
          stdout: { showInComment: true },
          stderr: { showInComment: true },
          path: 'python',
          logPrefix: 'Changelog'
        },
        {
          ...commonOptions,
          cwd: path.join(context.sdkFolder, pkg.relativeFolderPath),
          argList: ['-m', 'packaging_tools.change_log', reportFiles[0], reportFiles[1]]
        }
      );
      context.logger.remove(captureTransport);

      for (const changelog of changelogs) {
        if (changelog.indexOf('Breaking changes') !== -1) {
          context.logger.warn(`Breaking change found in changelog`);
          pkg.hasBreakingChange = true;
          break;
        }
      }

      context.logger.log('endsection', `Call legacy build for azure-sdk-for-python`);
      break;
  }
};

export const legacyInstallInstruction = async (context: WorkflowContext, pkg: PackageData, syncConfig: SyncConfig) => {
  if (context.skipLegacy) {
    return;
  }
  const getInstallInstruction = async (option?: InstallationInstructions): Promise<string | undefined> => {
    if (!option) {
      return undefined;
    }
    let result = option;
    if (typeof option === 'function') {
      result = await option({
        packageName: pkg.name,
        artifactDownloadCommand: (url, fileName) =>
          context.config.storage.downloadCommand.replace('{URL}', url).replace('{FILENAME}', fileName),
        artifactUrls: pkg.artifactBlobUrls!,
        generationRepositoryUrl: `https://github.com/${repoKeyToString(context.sdkRepoConfig.integrationRepository)}`,
        package: {
          context: {
            isPublic: !context.legacyLangConfig?.isPrivatePackage && context.config.storage.isPublic
          }
        } as SDKRepositoryPackage,
        sdkRepositoryGenerationPullRequestHeadBranch: syncConfig.targetBranch
      });
    }
    if (typeof result === 'string') {
      return result;
    }
    if (Array.isArray(result)) {
      return result.join('\n');
    }
    return undefined;
  };

  const output: InstallInstructionScriptOutput = {
    full: (await getInstallInstruction(context.legacyLangConfig?.installationInstructions)) ?? '',
    lite: await getInstallInstruction(context.legacyLangConfig?.liteInstallationInstruction)
  };
  return output;
};

export const legacyArtifactSearchOption = (context: WorkflowContext, pkg: PackageData) => {
  if (context.skipLegacy) {
    return;
  }
  switch (context.config.sdkName) {
    case 'azure-sdk-for-net':
      if (pkg.status === 'failed') {
        return false;
      }
      return {
        searchRegex: new RegExp(`${path.basename(pkg.relativeFolderPath).replace(/\./g, '\\.')}.*nupkg$`),
        searchFolder: 'artifacts/packages'
      };

    case 'azure-sdk-for-js':
      return { searchRegex: /\.tgz$/ };

    case 'azure-cli-extensions':
    case 'azure-sdk-for-python':
    case 'azure-sdk-for-python-track2':
      return { searchRegex: /\.(whl|zip)$/ };
  }
  return false;
};
