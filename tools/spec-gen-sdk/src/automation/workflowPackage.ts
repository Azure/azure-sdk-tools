import { default as FileHound } from 'filehound';
import path from 'path';
import fs, { copyFileSync, existsSync } from 'fs';
import { InstallInstructionScriptInput } from '../types/InstallInstructionScriptInput';
import { getInstallInstructionScriptOutput } from '../types/InstallInstructionScriptOutput';
import { getGenerationBranchName, getIntegrationBranchName, PackageData } from '../types/PackageData';
import { deleteTmpJsonFile, readTmpJsonFile, writeTmpJsonFile } from '../utils/fsUtils';
import { RepoKey, repoKeyToString } from '../utils/githubUtils';
import { gitCheckoutBranch, gitGetCommitter } from '../utils/gitUtils';
import { isLineMatch, runSdkAutoCustomScript, setSdkAutoStatus } from '../utils/runScript';
import { legacyArtifactSearchOption, legacyBuildPackage, legacyInstallInstruction } from './legacy';
import { CommentCaptureTransport, getBlobName, loggerStorageAccountTransport } from './logging';
import {
  branchMain,
  branchSdkGen,
  branchSecondary,
  FailureType,
  remoteIntegration,
  remoteMain,
  setFailureType,
  WorkflowContext
} from './workflow';
import { mkdirpSync } from 'fs-extra';
import { getLanguageByRepoName } from './entrypoint';
import { FileStatusResult } from 'simple-git';

export const workflowPkgMain = async (context: WorkflowContext, pkg: PackageData) => {
  context.logger.log('section', `Handle package ${pkg.name}`);

  const captureTransport = new CommentCaptureTransport({
    extraLevelFilter: ['error', 'warn'],
    level: 'debug',
    output: pkg.messages
  });
  context.logger.add(captureTransport);

  const { blobTransport, blobName } = await loggerStorageAccountTransport(
    context,
    getBlobName(context, 'logs.txt', pkg)
  );
  pkg.logsBlobUrl = `${blobName}`;
  context.logger.add(blobTransport);
  context.logger.info(`Package log to ${pkg.logsBlobUrl}`);

  const syncConfig = workflowPkgGetSyncConfig(context, pkg);
  const pushBranchPromise = (await workflowPkgUpdateBranch(context, pkg, syncConfig))();

  await workflowPkgCallBuildScript(context, pkg);
  await workflowPkgCallChangelogScript(context, pkg);
  await workflowPkgDetectArtifacts(context, pkg);
  await workflowPkgSaveSDKArtifact(context, pkg);
  await workflowPkgSaveApiViewArtifact(context, pkg);
  await workflowPkgCallInstallInstructionScript(context, pkg, syncConfig);
  await pushBranchPromise;
  await workflowPkgUpdatePR(context, pkg, syncConfig);

  setSdkAutoStatus(pkg, 'succeeded');

  context.logger.remove(blobTransport);
  context.logger.remove(captureTransport);
  context.logger.log('endsection', `Handle package ${pkg.name}`);
};

const workflowPkgCallBuildScript = async (context: WorkflowContext, pkg: PackageData) => {
  const runOptions = context.swaggerToSdkConfig.packageOptions.buildScript;
  if (!runOptions) {
    await legacyBuildPackage(context, pkg);
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
  let searchOption = context.swaggerToSdkConfig.artifactOptions.artifactPathFromFileSearch;
  if (searchOption === undefined) {
    searchOption = legacyArtifactSearchOption(context, pkg);
    if (searchOption) {
      context.logger.info(`Use legacy artifact search option`);
    }
  }

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
    specCommitSha: context.specCommitSha,
    language,
    artifactName
  };
  fs.writeFileSync(path.join(destination, `_meta_${fileName}.json`), JSON.stringify(apiViewArtifactMeta, undefined, 2));
};

const fileInstallInstructionInput = 'installInstructionInput.json';
const fileInstallInstructionOutput = 'installInstructionOutput.json';
const workflowPkgCallInstallInstructionScript = async (
  context: WorkflowContext,
  pkg: PackageData,
  syncConfig: SyncConfig
) => {
  const runOptions = context.swaggerToSdkConfig.artifactOptions.installInstructionScript;
  if (!runOptions) {
    const legacyResult = await legacyInstallInstruction(context, pkg, syncConfig);
    if (legacyResult === undefined) {
      context.logger.info('Skip installInstructionScript');
    } else {
      context.logger.info(`Legacy InstallInstruction`);
      pkg.installationInstructions = legacyResult.full;
      pkg.liteInstallationInstruction = legacyResult.lite;
    }
    return;
  }

  context.logger.log('section', 'Call InstallInstructionScript');

  const input: InstallInstructionScriptInput = {
    isPublic: context.config.storage.isPublic,
    downloadUrlPrefix: `${getBlobName(context, '', pkg)}`,
    downloadCommandTemplate: context.config.storage.downloadCommand,
    packageName: pkg.name,
    artifacts: pkg.artifactPaths.map((p) => path.basename(p)),
    trigger: context.trigger
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

  if (!context.useMergedRoutine) {
    config.baseRepo = context.sdkRepoConfig.integrationRepository;
    config.baseRemote = remoteIntegration;
    config.targetBranch = getGenerationBranchName(context, pkg.name);
  }

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

const workflowPkgUpdatePR = async (context: WorkflowContext, pkg: PackageData, syncConfig: SyncConfig) => {
  if (context.useMergedRoutine) {
    const intRepo = context.sdkRepoConfig.integrationRepository;
    const genPrHead = `${intRepo.owner}:${getGenerationBranchName(context, pkg.name)}`;
    context.logger.log('github', `Get GenerationPR and close it if exist`);
    const { data: genPr } = await context.octokit.pulls.list({
      owner: intRepo.owner,
      repo: intRepo.name,
      state: 'open',
      head: genPrHead
    });
    for (const pr of genPr) {
      context.logger.log('github', `Close GenerationPR ${repoKeyToString(intRepo)}/${pr.number}`);
      await context.octokit.pulls.update({
        owner: intRepo.owner,
        repo: intRepo.name,
        pull_number: pr.number,
        state: 'closed'
      });
    }
  }

  const head = `${syncConfig.targetRepo.owner}:${syncConfig.targetBranch}`;
  const headLabel = syncConfig.targetRepo.owner === syncConfig.baseRepo.owner ? syncConfig.targetBranch : head;

  const title = `[${context.useMergedRoutine ? 'ReleasePR' : 'AutoPR'} ${pkg.name}] ${context.specPrTitle}`;
  let body = `Create to sync ${context.specPrHtmlUrl}`;
  if (context.useMergedRoutine) {
    body = `${body}\n[ReCreate this PR](https://github.com/${syncConfig.baseRepo.name}/compare/${syncConfig.baseBranch}...${headLabel}?expand=1)`;
  }

  body = `${body}\n\n${pkg.installationInstructions ?? ''}`;
  body = `${body}\n This pull request has been automatically generated for preview purposes.`;
  context.logger.log(
    'github',
    `Get PR in ${repoKeyToString(syncConfig.baseRepo)} from ${head} to ${syncConfig.baseBranch}`
  );
  const { data: existingPrs } = await context.octokit.pulls.list({
    owner: syncConfig.baseRepo.owner,
    repo: syncConfig.baseRepo.name,
    state: context.useMergedRoutine ? 'all' : 'open',
    head,
    base: syncConfig.baseBranch,
    sort: 'created',
    direction: 'desc'
  });

  let targetPr: typeof existingPrs[0] | undefined = existingPrs[0];
  const draft =
    context.trigger === 'pullRequest'
      ? context.swaggerToSdkConfig.advancedOptions.draftGenerationPR
      : context.swaggerToSdkConfig.advancedOptions.draftIntegrationPR;
  if (targetPr !== undefined && (targetPr.merged_at === null || targetPr.merged_at === undefined)) {
    context.logger.log('github', `Update existing PR ${targetPr.html_url}`);
    await context.octokit.pulls.update({
      owner: syncConfig.baseRepo.owner,
      repo: syncConfig.baseRepo.name,
      pull_number: targetPr.number,
      title,
      body,
      maintainer_can_modify: false,
      draft
    });
    if (!syncConfig.hasChanges) {
      context.logger.log('github', 'Not showing PR in comment because there is no diff');
      targetPr = undefined;
    }
  } else if (!syncConfig.hasChanges) {
    context.logger.log('github', 'Skip creating PR because there is no diff');
    targetPr = undefined;
  } else {
    context.logger.log('github', `Create new PR`);
    const rsp = await context.octokit.pulls.create({
      owner: syncConfig.baseRepo.owner,
      repo: syncConfig.baseRepo.name,
      head: headLabel,
      base: syncConfig.baseBranch,
      title,
      body,
      maintainer_can_modify: false,
      draft
    });
    targetPr = rsp.data;
    context.logger.log('github', `PR created at ${targetPr.html_url}`);

    if (context.useMergedRoutine && context.swaggerToSdkConfig.advancedOptions.closeIntegrationPR) {
      context.logger.log('github', `Close IntegrationPR`);
      await context.octokit.pulls.update({
        owner: syncConfig.baseRepo.owner,
        repo: syncConfig.baseRepo.name,
        pull_number: targetPr.number,
        state: 'closed'
      });
    }
  }

  pkg.generationPullRequestUrl = targetPr?.html_url;
};
