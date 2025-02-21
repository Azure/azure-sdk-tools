import * as path from 'path';
import * as _ from 'lodash';
import * as fs from 'fs';
import { default as Transport } from 'winston-transport';
import { findSDKToGenerateFromTypeSpecProject } from '../utils/typespecUtils';
import simpleGit, { SimpleGit, SimpleGitOptions } from 'simple-git';
import { repoKeyToString } from '../utils/repo';
import { runSdkAutoCustomScript, setSdkAutoStatus } from '../utils/runScript';
import {
  deleteTmpJsonFile,
  readTmpJsonFile,
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
  await workflowValidateSdkConfig(context);
  if (context.status === 'notEnabled') {
    return;
  }
  await workflowCallInitScript(context);
  await workflowGenerateSdk(context);
  setSdkAutoStatus(context, 'succeeded');
};

export const workflowValidateSdkConfig = async (context: WorkflowContext) => {
  context.logger.log('section', 'Validate SDK configuration');
  let tspConfigPath = "";
  let readmeMdPath = "";
  let enabledSdkForTspConfig = false;
  let enabledSdkForReadme = false;
  let twoConfigProvided = false;
  if(context.config.tspConfigPath && context.config.readmePath) {
    twoConfigProvided = true;
  }
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
      if (!twoConfigProvided) {
        context.status = 'notEnabled';
        context.logger.warn(`Warning: cannot find supported emitter in tspconfig.yaml for typespec project ${tspConfigPath}. This typespec project will be skipped from SDK generation. Please add the right emitter config in the 'tspconfig.yaml' file. The example project can be found at https://aka.ms/azsdk/sample-arm-tsproject-tspconfig`);
        return;
      }
    } else {
      enabledSdkForTspConfig = true;
    }
  }
  if (readmeMdPath) {
    const readmeContent = fs.readFileSync(readmeMdPath).toString();
    const config = findSwaggerToSDKConfiguration(readmeContent);
    if (!config || config.repositories.length === 0 || !config.repositories.some(r => r.repo === context.config.sdkName)) {
      if (!twoConfigProvided) {
        context.status = 'notEnabled';
        context.logger.warn(`Warning: 'swagger-to-sdk' section cannot be found in ${readmeMdPath} or ${context.config.sdkName} cannot be found in 'swagger-to-sdk' section. Please add the section to the readme file according to this guidance https://aka.ms/azsdk/sample-readme-sdk-config`);
        return;
      }
    } else {
      enabledSdkForReadme = true;
    }
  }
  // only needs to check the two config provided case when both config enable the sdk generation or both config disable the sdk generation
  // the last case is the normal case, only one config enabled the sdk generation
  if (enabledSdkForTspConfig && enabledSdkForReadme) {
    throw new Error(`SDK generation configuration is enabled for both ${context.config.tspConfigPath} and ${context.config.readmePath}. You should enable only one of them.`);
  } else if (!enabledSdkForTspConfig && !enabledSdkForReadme) {
    context.status = 'notEnabled';
    context.logger.warn(`No SDKs are enabled for generation. Please enable them in either the corresponding tspconfig.yaml or readme.md file.`);
  } else {
    context.logger.info(`SDK to generate:${context.config.sdkName}`);
  }
  context.logger.log('endsection', 'Validate SDK configuration');
};

const workflowGenerateSdk = async (context: WorkflowContext) => {
  let readmeMdList: string[] = [];
  let typespecProjectList: string[] = [];
  let suppressionFile;
  const filterSuppressionFileMap: Map<string, string|undefined> = new Map();
  context.logger.add(context.messageCaptureTransport);
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
      downloadUrlPrefix: "",
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
