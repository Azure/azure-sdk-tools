import { getSdkAutoContext, workflowInit, workflowMain } from './workflow';
import { loggerWaitToFinish, vsoLogError } from './logging';
import { generateReport, generateHtmlFromFilteredLog, saveFilteredLog, saveVsoLog } from './reportStatus';
import { SdkAutoOptions } from '../types/Entrypoint';
import { FailureType, WorkflowContext } from '../types/Workflow';
import { setFailureType } from '../utils/workflowUtils';

export const sdkAutoMain = async (options: SdkAutoOptions) => {
  const sdkContext = await getSdkAutoContext(options);
  let workflowContext: WorkflowContext | undefined = undefined;

  try {
    workflowContext = await workflowInit(sdkContext);
    await workflowMain(workflowContext);
  } catch (e) {
    if (workflowContext) {
      const message = 'Refer to the inner logs for details or report this issue through https://aka.ms/azsdk/support/specreview-channel.';
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
