import {existsSync, mkdirSync, rmSync} from 'fs';
import * as winston from 'winston';
import { workflowInit, workflowMain } from './workflow';
import {
  loggerConsoleTransport,
  loggerDevOpsTransport,
  loggerFileTransport,
  loggerTestTransport,
  loggerWaitToFinish,
  sdkAutoLogLevels,
  vsoLogError
} from './logging';
import path from 'path';
import { generateReport, generateHtmlFromFilteredLog, saveFilteredLog, saveVsoLog } from './reportStatus';
import { getSpecConfig, specConfigPath } from '../types/SpecConfig';
import { getSwaggerToSdkConfig } from '../types/SwaggerToSdkConfig';
import { extractPathFromSpecConfig } from '../utils/utils';
import { FailureType, SdkAutoContext, SdkAutoOptions, WorkflowContext } from '../types/Workflow';
import { getSdkRepoConfig, loadConfigContent, setFailureType } from '../utils/workflowUtils';

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
  if (!existsSync(logFolder)) {
    mkdirSync(logFolder, { recursive: true });
  }
  const fullLogFileName = path.join(logFolder, `${fileNamePrefix}-full.log`);
  const filteredLogFileName = path.join(logFolder, `${fileNamePrefix}-filtered.log`);
  const vsoLogFileName = path.join(logFolder, `${fileNamePrefix}-vso.log`);
  const htmlLogFileName = path.join(logFolder, `${fileNamePrefix}-${options.sdkName.substring("azure-sdk-for-".length)}-gen-result.html`);
  if (existsSync(fullLogFileName)) {
    rmSync(fullLogFileName);
  }
  if (existsSync(filteredLogFileName)) {
    rmSync(filteredLogFileName);
  }
  if (existsSync(htmlLogFileName)) {
    rmSync(htmlLogFileName);
  }
  if (existsSync(vsoLogFileName)) {
    rmSync(vsoLogFileName);
  }
  logger.add(loggerFileTransport(fullLogFileName));
  logger.info(`Log to ${fullLogFileName}, spec-gen-sdk version: ${options.version}`);
  const localSpecConfigPath = path.join(options.localSpecRepoPath, specConfigPath);
  const specConfigContent = loadConfigContent(localSpecConfigPath, logger)
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

export const sdkAutoMain = async (options: SdkAutoOptions) => {
  const sdkContext = await getSdkAutoContext(options);
  let workflowContext: WorkflowContext | undefined = undefined;

  try {
    workflowContext = await workflowInit(sdkContext);
    await workflowMain(workflowContext);
  } catch (e) {
    if (workflowContext) {
      const message = "Refer to the inner logs for details or report this issue through https://aka.ms/azsdk/support/specreview-channel.";
      sdkContext.logger.error(message);
      workflowContext.status = workflowContext.status === 'notEnabled' ? workflowContext.status : 'failed';
      setFailureType(workflowContext, FailureType.SpecGenSdkFailed);
      workflowContext.messages.push(e.message);
      vsoLogError(workflowContext, message);
      if (e.stack) {
        vsoLogError(workflowContext, `ErrorStack: ${e.stack}.`);
      }
    }
    if (e.stack) {
      sdkContext.logger.error(`ErrorStack: ${e.stack}.`);
    }
  }
  if (workflowContext) {
    saveFilteredLog(workflowContext);
    generateHtmlFromFilteredLog(workflowContext);
    generateReport(workflowContext);
    saveVsoLog(workflowContext);
  }
  await loggerWaitToFinish(sdkContext.logger);
  return workflowContext?.status;
};
