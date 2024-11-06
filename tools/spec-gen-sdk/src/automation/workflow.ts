import * as path from 'path';
import * as _ from 'lodash';
import * as fs from 'fs';
import { mkdirpSync } from 'fs-extra';
import { default as Transport } from 'winston-transport';
import { findSDKToGenerateFromTypeSpecProject } from '../utils/typespecUtils';
import { SdkAutoContext } from './entrypoint';
import simpleGit, { SimpleGit, SimpleGitOptions } from 'simple-git';
import {
  gitAddAll,
  gitCheckoutBranch,
  gitGetCommitter,
  gitGetDiffFileList,
  gitRemoveAllBranches,
  gitSetRemoteWithAuth
} from '../utils/gitUtils';
import { getSdkRepoConfig, getSpecConfig, SdkRepoConfig, specConfigPath } from '../types/SpecConfig';
import { getGithubFileContent, RepoKey, repoKeyToString } from '../utils/githubUtils';
import { getSwaggerToSdkConfig, SwaggerToSdkConfig } from '../types/SwaggerToSdkConfig';
import { runSdkAutoCustomScript, setSdkAutoStatus } from '../utils/runScript';
import {
  deleteTmpJsonFile,
  readTmpJsonFile,
  searchRelatedTypeSpecProjectBySharedLibrary,
  searchRelatedParentFolders,
  searchSharedLibrary,
  writeTmpJsonFile
} from '../utils/fsUtils';
import { GenerateInput } from '../types/GenerateInput';
import { legacyGenerate, legacyInit } from './legacy';
import { GenerateOutput, getGenerateOutput } from '../types/GenerateOutput';
import { getPackageData, PackageData } from '../types/PackageData';
import { workflowPkgMain } from './workflowPackage';
import { SDKAutomationState } from '../sdkAutomationState';
import { CommentCaptureTransport, getBlobName } from './logging';
import { findSwaggerToSDKConfiguration } from '@ts-common/azure-js-dev-tools';
import { SwaggerToSDKConfiguration as LegacySwaggerToSdkConfig } from '../swaggerToSDKConfiguration';
import { ReadmeMdFileProcessMod } from '../langSpecs/languageConfiguration';
import { getInitOutput } from '../types/InitOutput';
import { MessageRecord, sdkSuppressionsFileName, SdkSuppressionsYml, SdkPackageSuppressionsEntry, parseYamlContent, validateSdkSuppressionsFile } from '@azure/swagger-validation-common';
import { removeDuplicatesFromRelatedFiles } from '../utils/utils';
import { SDKSuppressionContentList } from '../utils/handleSuppressionLines';

export const remoteIntegration = 'integration';
export const remoteMain = 'main';
export const remoteSecondary = 'secondary';

export const branchSdkGen = 'sdkGen';
export const branchMain = 'main';
export const branchSecondary = 'secondary';
export const branchBase = 'base';

type ChangedSpecs = {
  [K in "readmeMd" | "typespecProject"]?: string;
} & {
  suppressionFile: string | undefined;
  specs: string[];
};

export const simpleGitOptions: SimpleGitOptions = {
  baseDir: process.cwd(),
  binary: 'git',
  maxConcurrentProcesses: 4
} as SimpleGitOptions;

export enum FailureType {
  CodegenFailed = 'Code Generator Failed',
  PipelineFrameworkFailed = 'Pipeline Framework Failed'
}

export type WorkflowContext = SdkAutoContext & {
  sdkRepoConfig: SdkRepoConfig;
  swaggerToSdkConfig: SwaggerToSdkConfig;
  specRepo: SimpleGit;
  specFolder: string;
  sdkRepo: SimpleGit;
  sdkFolder: string;
  pendingPackages: PackageData[];
  handledPackages: PackageData[];
  status: SDKAutomationState;
  failureType?: FailureType;
  messages: string[];
  messageCaptureTransport: Transport;
  legacyAfterScripts: string[];
  scriptEnvs: { [key: string]: string | undefined };
  tmpFolder: string;
  skipLegacy: boolean;
  extraResultRecords: MessageRecord[];
};

export const setFailureType = (context: WorkflowContext, failureType: FailureType) => {
  if (context.failureType !== FailureType.CodegenFailed) {
    context.failureType = failureType;
  }
};

export const workflowInit = async (context: SdkAutoContext): Promise<WorkflowContext> => {
  const messages = [];
  const captureTransport = new CommentCaptureTransport({
    extraLevelFilter: ['command', 'error', 'warn'],
    level: 'debug',
    output: messages
  });
  context.logger.add(captureTransport);

  const configsPromise = (await workflowInitConfig(context))();
  const specContextPromise = (await workflowInitSpecRepo(context))();

  const configs = await configsPromise;
  const sdkContext = await workflowInitSdkRepo(context, configs.sdkRepoConfig, configs.swaggerToSdkConfig);

  const tmpFolder = path.join(context.workingFolder, `${configs.sdkRepoConfig.mainRepository.name}_tmp`);
  mkdirpSync(tmpFolder);

  const skipLegacy = configs.swaggerToSdkConfig.generateOptions.generateScript !== undefined;

  const specContext = await specContextPromise;
  return {
    ...context,
    ...configs,
    ...specContext,
    ...sdkContext,
    pendingPackages: [],
    handledPackages: [],
    extraResultRecords: [],
    status: 'inProgress',
    messages,
    messageCaptureTransport: captureTransport,
    legacyAfterScripts: [],
    tmpFolder,
    skipLegacy,
    scriptEnvs: {
      USER: process.env.USER,
      HOME: process.env.HOME,
      PATH: process.env.PATH,
      SHELL: process.env.SHELL,
      NODE_OPTIONS: process.env.NODE_OPTIONS,
      TMPDIR: path.resolve(tmpFolder)
    }
  };
};

export const workflowMain = async (context: WorkflowContext) => {
  await workflowCallInitScript(context);
  const changedSpecs = await workflowDetectChangedSpec(context);
  context.logger.remove(context.messageCaptureTransport);

  const callMode =
    context.swaggerToSdkConfig.advancedOptions.generationCallMode ??
    context.legacyLangConfig?.readmeMdFileProcessMod ??
    'one-for-all-configs';
  if (callMode === 'one-for-all-configs' || callMode === ReadmeMdFileProcessMod.Batch) {
    await workflowHandleReadmeMdOrTypeSpecProject(context, changedSpecs);
  } else {
    for (const changedSpec of changedSpecs) {
      await workflowHandleReadmeMdOrTypeSpecProject(context, [changedSpec]);
    }
  }
  setSdkAutoStatus(context, 'succeeded');
};

export const workflowFilterSdkMain = async (context: SdkAutoContext) => {
  const specConfigContentPromise = workflowInitGetSpecConfig(context);
  const specContextPromise = (await workflowInitSpecRepo(context, { checkoutMainBranch: false }))();
  const specContext = await specContextPromise;
  const specConfigContent = await specConfigContentPromise;
  const specConfig = getSpecConfig(specConfigContent, context.config.specRepo);
  const changedSpecs = await workflowDetectChangedSpec({ ...context, ...specContext });

  context.logger.log('section', 'Filter SDK to generate');
  const sdkToGenerate = new Set<string>();

  const commit = await specContext.specRepo.revparse(branchMain);
  for (const ch of changedSpecs) {
    if (ch.typespecProject) {
      const entry = await specContext.specRepo.revparse(`${commit}:${ch.typespecProject}`);
      const blob = await specContext.specRepo.catFile(['-p', entry]);
      const content = blob.toString();
      const config = findSDKToGenerateFromTypeSpecProject(content, specConfig);
      if (!config || config.length === 0) {
        context.logger.warn(`Warning: cannot find supported emitter in tspconfig.yaml for typespec project ${ch.typespecProject}. This typespec project will be skipped from SDK generation. Please add the right emitter config in the 'tspconfig.yaml' file. The example project can be found at https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml.`);
        continue;
      }
      for (const repo of config) {
        sdkToGenerate.add(repo);
      }
    } else if (ch.readmeMd) {
      const entry = await specContext.specRepo.revparse(`${commit}:${ch.readmeMd}`);
      const blob = await specContext.specRepo.catFile(['-p', entry]);
      const content = blob.toString();
      const config = findSwaggerToSDKConfiguration(content);
      if (!config || config.repositories.length === 0) {
        context.logger.warn(`Warning: 'swagger-to-sdk' section cannot be found in ${ch.readmeMd}. This readme file will be skipped from SDK generation. Please add the section to the readme file according to this guidance https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/code-gen/configure-go-sdk.md#swagger-to-sdk.`);
        continue;
      }
      for (const repoConfig of config.repositories) {
        sdkToGenerate.add(repoConfig.repo);
      }
    }
  }

  context.logger.info(`SDK to generate:`);
  const enabledJobs: { [sdkName: string]: { sdkName: string } } = {};
  for (const sdkName of [...sdkToGenerate]) {
    if (specConfig.sdkRepositoryMappings[sdkName] === undefined) {
      context.logger.warn(`\tWarning: ${sdkName} not found in ${specConfigPath}. This SDK will be skipped from SDK generation. Please add the right config to the readme file according to this guidance https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/code-gen/configure-go-sdk.md#swagger-to-sdk.`);
      continue;
    }
    context.logger.info(`\t${sdkName}`);
    enabledJobs[sdkName] = { sdkName };
  }
  console.log(`##vso[task.setVariable variable=EnabledJobs;isOutput=true]${JSON.stringify(enabledJobs)}`);
  if (sdkToGenerate.size === 0 || Object.keys(enabledJobs).length === 0) {
    console.log(`##vso[task.setVariable variable=SkipAll;isOutput=true]true`);
  }

  const skipJobs: string[] = Object.keys(specConfig.sdkRepositoryMappings).filter(
    (sdkName) => !sdkToGenerate.has(sdkName)
  );
  console.log(`##vso[task.setVariable variable=SkippedJobs]${skipJobs.join(' ')}`);
  console.log(`##vso[task.setVariable variable=QueueJobs]${Object.keys(enabledJobs).join(' ')}`);

  context.logger.log('endsection', 'Filter SDK to generate');
};

const workflowHandleReadmeMdOrTypeSpecProject = async (context: WorkflowContext, changedSpecs: ChangedSpecs[]) => {
  context.logger.add(context.messageCaptureTransport);
  context.logger.log('info', `Handle the following readme.md or typespec project:`);
  const changedFilesSet = new Set<string>();
  const readmeMdList: string[] = [];
  const typespecProjectList: string[] = [];
  const suppressionFileMap: Map<string, string|undefined> = new Map();

  const specConfigContent = await workflowInitGetSpecConfig(context);
  const specConfig = getSpecConfig(specConfigContent, context.config.specRepo);
  for (const changedSpec of changedSpecs) {
    if (changedSpec.typespecProject) {
      let content: string | undefined = undefined;
      try {
        content = fs.readFileSync(path.join(context.specFolder, changedSpec.typespecProject!)).toString();
      } catch (error) {
        const typespecPath = `${path.join(context.specFolder, changedSpec.typespecProject!)}`;
        context.logger.error(`IOError: Fails to read typespec file with path of '${typespecPath}'. Skipping the typespec case and continue the run. Please ensure the typespec exists with the correct path. Error: ${error.message}`);
      }
      const config = findSDKToGenerateFromTypeSpecProject(content, specConfig)?.filter(
        (r) => r === context.config.sdkName
      )[0];
      if (config === undefined || config.length === 0) {
        context.logger.warn(
          `\tWarning: cannot find emitter config for ${context.config.sdkName} in tspconfig.yaml for typespec project ${changedSpec.typespecProject}. This SDK will be skipped from the generation for this project. Please add the right emitter config in the 'tspconfig.yaml' file. The example project can be found at https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml.`
        );
        continue;
      }
      context.logger.info(`\t${changedSpec.typespecProject}`);
      typespecProjectList.push(changedSpec.typespecProject!.replace('/tspconfig.yaml', ''));
      suppressionFileMap.set(changedSpec.typespecProject!.replace('/tspconfig.yaml', ''), changedSpec.suppressionFile);
    } else if (changedSpec.readmeMd) {
      let content: string | undefined = undefined;
      try {
        content = fs.readFileSync(path.join(context.specFolder, changedSpec.readmeMd!)).toString();
      } catch (error) {
        context.logger.error(`IOError: Fails to read readme file with path of '${path.join(context.specFolder, changedSpec.readmeMd!)}'. Skipping the swagger case and continue the run. Please ensure the readme exists with the correct path. Error: ${error.message}`);
      }
      const confSection = findSwaggerToSDKConfiguration(content);
      const config = confSection?.repositories.filter((r) => r.repo === context.config.sdkName)[0];
      if (config === undefined) {
        context.logger.warn(`\tWarning: ${context.config.sdkName} cannot be found in ${changedSpec.readmeMd}. This SDK will be skipped from SDK generation. Please add the right config to the readme file according to this guidance https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/code-gen/configure-go-sdk.md#swagger-to-sdk.`);
        continue;
      }
      context.legacyAfterScripts.push(...(config.after_scripts ?? []));
      context.logger.info(`\t${changedSpec.readmeMd}`);
      readmeMdList.push(changedSpec.readmeMd!);
      suppressionFileMap.set(changedSpec.readmeMd!, changedSpec.suppressionFile);
    }
    // Avoid the null error when both readme.md and tsp-config.yaml don't exist
    if (changedSpec.specs) {
      for (const filePath of changedSpec.specs) {
        changedFilesSet.add(filePath);
      }
    }
  }

  if (typespecProjectList.length === 0 && readmeMdList.length === 0) {
    context.logger.remove(context.messageCaptureTransport);
    return;
  }

  const changedFiles = [...changedFilesSet];

  const headCommit = await context.sdkRepo.revparse(branchMain);

  context.logger.log('git', `Checkout branch ${branchSdkGen}`);
  await gitCheckoutBranch(context, context.sdkRepo, branchMain);
  await context.sdkRepo.raw(['branch', branchSdkGen, headCommit, '--force']);
  await gitCheckoutBranch(context, context.sdkRepo, branchSdkGen);

  const { status, generateInput, generateOutput } = await workflowCallGenerateScript(
    context,
    changedFiles,
    readmeMdList,
    typespecProjectList
  );
  
  if (!generateOutput && status === 'failed') {
    context.logger.warn('Warning: Package processing is skipped as the SDK generation fails. Please look into the above generation errors or report this issue through https://aka.ms/azsdk/support/specreview-channel.');
    context.logger.remove(context.messageCaptureTransport);
    return;
  }

  // Get suppression files from formatting generateInput
  // filter suppressionFileMap with generateInput readmeMdList and typespecProjectList
  const filterGenerateFiles = [...generateInput.relatedTypeSpecProjectFolder || [], ...generateInput.relatedReadmeMdFiles || []]
  const filterSuppressionFileMap: Map<string, string|undefined> = new Map(
    Array.from(suppressionFileMap).filter(([key]) => filterGenerateFiles.includes(key))
  );

  const sdkSuppressionsYml = await workflowInitGetSdkSuppressionsYml(context, filterSuppressionFileMap);
  context.pendingPackages =
    (generateOutput?.packages ?? []).map((result) => getPackageData(context, result, sdkSuppressionsYml));

  const fileList = await workflowSaveGenerateResult(context);
  await workflowDetectChangedPackages(context, fileList, readmeMdList);

  context.logger.remove(context.messageCaptureTransport);
  for (const pkg of context.pendingPackages) {
    await workflowSetSdkRemoteAuth(context);
    await workflowPkgMain(context, pkg);
  }

  context.handledPackages.push(...context.pendingPackages);
  context.pendingPackages = [];
};

export const workflowInitGetSpecConfig = async (context: SdkAutoContext) => {
  context.logger.log('github', `Get ${specConfigPath} from ${repoKeyToString(context.config.specRepo)}`);
  const fileContent = await getGithubFileContent(context, context.config.specRepo, specConfigPath);
  return fileContent;
};

export const workflowInitGetSdkSuppressionsYml = async (
  context: WorkflowContext,
  filterSuppressionFileMap: Map<string, string|undefined>
): Promise<SDKSuppressionContentList> => {

  context.logger.info(
    `Get file content from ${repoKeyToString(context.config.specRepo)} ` +
    `for following SDK suppression files: ${Array.from(filterSuppressionFileMap.values()).join(',')} `
  );

  const suppressionFileMap: SDKSuppressionContentList = new Map();
  for (const [changedSpecFilePath, sdkSuppressionFilePath] of filterSuppressionFileMap) {
    context.logger.info(`${changedSpecFilePath} corresponding SDK suppression files ${sdkSuppressionFilePath}.`);
    if(!sdkSuppressionFilePath){
      suppressionFileMap.set(changedSpecFilePath, {content: null, sdkSuppressionFilePath, errors: ['No suppression file added.']});
      continue;
    }
    // Retrieve the data of all the files that suppress certain content for a language
    const sdkSuppressionFilesContentTotal: SdkSuppressionsYml = { suppressions: {} };
    // Use file parsing to obtain yaml content and check if the suppression file has any grammar errors
    const sdkSuppressionFilesParseErrorTotal: string[] = [];
    let suppressionFileData: string = '';
    const filePath = path.join(context.specFolder, sdkSuppressionFilePath);
    try {
      suppressionFileData = fs.readFileSync(filePath).toString();
    } catch (error) {
      context.logger.error(`IOError: Fails to read SDK suppressions file with path of '${sdkSuppressionFilePath}'. Assuming no suppressions are present. Please ensure the suppression file exists in the right path in order to load the suppressions for the SDK breaking changes. Error: ${error.message}`);
      continue;
    }
    // parse file both to get yaml content and validate the suppression file has grammar error
    const suppressionFileParseResult = parseYamlContent(suppressionFileData, filePath);
    if (!suppressionFileParseResult.result) {
      sdkSuppressionFilesParseErrorTotal.push(suppressionFileParseResult.message);
      continue;
    }
    const suppressionFileContent = suppressionFileParseResult.result as SdkSuppressionsYml;
    // Check if the suppression file content has any schema error
    const validateSdkSuppressionsFileResult = validateSdkSuppressionsFile(suppressionFileContent);
    if (!validateSdkSuppressionsFileResult.result) {
      sdkSuppressionFilesParseErrorTotal.push(validateSdkSuppressionsFileResult.message)
      context.logger.error(`ContentError: ${validateSdkSuppressionsFileResult.message}. The SDK suppression file is malformed. Please refer to the https://aka.ms/azsdk/sdk-suppression to fix the content.`);
      continue;
    }
    sdkSuppressionFilesContentTotal.suppressions = _.mergeWith(suppressionFileContent.suppressions, sdkSuppressionFilesContentTotal.suppressions,
      function customizer(originSdkPackageSuppression: SdkPackageSuppressionsEntry, othersSdkPackageSuppression: SdkPackageSuppressionsEntry) {
        if (_.isArray(originSdkPackageSuppression)) {
          return originSdkPackageSuppression.concat(othersSdkPackageSuppression)
        }
      })
    suppressionFileMap.set(changedSpecFilePath, {content: sdkSuppressionFilesContentTotal, sdkSuppressionFilePath, errors: sdkSuppressionFilesParseErrorTotal })
  }

  return suppressionFileMap;
};

export const workflowInitGetDefaultBranch = async (context: SdkAutoContext, repo: RepoKey) => {
  const rsp = await context.octokit.repos.get({
    owner: repo.owner,
    repo: repo.name
  });

  return rsp.data.default_branch;
};

const workflowInitConfig = async (context: SdkAutoContext) => {
  const sdkRepoConfig = await getSdkRepoConfig(context);

  context.logger.info(`mainRepository: ${repoKeyToString(sdkRepoConfig.mainRepository)}`);
  context.logger.info(`mainBranch: ${sdkRepoConfig.mainBranch}`);
  context.logger.info(`integrationRepository: ${repoKeyToString(sdkRepoConfig.integrationRepository)}`);
  context.logger.info(`integrationBranchPrefix: ${sdkRepoConfig.integrationBranchPrefix}`);
  context.logger.info(`secondaryRepository: ${repoKeyToString(sdkRepoConfig.secondaryRepository)}`);
  context.logger.info(`secondaryBranch: ${sdkRepoConfig.secondaryBranch}`);

  context.logger.log(
    'github',
    `Get ${sdkRepoConfig.configFilePath} from ${repoKeyToString(sdkRepoConfig.mainRepository)}`
  );
  return async () => {
    const fileContent = await getGithubFileContent(
      context,
      sdkRepoConfig.mainRepository,
      sdkRepoConfig.configFilePath,
      sdkRepoConfig.mainBranch
    );

    if (!fileContent) {
      throw new Error(`ConfigError: ${repoKeyToString(sdkRepoConfig.mainRepository)} ${sdkRepoConfig.configFilePath} doesn't exist. Please refer to the https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/samplefiles/README.md#swagger-to-sdk sample file to add the right configuration.`);
    }

    const swaggerToSdkConfig = getSwaggerToSdkConfig(fileContent);

    return { sdkRepoConfig, swaggerToSdkConfig };
  };
};

const workflowInitSpecRepo = async (
  context: SdkAutoContext,
  opts: { checkoutMainBranch: boolean } = { checkoutMainBranch: true }
) => {
  const specFolder = path.join(context.workingFolder, context.config.specRepo.name);
  mkdirpSync(specFolder);

  const specRepo = simpleGit({ ...simpleGitOptions, baseDir: path.resolve(process.cwd(), specFolder) });
  await specRepo.init(false);

  await gitSetRemoteWithAuth(context, specRepo, remoteMain, context.config.specRepo);

  await gitRemoveAllBranches(context, specRepo);

  let refSpec = `pull/${context.config.pullNumber}/merge`;
  let fetchResult;
  let toFetch: string[];
  if (context.trigger === 'pullRequest') {
    toFetch = [
      `+refs/${refSpec}:refs/heads/${branchMain}`,
      `+refs/heads/${context.specPrBaseBranch}:refs/heads/${branchBase}`
    ];
  } else {
    refSpec = `heads/${context.specPrBaseBranch}`;
    toFetch = [`+refs/${refSpec}:refs/${refSpec}`];
  }

  context.logger.log('git', `Fetch ${refSpec} on ${repoKeyToString(context.config.specRepo)}`);

  return async () => {
    fetchResult = await specRepo.fetch([remoteMain, '--update-head-ok', '--force', ...toFetch]);
    if (!fetchResult) {
      throw new Error(`Error: Failed to fetch spec repo. Code: ${fetchResult}. Please re-run the failed job in pipeline run or emit '/azp run' in the PR comment to trigger the re-run if the error is retryable`);
    }

    if (context.trigger === 'pullRequest') {
      const headSha = await specRepo.revparse(branchMain);
      if (headSha !== context.specCommitSha) {
        if (context.config.runEnv !== 'test') {
          context.logger.warn(`Warning: HeadSha mismatches. Update to ${headSha}.`);
        }
        context.specCommitSha = headSha;
      }
    } else {
      const headCommit = await specRepo.show([context.specCommitSha]);
      await specRepo.raw(['branch', '--copy', headCommit, branchMain, '--force']);
      const baseCommit = await specRepo.revparse('HEAD');
      await specRepo.raw(['branch', '--copy', baseCommit, branchBase, '--force']);
    }

    if (opts.checkoutMainBranch) {
      await gitCheckoutBranch(context, specRepo, branchMain);
    }

    return { specFolder, specRepo };
  };
};

const workflowSetSdkRemoteAuth = async (
  context: Pick<WorkflowContext, 'sdkRepo' | 'sdkRepoConfig' | 'logger' | 'getGithubAccessToken'>
) => {
  const { sdkRepo, sdkRepoConfig } = context;
  await gitSetRemoteWithAuth(context, sdkRepo, remoteMain, sdkRepoConfig.mainRepository);
  await gitSetRemoteWithAuth(context, sdkRepo, remoteSecondary, sdkRepoConfig.secondaryRepository);
  await gitSetRemoteWithAuth(context, sdkRepo, remoteIntegration, sdkRepoConfig.integrationRepository);
};

const workflowInitSdkRepo = async (
  context: SdkAutoContext,
  sdkRepoConfig: SdkRepoConfig,
  swaggerToSdkConfig: SwaggerToSdkConfig
) => {
  const cloneDir =
    swaggerToSdkConfig.advancedOptions.cloneDir ??
    (swaggerToSdkConfig as LegacySwaggerToSdkConfig).meta?.advanced_options?.clone_dir;
  const sdkFolderName = cloneDir
    ? path.join(sdkRepoConfig.mainRepository.name, cloneDir)
    : sdkRepoConfig.mainRepository.name;
  const sdkFolder = path.join(context.workingFolder, sdkFolderName);
  mkdirpSync(sdkFolder);

  const sdkRepo = simpleGit({ ...simpleGitOptions, baseDir: path.resolve(process.cwd(), sdkFolder) });
  await sdkRepo.init(false);

  await workflowSetSdkRemoteAuth({ ...context, sdkRepo, sdkRepoConfig });
  await gitRemoveAllBranches(context, sdkRepo);

  context.logger.log('git', `Fetch ${repoKeyToString(sdkRepoConfig.mainRepository)} to ${branchMain}`);

  let fetchResult = await sdkRepo.fetch([
    branchMain,
    '--update-head-ok',
    '--force',
    `+refs/heads/${sdkRepoConfig.mainBranch}:refs/heads/${branchMain}`
  ]);
  if (!fetchResult) {
    throw new Error(`Error: Failed to fetch spec repo. Code: ${fetchResult}. Please re-run the failed job in pipeline run or emit '/azp run' in the PR comment to trigger the re-run if the error is retryable.`);
  }

  context.logger.log('git', `Checkout ${branchMain}`);
  await gitCheckoutBranch(context, sdkRepo, branchMain);

  if (
    sdkRepoConfig.secondaryRepository.name === sdkRepoConfig.mainRepository.name &&
    sdkRepoConfig.secondaryRepository.owner === sdkRepoConfig.mainRepository.owner &&
    sdkRepoConfig.secondaryBranch === sdkRepoConfig.mainBranch
  ) {
    context.logger.log('git', `Checkout ${branchSecondary} from ${branchMain}`);
    await sdkRepo.raw(['branch', '--copy', '--force', branchMain, branchSecondary]);
  } else {
    context.logger.log('git', `Fetch ${repoKeyToString(sdkRepoConfig.secondaryRepository)} to ${branchSecondary}`);

    fetchResult = await sdkRepo.fetch([
      branchSecondary,
      '--update-head-ok',
      '--force',
      `+refs/heads/${sdkRepoConfig.secondaryBranch}:refs/heads/${branchSecondary}`
    ]);
    if (!fetchResult) {
      throw new Error(`Error: Failed to fetch spec repo. Code: ${fetchResult}. Please re-run the failed job in pipeline run or emit '/azp run' in the PR comment to trigger the re-run if the error is retryable.`);
    }
  }

  return { sdkRepo, sdkFolder };
};

const fileInitInput = 'initInput.json';
const fileInitOutput = 'initOutput.json';
const workflowCallInitScript = async (context: WorkflowContext) => {
  const initScriptConfig = context.swaggerToSdkConfig.initOptions?.initScript;
  if (initScriptConfig === undefined) {
    context.logger.log('warn', `Skip initScript due to not configured`);
    await legacyInit(context);
    return;
  }

  writeTmpJsonFile(context, fileInitInput, {});
  deleteTmpJsonFile(context, fileInitOutput);

  context.logger.log('section', `Call initScript`);
  await runSdkAutoCustomScript(context, initScriptConfig, {
    cwd: context.sdkFolder,
    statusContext: context,
    argTmpFileList: [fileInitInput, fileInitOutput]
  });
  context.logger.log('endsection', `Call initScript`);

  const initOutputContent = readTmpJsonFile(context, fileInitOutput);
  if (initOutputContent !== undefined) {
    const initOutput = getInitOutput(initOutputContent);
    if (initOutput?.envs !== undefined) {
      context.scriptEnvs = { ...context.scriptEnvs, ...initOutput.envs };
    }
  }
};

const readmeMdRegex = /^readme.md$/;
const typespecProjectRegex = /^tspconfig.yaml$/;
const suppressionFileRegex = new RegExp(sdkSuppressionsFileName);
const typespecProjectSharedLibraryRegex = /[^/]+\.Shared/;
const workflowDetectChangedSpec = async (
  context: Pick<WorkflowContext, 'specRepo' | 'logger' | 'specFolder' | keyof SdkAutoContext>
) => {
  const repo = context.specRepo;
  const headCommit = await repo.revparse(branchMain);
  const baseCommit = await repo.revparse(branchBase);
  const diff = await repo.diff(['--name-status', headCommit, baseCommit]);

  const diffFileList = await gitGetDiffFileList(diff, context, 'in spec PR');
  const fileList = diffFileList.filter((p) => p.indexOf('/scenarios/') === -1);

  const treeId = await repo.revparse(`${headCommit}^{tree}`);

  context.logger.info(`Related readme.md and typespec project list:`);
  const changedSpecs: ChangedSpecs[] = [];
  const readmeMDResult = await searchRelatedParentFolders(fileList, {
    searchFileRegex: readmeMdRegex,
    repo: context.specRepo,
    specFolder: context.specFolder,
    treeId
  });
  const suppressionsResult = await searchRelatedParentFolders(fileList, {
    searchFileRegex: suppressionFileRegex,
    repo: context.specRepo,
    specFolder: context.specFolder,
    treeId
  });
  const typespecProjectResult = await searchRelatedParentFolders(fileList, {
    searchFileRegex: typespecProjectRegex,
    repo: context.specRepo,
    specFolder: context.specFolder,
    treeId
  });
  const typespecProjectSharedLibraries = await searchSharedLibrary(fileList, {
    searchFileRegex: typespecProjectSharedLibraryRegex,
    repo: context.specRepo,
    specFolder: context.specFolder,
    treeId
  });
  const typespecProjectResultSearchedBySharedLibrary = await searchRelatedTypeSpecProjectBySharedLibrary(
    typespecProjectSharedLibraries,
    {
      searchFileRegex: typespecProjectRegex,
      repo: context.specRepo,
      specFolder: context.specFolder,
      treeId
    }
  );
  for (const folderPath of Object.keys(typespecProjectResultSearchedBySharedLibrary)) {
    if (typespecProjectResult[folderPath]) {
      typespecProjectResult[folderPath] = typespecProjectResult[folderPath].concat(
        typespecProjectResultSearchedBySharedLibrary[folderPath]
      );
    } else {
      typespecProjectResult[folderPath] = typespecProjectResultSearchedBySharedLibrary[folderPath];
    }
  }
  const result = {};
  for (const folderPath of Object.keys(readmeMDResult)) {
    result[folderPath] = readmeMDResult[folderPath];
  }
  for (const folderPath of Object.keys(suppressionsResult)) {
    // Each readme.md should have corresponding suppression file in folder level
    // When swagger changed, it cannot get parent suppression file if this swagger folder has readme.md
    if (Object.keys(readmeMDResult).includes(folderPath)) { 
      result[folderPath] = suppressionsResult[folderPath]; 
    }
  }
  for (const folderPath of Object.keys(typespecProjectResult)) {
    result[folderPath] = typespecProjectResult[folderPath];
  }
  for (const folderPath of Object.keys(result)) {
    const readmeMdPath = path.join(folderPath, 'readme.md');
    const cs: ChangedSpecs = {
      readmeMd: readmeMdPath,
      suppressionFile: undefined,
      specs: readmeMDResult[folderPath]
    };

    if (typespecProjectResult[folderPath]) {
      delete cs.readmeMd;
      cs.specs = typespecProjectResult[folderPath];
      cs.typespecProject = path.join(folderPath, 'tspconfig.yaml');
      context.logger.info(`\t tspconfig.yaml file: ${cs.typespecProject}`);
    } else {
      context.logger.info(`\t readme.md file: ${readmeMdPath}`);
    }

    if (suppressionsResult[folderPath]) {
      // where suppression file exist path. It is a fixed file path, the same as the readme.md path.
      cs.suppressionFile = path.join(folderPath, sdkSuppressionsFileName);
      context.logger.info(`\t The ${cs.readmeMd ? 'readme' : 'tsp'} file corresponding ${sdkSuppressionsFileName} exists ${cs.suppressionFile}`);
    } else {
      context.logger.info(`\t The ${cs.readmeMd ? 'readme' : 'tsp'} file corresponding ${sdkSuppressionsFileName} does not exist ${cs.suppressionFile}`);
    }

    changedSpecs.push(cs);
  }

  return changedSpecs;
};

const fileGenerateInput = 'generateInput.json';
const fileGenerateOutput = 'generateOutput.json';
const workflowCallGenerateScript = async (
  context: WorkflowContext,
  changedFiles: string[],
  relatedReadmeMdFiles: string[],
  relatedTypeSpecProjectFolder: string[]
) => {
  const statusContext = { status: 'succeeded' as SDKAutomationState };
  let generateOutput: GenerateOutput | undefined = undefined;

  const generateInput: GenerateInput = {
    dryRun: false,
    specFolder: path.relative(context.sdkFolder, context.specFolder),
    headSha: context.specCommitSha,
    headRef: context.specHeadRef,
    repoHttpsUrl: context.specHtmlUrl,
    trigger: context.trigger,
    changedFiles,
    installInstructionInput: {
      isPublic: context.config.storage.isPublic,
      downloadUrlPrefix: `${getBlobName(context, '')}`,
      downloadCommandTemplate: context.config.storage.downloadCommand,
      trigger: context.trigger
    },
    autorestConfig: context.autorestConfig
  };

  if (context.swaggerToSdkConfig.generateOptions.generateScript === undefined) {
    // Fallback to legacy autorest
    try {
      await legacyGenerate(context, relatedReadmeMdFiles, statusContext);
    } catch (e) {
      setFailureType(context, FailureType.CodegenFailed);
      throw e;
    }
    setSdkAutoStatus(context, statusContext.status);
    return { ...statusContext, generateInput, generateOutput };
  }

  context.logger.log('section', 'Call generateScript');


  if (relatedTypeSpecProjectFolder?.length > 0) {
    generateInput.relatedTypeSpecProjectFolder = relatedTypeSpecProjectFolder;
  }

  // Duplicate relatedTypeSpecProjectFolder and relatedReadmeMdFiles paths files to avoid generate twice
  // If path is both in relatedTypeSpecProjectFolder and relatedReadmeMdFiles, it will be keep relatedTypeSpecProjectFolder and removed from relatedReadmeMdFiles
  if (relatedReadmeMdFiles?.length > 0) {
    const filteredReadmeMdFiles = removeDuplicatesFromRelatedFiles(relatedTypeSpecProjectFolder, relatedReadmeMdFiles, context);
    if (filteredReadmeMdFiles && filteredReadmeMdFiles?.length > 0) {
      generateInput.relatedReadmeMdFiles = filteredReadmeMdFiles;
    }
  }

  writeTmpJsonFile(context, fileGenerateInput, generateInput);
  deleteTmpJsonFile(context, fileGenerateOutput);

  await runSdkAutoCustomScript(context, context.swaggerToSdkConfig.generateOptions.generateScript, {
    cwd: context.sdkFolder,
    argTmpFileList: [fileGenerateInput, fileGenerateOutput],
    statusContext
  });
  context.logger.log('endsection', 'Call generateScript');

  setSdkAutoStatus(context, statusContext.status);

  const generateOutputJson = readTmpJsonFile(context, fileGenerateOutput);
  if (generateOutputJson !== undefined) {
    generateOutput = getGenerateOutput(generateOutputJson);
  } else {
    return { ...statusContext, generateInput, generateOutput };
  }

  /**
   * When the changedSpec involves multiple services, the callGenerateScript function will yield a larger set of packages in the generateOutput. 
   * Within these packages, some may lack a packageName, exemplified by {"path": [ ""],"result": "failed"}, leading to a failed status. 
   * However, it's crucial to proceed with processing the remaining packages. 
   * Therefore, we should exclude any packages with empty or invalid packageName entries, log the error, and continue with the process.
   * Conversely, if the generateOutput package is an empty array, we should halt the process and log the error.
   */
  generateOutput.packages = (generateOutput.packages ?? []).filter(item => !!item.packageName);

  return { ...statusContext, generateInput, generateOutput };
};

const workflowSaveGenerateResult = async (context: WorkflowContext) => {
  context.logger.log('section', 'Commit generate result');
  context.logger.log('git', 'Add * in SDK repo');
  await gitAddAll(context.sdkRepo);

  const diff = await context.sdkRepo.diff(['--name-status', 'HEAD']);
  const fileList = await gitGetDiffFileList(diff, context, 'after SDK generate');
  if (fileList.length === 0) {
    context.logger.warn('Warning: No file changes detected after the generation. Please refer to the generation errors to understand the reasons.');
    setSdkAutoStatus(context, 'warning');
  }

  context.logger.log('git', 'Commit all the changes');
  await gitGetCommitter(context.sdkRepo);
  await context.sdkRepo.raw(['commit', '-m', 'CodeGen Result']);

  context.logger.log('endsection', 'Commit generate result');
  return fileList;
};

const workflowDetectChangedPackages = async (context: WorkflowContext, fileList: string[], readmeMdList: string[]) => {
  context.logger.log('section', 'Detect changed packages');
  if (context.pendingPackages.length === 0) {
    let searchConfig = context.swaggerToSdkConfig.packageOptions.packageFolderFromFileSearch;
    if (searchConfig === false) {
      context.logger.warn(`Warning: Skip detecting changed packages based on the config in readme.md. Please refer to the schema https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/sdkautomation/SwaggerToSdkConfigSchema.json for 'packageOptions' configuration.`);
      return;
    }
    if (searchConfig === undefined) {
      const legacyFilenameConfig = context.legacyLangConfig?.packageRootFileName;
      if (!legacyFilenameConfig) {
        throw new Error('N/A (this code has been deprecated)');
      }
      searchConfig = {
        packageNamePrefix: context.legacyLangConfig?.packageNameAltPrefix,
        searchRegex:
          typeof legacyFilenameConfig === 'string'
            ? new RegExp(`^${legacyFilenameConfig.replace('.', '\\.')}$`)
            : legacyFilenameConfig
      };
    }
    context.logger.info(`Package from changed file search: ${searchConfig.searchRegex}`);
    const packageFolderList = await searchRelatedParentFolders(fileList, {
      rootFolder: context.sdkFolder,
      searchFileRegex: searchConfig.searchRegex
    });
    for (const packageFolderPath of Object.keys(packageFolderList)) {
      let packageName = await context.legacyLangConfig?.packageNameCreator?.(
        context.sdkFolder,
        packageFolderPath,
        readmeMdList[0]
      );
      if (packageName && context.legacyLangConfig?.packageNameAltPrefix) {
        packageName = context.legacyLangConfig.packageNameAltPrefix + packageName;
      }
      context.pendingPackages.push(
        getPackageData(context, {
          packageName,
          path: [packageFolderPath],
          result: 'succeeded'
        })
      );
    }
  }

  context.logger.info(`${context.pendingPackages.length} packages found after generation:`);
  for (const pkg of context.pendingPackages) {
    context.logger.info(`\t${pkg.relativeFolderPath}`);
    for (const extraPath of pkg.extraRelativeFolderPaths) {
      context.logger.info(`\t- ${extraPath}`);
    }
  }

  if (context.pendingPackages.length === 0) {
    context.logger.warn(`Warning: No package detected after generation. Please refer to the above logs to understand why the package hasn't been generated. `);
  }
};
