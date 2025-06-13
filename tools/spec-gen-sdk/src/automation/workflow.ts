import * as winston from 'winston';
import * as path from 'path';
import * as fs from 'fs';
import { setSdkAutoStatus } from '../utils/runScript';
import { CommentCaptureTransport, loggerConsoleTransport, loggerDevOpsTransport, loggerFileTransport, loggerTestTransport, sdkAutoLogLevels } from './logging';
import { extractPathFromSpecConfig } from '../utils/utils';
import { WorkflowContext } from '../types/Workflow';
import { getSdkRepoConfig, loadConfigContent } from '../utils/workflowUtils';
import { SdkAutoContext, SdkAutoOptions } from '../types/Entrypoint';
import { getSpecConfig, specConfigPath } from '../types/SpecConfig';
import { getSwaggerToSdkConfig } from '../types/SwaggerToSdkConfig';
import { workflowCallInitScript, workflowGenerateSdk, workflowValidateSdkConfig } from './workflowSteps';

export const getSdkAutoContext = async (options: SdkAutoOptions): Promise<SdkAutoContext> => {
  const logger = winston.createLogger({
    levels: sdkAutoLogLevels.levels
  });

  if (options.runEnv === 'local') {
    logger.add(loggerConsoleTransport());
  } else if (options.runEnv === 'azureDevOps') {
    logger.add(loggerDevOpsTransport());
  } else if (options.runEnv === 'test') {
    logger.add(loggerTestTransport());
  }

  // Extract relevant parts from tspConfigPath or readmePath
  const fileNamePrefix = extractPathFromSpecConfig(options.tspConfigPath, options.readmePath)
  const logFolder = path.join(options.workingFolder, 'out/logs');
  if (!fs.existsSync(logFolder)) {
    fs.mkdirSync(logFolder, { recursive: true });
  }
  const fullLogFileName = path.join(logFolder, `${fileNamePrefix}-full.log`);
  const filteredLogFileName = path.join(logFolder, `${fileNamePrefix}-filtered.log`);
  const vsoLogFileName = path.join(logFolder, `${fileNamePrefix}-vso.log`);
  const htmlLogFileName = path.join(logFolder, `${fileNamePrefix}-${options.sdkName.substring("azure-sdk-for-".length)}-gen-result.html`);
  if (fs.existsSync(fullLogFileName)) {
    fs.rmSync(fullLogFileName);
  }
  if (fs.existsSync(filteredLogFileName)) {
    fs.rmSync(filteredLogFileName);
  }
  if (fs.existsSync(htmlLogFileName)) {
    fs.rmSync(htmlLogFileName);
  }
  if (fs.existsSync(vsoLogFileName)) {
    fs.rmSync(vsoLogFileName);
  }
  logger.add(loggerFileTransport(fullLogFileName));
  logger.info(`Log to ${fullLogFileName}, spec-gen-sdk version: ${options.version}`);
  const localSpecConfigPath = path.join(options.localSpecRepoPath, specConfigPath);
  const specConfigContent = loadConfigContent(localSpecConfigPath, logger);  
  const specRepoConfig = getSpecConfig(specConfigContent, options.specRepo);

  const sdkRepoConfig = await getSdkRepoConfig(options, specRepoConfig);
  const swaggerToSdkConfigPath = path.join(options.localSdkRepoPath, sdkRepoConfig.configFilePath);
  const swaggerToSdkConfigContent = loadConfigContent(swaggerToSdkConfigPath, logger);
  const swaggerToSdkConfig = getSwaggerToSdkConfig(swaggerToSdkConfigContent);

  return {
    config: options,
    logger,
    fullLogFileName,
    filteredLogFileName,
    vsoLogFileName,
    htmlLogFileName,
    specRepoConfig,
    sdkRepoConfig,
    swaggerToSdkConfig,
    isPrivateSpecRepo: options.specRepo.name.endsWith('-pr')
  };
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

