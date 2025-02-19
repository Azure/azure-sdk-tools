import * as path from 'path';
import * as _ from 'lodash';
import * as fs from 'fs';
import { default as Transport } from 'winston-transport';
import { findSDKToGenerateFromTypeSpecProject } from '../utils/typespecUtils';
import simpleGit, { SimpleGit, SimpleGitOptions } from 'simple-git';
import {
  gitGetDiffFileList
} from '../utils/gitUtils';
import { specConfigPath } from '../types/SpecConfig';
import { repoKeyToString } from '../utils/repo';
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
import { GenerateOutput, getGenerateOutput } from '../types/GenerateOutput';
import { getPackageData, PackageData } from '../types/PackageData';
import { workflowPkgMain } from './workflowPackage';
import { SDKAutomationState } from './sdkAutomationState';
import { CommentCaptureTransport } from './logging';
import { findSwaggerToSDKConfiguration } from '../utils/readme';
import { getInitOutput } from '../types/InitOutput';
import { MessageRecord } from '../types/Message';
import { sdkSuppressionsFileName, SdkSuppressionsYml, SdkPackageSuppressionsEntry, validateSdkSuppressionsFile } from '../types/sdkSuppressions';
import { parseYamlContent, removeDuplicatesFromRelatedFiles } from '../utils/utils';
import { SDKSuppressionContentList } from '../utils/handleSuppressionLines';
import { SdkAutoContext } from './entrypoint';

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
  specRepo: SimpleGit;
  specFolder: string;
  sdkRepo: SimpleGit;
  sdkFolder: string;
  sdkArtifactFolder?: string;
  sdkApiViewArtifactFolder?: string;
  pendingPackages: PackageData[];
  handledPackages: PackageData[];
  status: SDKAutomationState;
  failureType?: FailureType;
  messages: string[];
  messageCaptureTransport: Transport;
  scriptEnvs: { [key: string]: string | undefined };
  tmpFolder: string;
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

  const specContext = workflowInitSpecRepo(context);
  const sdkContext = workflowInitSdkRepo(context);

  const tmpFolder = path.join(context.config.workingFolder, `${context.sdkRepoConfig.mainRepository.name}_tmp`);
  fs.mkdirSync(tmpFolder, { recursive: true });

  return {
    ...context,
    ...specContext,
    ...sdkContext,
    pendingPackages: [],
    handledPackages: [],
    extraResultRecords: [],
    status: 'inProgress',
    messages,
    messageCaptureTransport: captureTransport,
    tmpFolder,
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
  if (context.config.pullNumber) {
    const changedSpecs = await workflowDetectChangedSpec({ ...context });
    await workflowValidateSdkConfigForSpecPr(context, changedSpecs);
    await workflowCallInitScript(context);
    await workflowGenerateSdkForSpecPr(context, changedSpecs);
  } else {
    await workflowValidateSdkConfig(context);
    await workflowCallInitScript(context);
    await workflowGenerateSdk(context);
  }
  setSdkAutoStatus(context, 'succeeded');
};

export const workflowValidateSdkConfigForSpecPr = async (context: WorkflowContext, changedSpecs: ChangedSpecs[]) => {

  context.logger.log('section', 'Validate SDK configuration');
  const sdkToGenerate = new Set<string>();

  const commit = await context.specRepo.revparse(context.config.specCommitSha);
  for (const ch of changedSpecs) {
    if (ch.typespecProject) {
      const entry = await context.specRepo.revparse(`${commit}:${ch.typespecProject}`);
      const blob = await context.specRepo.catFile(['-p', entry]);
      const content = blob.toString();
      const config = findSDKToGenerateFromTypeSpecProject(content, context.specRepoConfig);
      // todo map the sdkName by the sdk language
      if (!config || config.length === 0 || !config.includes(context.config.sdkName)) {
        context.logger.warn(`Warning: cannot find supported emitter in tspconfig.yaml for typespec project ${ch.typespecProject}. This typespec project will be skipped from SDK generation. Please add the right emitter config in the 'tspconfig.yaml' file. The example project can be found at https://aka.ms/azsdk/sample-arm-tsproject-tspconfig`);
        continue;
      }
      sdkToGenerate.add(context.config.sdkName);
    } else if (ch.readmeMd) {
      const entry = await context.specRepo.revparse(`${commit}:${ch.readmeMd}`);
      const blob = await context.specRepo.catFile(['-p', entry]);
      const content = blob.toString();
      const config = findSwaggerToSDKConfiguration(content);
      if (!config || config.repositories.length === 0) {
        context.logger.warn(`Warning: 'swagger-to-sdk' section cannot be found in ${ch.readmeMd}. This readme file will be skipped from SDK generation. Please add the section to the readme file according to this guidance https://aka.ms/azsdk/sample-readme-sdk-config`);
        continue;
      }
      else if (!config.repositories.some(r => r.repo === context.config.sdkName)) {
        context.logger.warn(`Warning: ${context.config.sdkName} cannot be found in the 'swagger-to-sdk' section in the ${ch.readmeMd}. This SDK will be skipped from SDK generation. Please add the right config to the readme file according to this guidance https://aka.ms/azsdk/sample-readme-sdk-config`);
        continue;
      }
      sdkToGenerate.add(context.config.sdkName);
    }
  }

  if (changedSpecs.length === 0) {
    throw new Error(`No changes detected in the API specs; SDK generation skipped.`);
  }
  if (sdkToGenerate.size === 0) {
    context.status = 'notEnabled';
    throw new Error(`No SDKs are enabled for generation. Please check the configuration in the realted tspconfig.yaml or readme.md`);
  }
  context.logger.info(`SDK to generate:`);
  const enabledJobs: { [sdkName: string]: { sdkName: string } } = {};
  for (const sdkName of [...sdkToGenerate]) {
    if (context.specRepoConfig.sdkRepositoryMappings[sdkName] === undefined) {
      context.logger.warn(`\tWarning: ${sdkName} not found in ${specConfigPath}. This SDK will be skipped from SDK generation. Please add the right config to the ${specConfigPath} according to this guidance https://aka.ms/azsdk/spec-repo-config`);
      continue;
    }
    context.logger.info(`\t${sdkName}`);
    enabledJobs[sdkName] = { sdkName };
  }
  context.logger.log('endsection', 'Validate SDK config for spec PR scenario');
};

export const workflowValidateSdkConfig = async (context: WorkflowContext) => {
  context.logger.log('section', 'Validate SDK configuration');
  let sdkToGenerate = "";

  let tspConfigPath, readmeMdPath;
  if(context.config.tspConfigPath) {
    tspConfigPath = path.join(context.config.localSpecRepoPath, context.config.tspConfigPath);
  }
  if(context.config.readmePath) {
    readmeMdPath = path.join(context.config.localSpecRepoPath, context.config.readmePath);
  }
  
  if (!tspConfigPath && !readmeMdPath) {
    throw new Error(`ConfigError: 'tspConfigPath' and 'readmePath' are not provided. Please provide at least one of them.`);
  }
  
  if (tspConfigPath) {
    const tspConfigContent = fs.readFileSync(tspConfigPath).toString();
    const config = findSDKToGenerateFromTypeSpecProject(tspConfigContent, context.specRepoConfig);
    if (!config || config.length === 0 || !config.includes(context.config.sdkName)) {
      context.logger.warn(`Warning: cannot find supported emitter in tspconfig.yaml for typespec project ${tspConfigPath}. This typespec project will be skipped from SDK generation. Please add the right emitter config in the 'tspconfig.yaml' file. The example project can be found at https://aka.ms/azsdk/sample-arm-tsproject-tspconfig`);
    }
    else {
      sdkToGenerate = context.config.sdkName;
    }
  }
  else if (readmeMdPath) {
    const readmeContent = fs.readFileSync(readmeMdPath).toString();
    const config = findSwaggerToSDKConfiguration(readmeContent);
    if (!config || config.repositories.length === 0) {
      context.logger.warn(`Warning: 'swagger-to-sdk' section cannot be found in ${readmeMdPath}. Please add the section to the readme file according to this guidance https://aka.ms/azsdk/sample-readme-sdk-config`);
    }
    else if (!config.repositories.some(r => r.repo === context.config.sdkName)) {
      context.logger.warn(`Warning: ${context.config.sdkName} cannot be found in the 'swagger-to-sdk' section in the ${readmeMdPath}. Please add the right config to the readme file according to this guidance https://aka.ms/azsdk/sample-readme-sdk-config`);
    }
    else {
      sdkToGenerate = context.config.sdkName;
    }
  }
  if (sdkToGenerate) {
    context.logger.info(`SDK to generate:${context.config.sdkName}`);
  }
  else {
    context.status = 'notEnabled';
    throw new Error(`No SDKs are enabled for generation. Please check the configuration in the related tspconfig.yaml or readme.md`);
  }
  context.logger.log('endsection', 'Validate SDK configuration');
};

const workflowHandleReadmeMdOrTypeSpecProject = async (context: WorkflowContext, changedSpecs: ChangedSpecs[]) => {
  context.logger.add(context.messageCaptureTransport);
  context.logger.log('info', `Handle the following readme.md or typespec project:`);
  const changedFilesSet = new Set<string>();
  const readmeMdList: string[] = [];
  const typespecProjectList: string[] = [];
  const suppressionFileMap: Map<string, string|undefined> = new Map();

  for (const changedSpec of changedSpecs) {
    if (changedSpec.typespecProject) {
      let content: string | undefined = undefined;
      try {
        content = fs.readFileSync(path.join(context.specFolder, changedSpec.typespecProject!)).toString();
      } catch (error) {
        const typespecPath = `${path.join(context.specFolder, changedSpec.typespecProject!)}`;
        context.logger.error(`IOError: Fails to read typespec file with path of '${typespecPath}'. Skipping the typespec case and continue the run. Please ensure the typespec exists with the correct path. Error: ${error.message}`);
      }
      const config = findSDKToGenerateFromTypeSpecProject(content, context.specRepoConfig)?.filter(
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
    context.status = 'notEnabled';
    context.logger.remove(context.messageCaptureTransport);
    return;
  }

  const changedFiles = [...changedFilesSet];
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

  workflowDetectChangedPackages(context);

  context.logger.remove(context.messageCaptureTransport);
  for (const pkg of context.pendingPackages) {
    await workflowPkgMain(context, pkg);
  }

  context.handledPackages.push(...context.pendingPackages);
  context.pendingPackages = [];
};

const workflowGenerateSdkForSpecPr = async (context: WorkflowContext, changedSpecs: ChangedSpecs[]) => {
  context.logger.remove(context.messageCaptureTransport);
  const callMode =
    context.swaggerToSdkConfig.advancedOptions.generationCallMode ??
    'one-for-all-configs';
  if (callMode === 'one-for-all-configs') {
    await workflowHandleReadmeMdOrTypeSpecProject(context, changedSpecs);
  } else {
    for (const changedSpec of changedSpecs) {
      await workflowHandleReadmeMdOrTypeSpecProject(context, [changedSpec]);
    }
  }
}

const workflowGenerateSdk = async (context: WorkflowContext) => {
  let readmeMdList: string[] = [];
  let typespecProjectList: string[] = [];
  let suppressionFile;
  const filterSuppressionFileMap: Map<string, string|undefined> = new Map();
  if (context.config.tspConfigPath) {
    context.logger.log('info', `Handle the following typespec project: ${context.config.tspConfigPath}`);
	typespecProjectList.push(context.config.tspConfigPath.replace('/tspconfig.yaml', ''));
    suppressionFile = path.join(context.config.localSpecRepoPath, context.config.tspConfigPath.replace('tspconfig.yaml', sdkSuppressionsFileName));
    if (fs.existsSync(suppressionFile)) {
      filterSuppressionFileMap.set(context.config.tspConfigPath, suppressionFile);
    }
  } else if (context.config.readmePath) {
    context.logger.log('info', `Handle the following readme.md: ${context.config.readmePath}`);
	readmeMdList.push(context.config.readmePath);
    suppressionFile = path.join(context.config.localSpecRepoPath, context.config.readmePath.replace('readme.md', sdkSuppressionsFileName));
    if (fs.existsSync(suppressionFile)) {
      filterSuppressionFileMap.set(context.config.readmePath, suppressionFile);
    }
  }
  else {
    context.logger.error(`ConfigError: 'tspConfigPath' and 'readmePath' are not provided. Please provide at least one of them.`);
    return;
  }

  const { status, generateOutput } = await workflowCallGenerateScript(
    context,
    [],
    readmeMdList,
    typespecProjectList
  );
  
  if (!generateOutput && status === 'failed') {
    context.logger.warn('Warning: Package processing is skipped as the SDK generation fails. Please look into the above generation errors or report this issue through https://aka.ms/azsdk/support/specreview-channel.');
    return;
  }
  const sdkSuppressionsYml = await workflowInitGetSdkSuppressionsYml(context, filterSuppressionFileMap);
  context.pendingPackages =
    (generateOutput?.packages ?? []).map((result) => getPackageData(context, result, sdkSuppressionsYml));

  workflowDetectChangedPackages(context);

  context.logger.remove(context.messageCaptureTransport);
  for (const pkg of context.pendingPackages) {
    await workflowPkgMain(context, pkg);
  }

  context.handledPackages.push(...context.pendingPackages);
  context.pendingPackages = [];
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

const workflowInitSpecRepo = (
  context: SdkAutoContext,
) => {
  const specFolder = context.config.localSpecRepoPath;
  const specRepo = simpleGit({ ...simpleGitOptions, baseDir: specFolder });
  return { specFolder, specRepo };
}

const workflowInitSdkRepo = (
  context: SdkAutoContext,
) => {
  const sdkFolder = context.config.localSdkRepoPath;
  const sdkRepo = simpleGit({ ...simpleGitOptions, baseDir: sdkFolder });
  return { sdkFolder, sdkRepo };
};

const fileInitInput = 'initInput.json';
const fileInitOutput = 'initOutput.json';
const workflowCallInitScript = async (context: WorkflowContext) => {
  const initScriptConfig = context.swaggerToSdkConfig.initOptions?.initScript;
  if (initScriptConfig === undefined) {
    context.logger.error('ConfigError: initScript is not configured in the swagger-to-sdk config. Please refer to the schema.');
    setFailureType(context, FailureType.PipelineFrameworkFailed);
    throw new Error('The initScript is not configured in the swagger-to-sdk config. Please refer to the schema.');
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
  const headCommit = await repo.revparse("HEAD");
  const baseCommit = await repo.revparse("HEAD^");
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
    specFolder: path.relative(context.sdkFolder, context.specFolder),
    headSha: context.config.specCommitSha,
    repoHttpsUrl: context.config.specRepoHttpsUrl ?? "",
    changedFiles,
    apiVersion: context.config.apiVersion,
    installInstructionInput: {
      isPublic: !context.isPrivateSpecRepo,
      downloadUrlPrefix: "https://artprodcus3.artifacts.visualstudio.com",
      downloadCommandTemplate: "downloadCommand",
    }
  };

  if (context.swaggerToSdkConfig.generateOptions.generateScript === undefined) {
    context.logger.error('ConfigError: generateScript is not configured in the swagger-to-sdk config. Please refer to the schema.');
    setFailureType(context, FailureType.PipelineFrameworkFailed);
    throw new Error('The generateScript is not configured in the swagger-to-sdk config. Please refer to the schema.');
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

const workflowDetectChangedPackages = (context: WorkflowContext) => {
  context.logger.log('section', 'Detect changed packages');
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
  context.logger.log('endsection', 'Detect changed packages');
};
