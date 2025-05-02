import { default as FileHound } from 'filehound';
import path from 'path';
import fs, { copyFileSync, existsSync } from 'fs';
import { InstallInstructionScriptInput } from '../types/InstallInstructionScriptInput';
import { getInstallInstructionScriptOutput } from '../types/InstallInstructionScriptOutput';
import { PackageData } from '../types/PackageData';
import { deleteTmpJsonFile, readTmpJsonFile, writeTmpJsonFile } from '../utils/fsUtils';
import { isLineMatch, runSdkAutoCustomScript, setSdkAutoStatus } from '../utils/runScript';
import {
  WorkflowContext
} from './workflow';
import { getLanguageByRepoName } from './entrypoint';
import { CommentCaptureTransport } from './logging';
import { toolWarning } from '../utils/messageUtils';

export const workflowPkgMain = async (context: WorkflowContext, pkg: PackageData) => {
  context.logger.log('section', `Handle package ${pkg.name}`);
  context.logger.info(`Package log to a new logFile`);
  
  const pkgCaptureTransport = new CommentCaptureTransport({
    extraLevelFilter: ['error', 'warn'],
    level: 'debug',
    output: pkg.messages
  });
  context.logger.add(pkgCaptureTransport);

  await workflowPkgCallBuildScript(context, pkg);
  await workflowPkgCallChangelogScript(context, pkg);
  await workflowPkgDetectArtifacts(context, pkg);
  await workflowPkgSaveSDKArtifact(context, pkg);
  await workflowPkgSaveApiViewArtifact(context, pkg);
  await workflowPkgCallInstallInstructionScript(context, pkg);

  setSdkAutoStatus(pkg, 'succeeded');
  context.logger.remove(pkgCaptureTransport);
  context.logger.log('endsection', `Handle package ${pkg.name}`);
};

const workflowPkgCallBuildScript = async (context: WorkflowContext, pkg: PackageData) => {
  const runOptions = context.swaggerToSdkConfig.packageOptions.buildScript;
  if (!runOptions) {
    context.logger.info('buildScript of packageOptions is not configured in swagger_to_sdk_config.json.');
    return;
  }

  context.logger.log('section', 'Call BuildScript');
  await runSdkAutoCustomScript(context, runOptions, {
    cwd: context.config.localSdkRepoPath,
    fallbackName: 'Build',
    argList: [pkg.relativeFolderPath, ...pkg.extraRelativeFolderPaths],
    statusContext: pkg
  });

  context.logger.log('endsection', 'Call BuildScript');
};

const workflowPkgCallChangelogScript = async (context: WorkflowContext, pkg: PackageData) => {
  const runOptions = context.swaggerToSdkConfig.packageOptions.changelogScript;
  if (!runOptions) {
    context.logger.info('changelogScript is not configured');
    for (const changelog of pkg.changelogs) {
      if (changelog) {
        context.logger.info(`[Changelog] ${changelog}`, { showInComment: true });
      }
    }
  } else {
    context.logger.log('section', 'Call ChangelogScript');
    const changeLogCaptureTransport = new CommentCaptureTransport({
      extraLevelFilter: ['cmdout', 'cmderr'],
      output: pkg.changelogs
    });
    context.logger.add(changeLogCaptureTransport);
    const result = await runSdkAutoCustomScript(context, runOptions, {
      cwd: context.config.localSdkRepoPath,
      fallbackName: 'Changelog',
      argList: [pkg.relativeFolderPath, ...pkg.extraRelativeFolderPaths],
      statusContext: pkg
    });
    context.logger.remove(changeLogCaptureTransport);
    setSdkAutoStatus(pkg, result);
    if (result !== 'failed') {
      for (const changelog of pkg.changelogs) {
        if (isLineMatch(changelog, runOptions.breakingChangeDetect)) {
          pkg.hasBreakingChange = true;
          break;
        }
      }
    }
    context.logger.log('endsection', 'Call ChangelogScript');
  }
};

const workflowPkgDetectArtifacts = async (context: WorkflowContext, pkg: PackageData) => {
  const searchOption = context.swaggerToSdkConfig.artifactOptions.artifactPathFromFileSearch;
  if (!searchOption) {
    context.logger.info(`Skip artifact search`);
    return;
  }

  const searchRegex = searchOption.searchRegex;
  context.logger.info(`Search artifact with: ${searchRegex}`);
  const folders = [pkg.relativeFolderPath, ...pkg.extraRelativeFolderPaths];
  if (searchOption.searchFolder) {
    if (fs.existsSync(path.join(context.config.localSdkRepoPath, searchOption.searchFolder))) {
      folders.push(searchOption.searchFolder);
    } else {
      context.logger.warn(toolWarning(`Skip artifact folder because it doesn't exist: ${searchOption.searchFolder}`));
    }
  }

  let files = await FileHound.create()
    .paths(...folders.map((packageFolder) => path.join(context.config.localSdkRepoPath, packageFolder)))
    .addFilter((file) => {
      return searchRegex.test(file.getName());
    })
    .find();
  files = files.map((filePath) => path.relative(context.config.localSdkRepoPath, filePath));

  context.logger.info(`${files.length} artifact found:`);
  for (const artifactPath of files) {
    context.logger.info(`\t${artifactPath}`);
  }
  pkg.artifactPaths.push(...files);
};

/**
 * 
 * @param context 
 * @param pkg 
 * 
 * Copy sdk artifact path to {work_dir}/out/stagedArtifacts
 */
const workflowPkgSaveSDKArtifact = async (context: WorkflowContext, pkg: PackageData) => {
  const language = pkg.language ?? getLanguageByRepoName(context.sdkRepoConfig.mainRepository.name);
  const relativeFolderPathParts = pkg.relativeFolderPath.split('/');
  let serviceName = relativeFolderPathParts[relativeFolderPathParts.indexOf('sdk') + 1];
  if (language.toLowerCase() === 'go') {
    serviceName = pkg.relativeFolderPath.replace(/^\/?sdk\//, ""); // go uses relative path as package name
  }
  pkg.serviceName = serviceName;
  context.logger.info(`Save ${pkg.artifactPaths.length} artifact to Azure devOps.`);
  
  const stagedArtifactsFolder = path.join(context.config.workingFolder, 'out', 'stagedArtifacts');
  context.stagedArtifactsFolder = stagedArtifactsFolder;

  // if no artifact generated or language is Go, skip
  if (pkg.artifactPaths.length === 0 || language.toLowerCase() === 'go') { 
    return; 
  }
 
  const destination = path.join(stagedArtifactsFolder, pkg.name);
  if (!existsSync(destination)) {
    fs.mkdirSync(destination, { recursive: true });
  }
  context.sdkArtifactFolder = destination;
  for (const artifactPath of pkg.artifactPaths) {
    const fileName = path.basename(artifactPath);
    if (context.config.runEnv !== 'test') {
      context.logger.info(`Copy SDK artifact ${fileName} from ${path.join(context.config.localSdkRepoPath, artifactPath)} to ${path.join(destination, fileName)}`);
      copyFileSync(path.join(context.config.localSdkRepoPath, artifactPath), path.join(destination, fileName));
    }
  }
};

/*
* Copy apiView artifact and generate meta file in {work_dir}/out/stagedArtifacts
* */
const workflowPkgSaveApiViewArtifact = async (context: WorkflowContext, pkg: PackageData) => {
  if (!pkg.apiViewArtifactPath) {
    return;
  }

  const destination = path.join(context.config.workingFolder, 'out', 'stagedArtifacts', pkg.name);
  if (!existsSync(destination)) {
    fs.mkdirSync(destination, { recursive: true });
  }
  context.sdkApiViewArtifactFolder = destination;
  const fileName = path.basename(pkg.apiViewArtifactPath);
  context.logger.info(`Copy apiView artifact from ${path.join(context.config.localSdkRepoPath, pkg.apiViewArtifactPath)} to ${path.join(destination, fileName)}`);
  copyFileSync(path.join(context.config.localSdkRepoPath, pkg.apiViewArtifactPath), path.join(destination, fileName));
};

const fileInstallInstructionInput = 'installInstructionInput.json';
const fileInstallInstructionOutput = 'installInstructionOutput.json';
const workflowPkgCallInstallInstructionScript = async (
  context: WorkflowContext,
  pkg: PackageData
) => {
  const runOptions = context.swaggerToSdkConfig.artifactOptions.installInstructionScript;
  if (!runOptions) {
    context.logger.info('Skip installInstructionScript');
    return;
  }

  context.logger.log('section', 'Call InstallInstructionScript');

  const input: InstallInstructionScriptInput = {
    isPublic: false,
    downloadUrlPrefix: "",
    downloadCommandTemplate: "",
    packageName: pkg.name,
    artifacts: pkg.artifactPaths.map((p) => path.basename(p))
  };
  writeTmpJsonFile(context, fileInstallInstructionInput, input);
  deleteTmpJsonFile(context, fileInstallInstructionOutput);

  const result = await runSdkAutoCustomScript(context, runOptions, {
    cwd: context.config.localSdkRepoPath,
    fallbackName: 'Inst',
    argTmpFileList: [fileInstallInstructionInput, fileInstallInstructionOutput],
    statusContext: context
  });
  if (result !== 'failed') {
    const output = getInstallInstructionScriptOutput(readTmpJsonFile(context, fileInstallInstructionOutput));
    pkg.installationInstructions = output.full;
    pkg.liteInstallationInstruction = output.lite;
  }

  context.logger.log('section', 'Call InstallInstructionScript');
};
