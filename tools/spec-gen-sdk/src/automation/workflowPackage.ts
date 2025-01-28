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

export const workflowPkgMain = async (context: WorkflowContext, pkg: PackageData) => {
  context.logger.log('section', `Handle package ${pkg.name}`);
  context.logger.info(`Package log to a new logFile`);

  await workflowPkgCallBuildScript(context, pkg);
  await workflowPkgCallChangelogScript(context, pkg);
  await workflowPkgDetectArtifacts(context, pkg);
  await workflowPkgSaveSDKArtifact(context, pkg);
  await workflowPkgSaveApiViewArtifact(context, pkg);
  await workflowPkgCallInstallInstructionScript(context, pkg);

  setSdkAutoStatus(pkg, 'succeeded');
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
    const result = await runSdkAutoCustomScript(context, runOptions, {
      cwd: context.sdkFolder,
      fallbackName: 'Changelog',
      argList: [pkg.relativeFolderPath, ...pkg.extraRelativeFolderPaths],
      statusContext: pkg
    });

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
 * Copy sdk artifact path to {work_dir}/out/generatedSdkArtifacts
 */
const workflowPkgSaveSDKArtifact = async (context: WorkflowContext, pkg: PackageData) => {
  context.logger.info(`Save ${pkg.artifactPaths.length} artifact to Azure devOps.`);
  const language = pkg.language ?? getLanguageByRepoName(context.sdkRepoConfig.mainRepository.name);
  console.log(`##vso[task.setVariable variable=sdkLanguage]${language}`);

  // if no artifact generated or language is Go, skip
  if (pkg.artifactPaths.length === 0 || language.toLocaleLowerCase() === 'go') { 
    return; 
  }
  
  const destination = path.join(context.config.workingFolder, 'out/generatedSdkArtifacts');
  if (!existsSync(destination)) {
    fs.mkdirSync(destination, { recursive: true });
  }
  context.sdkArtifactFolder = destination;
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
  const destination = path.join(context.config.workingFolder, 'out/sdkApiViewArtifacts');
  if (!existsSync(destination)) {
    fs.mkdirSync(destination, { recursive: true });
  }
  context.sdkApiViewArtifactFolder = destination;
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
    artifacts: pkg.artifactPaths.map((p) => path.basename(p))
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

