import { default as FileHound } from 'filehound';
import path from 'path';
import fs, { copyFileSync, existsSync } from 'fs';
import { InstallInstructionScriptInput } from '../types/InstallInstructionScriptInput';
import { getInstallInstructionScriptOutput } from '../types/InstallInstructionScriptOutput';
import { getGenerationBranchName, getIntegrationBranchName, PackageData } from '../types/PackageData2';
import { deleteTmpJsonFile, readTmpJsonFile, writeTmpJsonFile } from '../utils/fsUtils2';
import { RepoKey, repoKeyToString } from '../utils/githubUtils2';
import { gitCheckoutBranch, gitGetCommitter } from '../utils/gitUtils2';
import { isLineMatch, runSdkAutoCustomScript, setSdkAutoStatus } from '../utils/runScript2';
import { CommentCaptureTransport } from './logging';
import {
  branchMain,
  branchSdkGen,
  branchSecondary,
  FailureType,
  remoteIntegration,
  remoteMain,
  setFailureType,
  WorkflowContext
} from './workflow2';
import { mkdirpSync } from 'fs-extra';
import { getLanguageByRepoName } from './entrypoint2';
import { FileStatusResult } from 'simple-git';

export const workflowPkgMain = async (context: WorkflowContext, pkg: PackageData) => {
  context.logger.log('section', `Handle package ${pkg.name}`);

  const captureTransport = new CommentCaptureTransport({
    extraLevelFilter: ['error', 'warn'],
    level: 'debug',
    output: pkg.messages
  });
  context.logger.add(captureTransport);

  context.logger.info(`Package log to a new logFile`);

  const syncConfig = workflowPkgGetSyncConfig(context, pkg);
  const pushBranchPromise = (await workflowPkgUpdateBranch(context, pkg, syncConfig))();

  await workflowPkgCallBuildScript(context, pkg);
  await workflowPkgCallChangelogScript(context, pkg);
  await workflowPkgDetectArtifacts(context, pkg);
  await workflowPkgSaveSDKArtifact(context, pkg);
  await workflowPkgSaveApiViewArtifact(context, pkg);
  await workflowPkgCallInstallInstructionScript(context, pkg);
  await pushBranchPromise;

  setSdkAutoStatus(pkg, 'succeeded');

  context.logger.remove(captureTransport);
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
    cwd: context.sdkFolder,
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

    const captureTransport = new CommentCaptureTransport({
      extraLevelFilter: ['cmdout', 'cmderr'],
      output: pkg.changelogs
    });
    context.logger.add(captureTransport);
    const result = await runSdkAutoCustomScript(context, runOptions, {
      cwd: context.sdkFolder,
      fallbackName: 'Changelog',
      argList: [pkg.relativeFolderPath, ...pkg.extraRelativeFolderPaths],
      statusContext: pkg
    });
    context.logger.remove(captureTransport);

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
    if (fs.existsSync(path.join(context.sdkFolder, searchOption.searchFolder))) {
      folders.push(searchOption.searchFolder);
    } else {
      context.logger.warn(`Skip artifact folder because it doesn't exist: ${searchOption.searchFolder}`);
    }
  }

  let files = await FileHound.create()
    .paths(...folders.map((packageFolder) => path.join(context.sdkFolder, packageFolder)))
    .addFilter((file) => {
      return searchRegex.test(file.getName());
    })
    .find();
  files = files.map((filePath) => path.relative(context.sdkFolder, filePath));

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
 * Copy sdk artifact path to {work_dir}/generatedSdkArtifacts
 */
const workflowPkgSaveSDKArtifact = async (context: WorkflowContext, pkg: PackageData) => {
  context.logger.info(`Save ${pkg.artifactPaths.length} artifact to Azure devOps.`);
  const language = pkg.language ?? getLanguageByRepoName(context.sdkRepoConfig.mainRepository.name);
  console.log(`##vso[task.setVariable variable=sdkLanguage]${language}`);

  // if no artifact generated or language is Go, skip
  if (pkg.artifactPaths.length === 0 || language.toLocaleLowerCase() === 'go') { 
    return; 
  }
  
  const destination = path.join(context.workingFolder, 'generatedSdkArtifacts');
  if (!existsSync(destination)) {
    mkdirpSync(destination);
  }
  console.log(`##vso[task.setVariable variable=HasSDKArtifact]true`);
  const artifactName = `SDK_Artifact_${language}`; // it's the artifact in pipeline artifacts
  console.log(`##vso[task.setVariable variable=sdkArtifactName]${artifactName}`);
  for (const artifactPath of pkg.artifactPaths) {
    const fileName = path.basename(artifactPath);
    if (context.config.runEnv !== 'test') {
      context.logger.info(`Copy SDK artifact ${fileName} from ${path.join(context.sdkFolder, artifactPath)} to ${path.join(destination, fileName)}`);
      copyFileSync(path.join(context.sdkFolder, artifactPath), path.join(destination, fileName));
    }
  }
};

/*
* Copy apiView artifact and generate meta file in {work_dir}/sdkApiViewArtifacts/{language}/
* */
const workflowPkgSaveApiViewArtifact = async (context: WorkflowContext, pkg: PackageData) => {
  if (!pkg.apiViewArtifactPath) {
    return;
  }

  const language = pkg.language ?? getLanguageByRepoName(context.sdkRepoConfig.mainRepository.name);
  const destination = path.join(context.workingFolder, 'sdkApiViewArtifacts');
  if (!existsSync(destination)) {
    mkdirpSync(destination);
  }
  console.log(`##vso[task.setVariable variable=HasApiViewArtifact]true`);
  const artifactName = `sdkApiViewArtifact_${language}`; // it's the artifact in pipeline artifacts
  console.log(`##vso[task.setVariable variable=ArtifactName]${artifactName}`);
  const fileName = path.basename(pkg.apiViewArtifactPath);
  context.logger.info(`Copy apiView artifact from ${path.join(context.sdkFolder, pkg.apiViewArtifactPath)} to ${path.join(destination, fileName)}`);
  copyFileSync(path.join(context.sdkFolder, pkg.apiViewArtifactPath), path.join(destination, fileName));
  const apiViewArtifactMeta = {
    packageName: pkg.name,
    apiViewArtifact: fileName,
    specCommitSha: context.config.specCommitSha,
    language,
    artifactName
  };
  fs.writeFileSync(path.join(destination, `_meta_${fileName}.json`), JSON.stringify(apiViewArtifactMeta, undefined, 2));
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
    artifacts: pkg.artifactPaths.map((p) => path.basename(p)),
    trigger: "pullRequest"
  };
  writeTmpJsonFile(context, fileInstallInstructionInput, input);
  deleteTmpJsonFile(context, fileInstallInstructionOutput);

  const result = await runSdkAutoCustomScript(context, runOptions, {
    cwd: context.sdkFolder,
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

export type SyncConfig = {
  baseBranch: string;
  baseRepo: RepoKey;
  baseRemote: string;
  targetBranch: string;
  targetRepo: RepoKey;
  targetRemote: string;
  hasChanges?: boolean;
};

const workflowPkgGetSyncConfig = (context: WorkflowContext, pkg: PackageData) => {
  const config: SyncConfig = {
    baseBranch: context.sdkRepoConfig.mainBranch,
    baseRepo: context.sdkRepoConfig.mainRepository,
    baseRemote: remoteMain,
    targetBranch: getIntegrationBranchName(context, pkg.name),
    targetRepo: context.sdkRepoConfig.integrationRepository,
    targetRemote: remoteIntegration
  };

  config.baseRepo = context.sdkRepoConfig.integrationRepository;
  config.baseRemote = remoteIntegration;
  config.targetBranch = getGenerationBranchName(context, pkg.name);
  context.logger.info(
    `baseBranch [${config.baseBranch}] baseRepo [${repoKeyToString(config.baseRepo)}] baseRemote [${config.baseRemote}]`
  );
  context.logger.info(
    `targetBranch [${config.targetBranch}] targetRepo [${repoKeyToString(config.targetRepo)}] baseRemote [${config.targetRemote
    }]`
  );
  return config;
};

const workflowPkgUpdateBranch = async (context: WorkflowContext, pkg: PackageData, syncConfig: SyncConfig) => {
  if (
    repoKeyToString(context.sdkRepoConfig.mainRepository) === repoKeyToString(syncConfig.baseRepo) &&
    context.sdkRepoConfig.mainBranch === syncConfig.baseBranch
  ) {
    context.logger.info('Skip sync baseBranch due to same remote same branch');
  } else {
    context.logger.log('git', `Push ${branchMain} to ${repoKeyToString(syncConfig.baseRepo)}:${syncConfig.baseBranch}`);
    const pushResult = await context.sdkRepo.push([syncConfig.baseRemote, `+refs/heads/${branchMain}:refs/heads/${syncConfig.baseBranch}`]);
    if (!pushResult) {
      context.logger.warn(
        `Warning: Failed to push with code [${pushResult}]: ${repoKeyToString(syncConfig.baseRepo)}:${syncConfig.baseBranch}. This doesn't impact SDK generation.`
      );
      setSdkAutoStatus(pkg, 'warning');
    }
  }

  context.logger.log('git', `Create targetBranch ${syncConfig.targetBranch}`);
  const baseCommit = await context.sdkRepo.revparse(branchSecondary);
  await context.sdkRepo.raw(['branch', syncConfig.targetBranch, baseCommit, '--force']);
  await gitCheckoutBranch(context, context.sdkRepo, syncConfig.targetBranch, false);

  const foldersToAdd = [pkg.relativeFolderPath, ...pkg.extraRelativeFolderPaths]
    .filter((p) => p !== undefined)
    .map((p) => path.relative('.', p));
  context.logger.log('git', `Checkout sdk folders from ${branchSdkGen} and commit: ${foldersToAdd.join(' ')}`);
  const sdkGenCommit = await context.sdkRepo.revparse(branchSdkGen);
  const sdkGenTree = await context.sdkRepo.revparse(`${sdkGenCommit}^{tree}`);
  if (foldersToAdd.length > 0) {
    await context.sdkRepo.raw(['read-tree', sdkGenTree]);
    await context.sdkRepo.raw(['checkout', '--', '.']);
  }

  await context.sdkRepo.raw(['update-index', '--refresh']);

  const statusFiles = await context.sdkRepo.status();
  const fileList = statusFiles.files.map((item: FileStatusResult) => item.path);

  let commitMsg = `CodeGen from PR ${context.config.pullNumber} in ${repoKeyToString(context.config.specRepo)}\n`;
  const commitMsgsuffix = await context.specRepo.log();
  commitMsg += commitMsgsuffix.latest?.message;

  await gitGetCommitter(context.sdkRepo);
  await context.sdkRepo.raw(['commit', '-m', commitMsg]);
  syncConfig.hasChanges = fileList.length > 0;

  context.logger.log('git', `Push ${syncConfig.targetBranch} to ${repoKeyToString(syncConfig.targetRepo)}`);

  // Push in parallel to speed up
  return async () => {
    const result = await context.sdkRepo.push([syncConfig.targetRemote, `+refs/heads/${syncConfig.targetBranch}:refs/heads/${syncConfig.targetBranch}`]);
    if (!result) {
      context.logger.error(
        `GitError: Failed to push with code [${result}]: ${repoKeyToString(syncConfig.targetRepo)}:${syncConfig.targetBranch}. Please re-run the pipeline if the error is retryable or report this issue through https://aka.ms/azsdk/support/specreview-channel.`
      );
      setSdkAutoStatus(pkg, 'failed');
      setFailureType(context, FailureType.PipelineFrameworkFailed);
    }
  };
};

