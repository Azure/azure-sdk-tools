import * as path from 'path';
import * as _ from 'lodash';
import * as fs from 'fs';
import { default as Transport } from 'winston-transport';
import { findSDKToGenerateFromTypeSpecProject } from '../utils/typespecUtils';
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
import { sdkSuppressionsFileName, SdkSuppressionsYml, SdkPackageSuppressionsEntry, validateSdkSuppressionsFile } from '../types/sdkSuppressions';
import { parseYamlContent } from '../utils/utils';
import { SDKSuppressionContentList } from '../utils/handleSuppressionLines';
import { VsoLogs, SdkAutoContext } from './entrypoint';
import { configError, configWarning, externalError, toolError } from '../utils/messageUtils';

export const remoteIntegration = 'integration';
export const remoteMain = 'main';
export const remoteSecondary = 'secondary';

export const branchSdkGen = 'sdkGen';
export const branchMain = 'main';
export const branchSecondary = 'secondary';
export const branchBase = 'base';

export enum FailureType {
  CodegenFailed = 'Code Generator Failed',
  SpecGenSdkFailed = 'Spec-Gen-Sdk Failed'
}

export type WorkflowContext = SdkAutoContext & {
  stagedArtifactsFolder?: string;
  sdkArtifactFolder?: string;
  sdkApiViewArtifactFolder?: string;
  specConfigPath?: string;
  pendingPackages: PackageData[];
  handledPackages: PackageData[];
  status: SDKAutomationState;
  failureType?: FailureType;
  messages: string[];
  messageCaptureTransport: Transport;
  scriptEnvs: { [key: string]: string | undefined };
  tmpFolder: string;
  vsoLogs: VsoLogs;
};

export const setFailureType = (context: WorkflowContext, failureType: FailureType) => {
  if (context.failureType !== FailureType.CodegenFailed) {
    context.failureType = failureType;
  }
};

export const workflowInit = async (context: SdkAutoContext): Promise<WorkflowContext> => {
  const messages = [];
  const messageCaptureTransport = new CommentCaptureTransport({
    extraLevelFilter: ['command', 'error', 'warn'],
    level: 'debug',
    output: messages
  });

  const tmpFolder = path.join(context.config.workingFolder, `${context.sdkRepoConfig.mainRepository.name}_tmp`);
  fs.mkdirSync(tmpFolder, { recursive: true });

  return {
    ...context,
    pendingPackages: [],
    handledPackages: [],
    vsoLogs: new Map(),
    specConfigPath: context.config.tspConfigPath ?? context.config.readmePath,
    status: 'inProgress',
    messages,
    messageCaptureTransport,
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
  let message = "";
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
    message = configError(`'tspConfigPath' and 'readmePath' are not provided. Please provide at least one of them`);
    throw new Error(message);
  }
  
  if (tspConfigPath) {
    const tspConfigContent = fs.readFileSync(tspConfigPath).toString();
    const config = findSDKToGenerateFromTypeSpecProject(tspConfigContent, context.specRepoConfig);
    if (!config || config.length === 0 || !config.includes(context.config.sdkName)) {
      if (!twoConfigProvided) {
        context.status = 'notEnabled';
        let sampleTspConfigUrl = "https://aka.ms/azsdk/tspconfig-sample-dpg";
        if (tspConfigPath.includes(".Management")) {
          sampleTspConfigUrl = "https://aka.ms/azsdk/tspconfig-sample-mpg"
        }
        message = configWarning(`Warning: cannot find supported emitter in tspconfig.yaml for typespec project ${tspConfigPath}. This typespec project will be skipped from SDK generation. Refer to ${sampleTspConfigUrl} to add the right emitter config in the 'tspconfig.yaml' file`);
        context.logger.warn(message);
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
        message = configWarning(`Warning: 'swagger-to-sdk' section cannot be found in ${readmeMdPath} or ${context.config.sdkName} cannot be found in 'swagger-to-sdk' section. Please add the section to the readme file according to this guidance https://aka.ms/azsdk/sample-readme-sdk-config`)
        context.logger.warn(message);
        return;
      }
    } else {
      enabledSdkForReadme = true;
    }
  }
  // only needs to check the two config provided case when both config enable the sdk generation or both config disable the sdk generation
  // the last case is the normal case, only one config enabled the sdk generation
  if (enabledSdkForTspConfig && enabledSdkForReadme) {
    message = configError(`SDK generation configuration is enabled for both ${context.config.tspConfigPath} and ${context.config.readmePath}. Refer to https://aka.ms/azsdk/spec-gen-sdk-config to disable sdk configuration from one of them`);
    throw new Error(message);
  } else if (!enabledSdkForTspConfig && !enabledSdkForReadme) {
    context.status = 'notEnabled';
    message = configWarning("No SDKs are enabled for generation. Please enable them in either the corresponding tspconfig.yaml or readme.md file.");
    context.logger.warn(message);
  } else {
    context.logger.info(`SDK to generate:${context.config.sdkName}, configPath: ${enabledSdkForTspConfig ? context.config.tspConfigPath : context.config.readmePath}`);
    context.specConfigPath = enabledSdkForTspConfig ? context.config.tspConfigPath : context.config.readmePath;
  }
  context.logger.log('endsection', 'Validate SDK configuration');
};

const workflowGenerateSdk = async (context: WorkflowContext) => {
  context.logger.add(context.messageCaptureTransport);
  let readmeMdList: string[] = [];
  let typespecProjectList: string[] = [];
  let suppressionFile;
  let message = "";
  const filterSuppressionFileMap: Map<string, string|undefined> = new Map();

  if (context.specConfigPath) {
    if (context.specConfigPath.endsWith('tspconfig.yaml')) {
      typespecProjectList.push(context.specConfigPath.replace('/tspconfig.yaml', ''));
      suppressionFile = path.join(context.config.localSpecRepoPath, context.specConfigPath.replace('tspconfig.yaml', sdkSuppressionsFileName));
    } else {
      readmeMdList.push(context.specConfigPath);
      suppressionFile = path.join(context.config.localSpecRepoPath, context.specConfigPath.replace('readme.md', sdkSuppressionsFileName));
    }
  }
  else {
    message = configError("'tspConfigPath' and 'readmePath' are not provided. Please provide at least one of them.");
    context.logger.error(message);
    return;
  }

  context.logger.log('info', `Handle the following typespec project: ${context.specConfigPath}`);
  if (fs.existsSync(suppressionFile)) {
    filterSuppressionFileMap.set(context.specConfigPath, suppressionFile);
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
  let suppressionFileParseResult;
  let message = "";
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
    try {
      suppressionFileData = fs.readFileSync(sdkSuppressionFilePath).toString();
    } catch (error) {
      const message = configError(`Fails to read SDK suppressions file with path of '${sdkSuppressionFilePath}'. Assuming no suppressions are present. Please ensure the suppression file exists in the right path in order to load the suppressions for the SDK breaking changes. Error: ${error.message}`);
      context.logger.error(message);
      continue;
    }
    // parse file both to get yaml content and validate the suppression file has grammar error
    try {
      suppressionFileParseResult = parseYamlContent(suppressionFileData);
    } catch (error) {
      message = configError(`The file parsing failed in the ${sdkSuppressionFilePath}. Details: ${error}`);
      context.logger.error(message);
      sdkSuppressionFilesParseErrorTotal.push(message);
      continue;
    }
    if (suppressionFileParseResult) {
      message = configWarning(`Ignore the suppressions as the file at ${sdkSuppressionFilePath} is empty.`);
      context.logger.warn(message);
      sdkSuppressionFilesParseErrorTotal.push(message);
      continue;
    }
    const suppressionFileContent = suppressionFileParseResult as SdkSuppressionsYml;
    // Check if the suppression file content has any schema error
    const validateSdkSuppressionsFileResult = validateSdkSuppressionsFile(suppressionFileContent);
    if (!validateSdkSuppressionsFileResult.result) {
      sdkSuppressionFilesParseErrorTotal.push(validateSdkSuppressionsFileResult.message)
      context.logger.error(configError(`ContentError: ${validateSdkSuppressionsFileResult.message}. The SDK suppression file is malformed. Please refer to the https://aka.ms/azsdk/sdk-suppression to fix the content.`));
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

const fileInitInput = 'initInput.json';
const fileInitOutput = 'initOutput.json';
const workflowCallInitScript = async (context: WorkflowContext) => {
  let message = "";
  context.logger.add(context.messageCaptureTransport);
  const initScriptConfig = context.swaggerToSdkConfig.initOptions?.initScript;
  if (initScriptConfig === undefined) {
    message = toolError("initScript is not configured in the swagger-to-sdk config. Please refer to the schema.");
    context.logger.error(message);
    setFailureType(context, FailureType.SpecGenSdkFailed);
    throw new Error(message);
  }

  writeTmpJsonFile(context, fileInitInput, {});
  deleteTmpJsonFile(context, fileInitOutput);

  context.logger.log('section', `Call initScript`);
  await runSdkAutoCustomScript(context, initScriptConfig, {
    cwd: context.config.localSdkRepoPath,
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
  context.logger.remove(context.messageCaptureTransport);
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
  let message = "";
  const generateInput: GenerateInput = {
    specFolder: path.relative(context.config.localSdkRepoPath, context.config.localSpecRepoPath),
    headSha: context.config.specCommitSha,
    repoHttpsUrl: context.config.specRepoHttpsUrl ?? "",
    changedFiles,
    apiVersion: context.config.apiVersion,
    runMode: context.config.runMode,
    sdkReleaseType: context.config.sdkReleaseType,
    installInstructionInput: {
      isPublic: !context.isPrivateSpecRepo,
      downloadUrlPrefix: "",
      downloadCommandTemplate: "downloadCommand",
    }
  };

  if (context.swaggerToSdkConfig.generateOptions.generateScript === undefined) {
    message = toolError("generateScript is not configured in the swagger-to-sdk config. Please refer to the schema.");
    context.logger.error(message);
    setFailureType(context, FailureType.SpecGenSdkFailed);
    throw new Error(message);
  }

  context.logger.log('section', 'Call generateScript');

  // One of relatedTypeSpecProjectFolder and relatedReadmeMdFiles must be non-empty
  if (relatedTypeSpecProjectFolder?.length > 0) {
    generateInput.relatedTypeSpecProjectFolder = relatedTypeSpecProjectFolder;
  } else {
    generateInput.relatedReadmeMdFiles = relatedReadmeMdFiles;
  }

  writeTmpJsonFile(context, fileGenerateInput, generateInput);
  deleteTmpJsonFile(context, fileGenerateOutput);

  await runSdkAutoCustomScript(context, context.swaggerToSdkConfig.generateOptions.generateScript, {
    cwd: context.config.localSdkRepoPath,
    argTmpFileList: [fileGenerateInput, fileGenerateOutput],
    statusContext
  });
  context.logger.log('endsection', 'Call generateScript');
  setSdkAutoStatus(context, statusContext.status);
  const generateOutputJson = readTmpJsonFile(context, fileGenerateOutput);
  if (generateOutputJson !== undefined) {
    generateOutput = getGenerateOutput(generateOutputJson);
  } else {
    message = externalError("Failed to read generateOutput.json. Please check if the generate script is configured correctly.");
    throw new Error(message);
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
