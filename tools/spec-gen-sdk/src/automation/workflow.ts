import * as path from 'node:path';
import * as fs from 'node:fs';
import { findSDKToGenerateFromTypeSpecProject } from '../utils/typespecUtils';
import { runSdkAutoCustomScript, setSdkAutoStatus } from '../utils/runScript';
import { deleteTmpJsonFile, readTmpJsonFile, writeTmpJsonFile } from '../utils/fsUtils';
import { getPackageData } from '../types/PackageData';
import { workflowPkgMain } from './workflowPackage';
import { CommentCaptureTransport } from './logging';
import { findSwaggerToSDKConfiguration } from '../utils/readme';
import { getInitOutput } from '../types/InitOutput';
import { sdkSuppressionsFileName } from '../types/sdkSuppressions';
import { configError, configWarning, toolError } from '../utils/messageUtils';
import { FailureType, SdkAutoContext, WorkflowContext } from '../types/Workflow';
import { setFailureType } from '../utils/workflowUtils';
import { workflowCallGenerateScript, workflowDetectChangedPackages, workflowInitGetSdkSuppressionsYml } from './workflowHelpers';

export const workflowInit = async (context: SdkAutoContext): Promise<WorkflowContext> => {
  const messages = [];
  const messageCaptureTransport = new CommentCaptureTransport({
    extraLevelFilter: ['command', 'error', 'warn'],
    level: 'debug',
    output: messages,
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
      TMPDIR: path.resolve(tmpFolder),
    },
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
  let tspConfigPath = '';
  let readmeMdPath = '';
  let enabledSdkForTspConfig = false;
  let enabledSdkForReadme = false;
  let twoConfigProvided = false;
  let message = '';
  if (context.config.tspConfigPath && context.config.readmePath) {
    twoConfigProvided = true;
  }
  if (context.config.tspConfigPath) {
    tspConfigPath = path.join(context.config.localSpecRepoPath, context.config.tspConfigPath);
  }
  if (context.config.readmePath) {
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
        let sampleTspConfigUrl = 'https://aka.ms/azsdk/tspconfig-sample-dpg';
        if (tspConfigPath.includes('.Management')) {
          sampleTspConfigUrl = 'https://aka.ms/azsdk/tspconfig-sample-mpg';
        }
        message = configWarning(
          `Warning: cannot find supported emitter in tspconfig.yaml for typespec project ${tspConfigPath}. ` +
            `This typespec project will be skipped from SDK generation. Refer to ${sampleTspConfigUrl} to add the right emitter config in the 'tspconfig.yaml' file`,
        );
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
    if (!config || config.repositories.length === 0 || !config.repositories.some((r) => r.repo === context.config.sdkName)) {
      if (!twoConfigProvided) {
        context.status = 'notEnabled';
        message = configWarning(
          `Warning: 'swagger-to-sdk' section cannot be found in ${readmeMdPath} or ${context.config.sdkName} cannot be found in ` +
            `'swagger-to-sdk' section. Please add the section to the readme file according to this guidance https://aka.ms/azsdk/sample-readme-sdk-config`,
        );
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
    message = configWarning(
      `SDK generation configuration is enabled for both ${context.config.tspConfigPath} and ${context.config.readmePath}. ` +
        `Refer to https://aka.ms/azsdk/spec-gen-sdk-config to disable sdk configuration from one of them. ` +
        `This generation will be using TypeSpecs to generate the SDK.`,
    );
    context.logger.warn(message);
    context.logger.info(`SDK to generate:${context.config.sdkName}, configPath: ${context.config.tspConfigPath}`);
    context.specConfigPath = context.config.tspConfigPath;
    context.isSdkConfigDuplicated = true;
  } else if (!enabledSdkForTspConfig && !enabledSdkForReadme) {
    context.status = 'notEnabled';
    message = configWarning('No SDKs are enabled for generation. Please enable them in either the corresponding tspconfig.yaml or readme.md file.');
    context.logger.warn(message);
  } else {
    if (twoConfigProvided && !enabledSdkForTspConfig && context.config.skipSdkGenFromOpenapi === 'true') {
      context.status = 'notEnabled';
      message = configWarning(
        `Warning: cannot find supported emitter in tspconfig.yaml for typespec project ${tspConfigPath}. ` +
          `This typespec project will be skipped from SDK generation. Refer to contoso sample project in spec repo to add the right emitter config in the 'tspconfig.yaml' file`,
      );
      context.logger.warn(message);
    } else {
      context.logger.info(`SDK to generate:${context.config.sdkName}, configPath: ${enabledSdkForTspConfig ? context.config.tspConfigPath : context.config.readmePath}`);
      context.specConfigPath = enabledSdkForTspConfig ? context.config.tspConfigPath : context.config.readmePath;
    }
  }
  context.logger.log('endsection', 'Validate SDK configuration');
};

const fileInitInput = 'initInput.json';
const fileInitOutput = 'initOutput.json';
export const workflowCallInitScript = async (context: WorkflowContext) => {
  let message = '';
  context.logger.add(context.messageCaptureTransport);
  const initScriptConfig = context.swaggerToSdkConfig.initOptions?.initScript;
  if (initScriptConfig === undefined) {
    message = toolError('initScript is not configured in the swagger-to-sdk config. Please refer to the schema.');
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
    argTmpFileList: [fileInitInput, fileInitOutput],
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

export const workflowGenerateSdk = async (context: WorkflowContext) => {
  context.logger.add(context.messageCaptureTransport);
  let readmeMdList: string[] = [];
  let typespecProjectList: string[] = [];
  let suppressionFile;
  let message = '';
  const filterSuppressionFileMap: Map<string, string | undefined> = new Map();

  if (context.specConfigPath) {
    if (context.specConfigPath.endsWith('tspconfig.yaml')) {
      typespecProjectList.push(context.specConfigPath.replace('/tspconfig.yaml', ''));
      suppressionFile = path.join(context.config.localSpecRepoPath, context.specConfigPath.replace('tspconfig.yaml', sdkSuppressionsFileName));
    } else {
      readmeMdList.push(context.specConfigPath);
      suppressionFile = path.join(context.config.localSpecRepoPath, context.specConfigPath.replace('readme.md', sdkSuppressionsFileName));
    }
  } else {
    message = configError("'tspConfigPath' and 'readmePath' are not provided. Please provide at least one of them.");
    context.logger.error(message);
    return;
  }

  context.logger.log('info', `Handle the following spec config: ${context.specConfigPath}`);
  if (fs.existsSync(suppressionFile)) {
    filterSuppressionFileMap.set(context.specConfigPath, suppressionFile);
  }

  const { status, generateOutput } = await workflowCallGenerateScript(context, [], readmeMdList, typespecProjectList);

  if (!generateOutput && status === 'failed') {
    context.logger.warn(
      'Warning: Package processing is skipped as the SDK generation fails. Please look into the above generation errors or report this issue through https://aka.ms/azsdk/support/specreview-channel.',
    );
    return;
  }
  const sdkSuppressionsYml = await workflowInitGetSdkSuppressionsYml(context, filterSuppressionFileMap);
  context.pendingPackages = (generateOutput?.packages ?? []).map((result) => getPackageData(context, result, sdkSuppressionsYml));

  workflowDetectChangedPackages(context);

  context.logger.remove(context.messageCaptureTransport);
  for (const pkg of context.pendingPackages) {
    await workflowPkgMain(context, pkg);
  }

  context.handledPackages.push(...context.pendingPackages);
  context.pendingPackages = [];
};
