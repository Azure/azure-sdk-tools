import { Logger } from '@azure/logger-js';
import { toArray } from '@ts-common/azure-js-dev-tools';

export const getRunnerReportLogger = (output: string[]): Logger => {
  const logFn = (text: string | string[]) => {
    output.push(...toArray(text));
    return Promise.resolve();
  };

  const logWarning = (text: string | string[]) => {
    if (text instanceof Array && text.join('.').includes('WARNING')) {
        output.push(...toArray(text));
    }
    return Promise.resolve();
  };

  const dummyFn = () => Promise.resolve();

  return {
    logError: logFn,
    logWarning: logFn,
    logInfo: logWarning,
    logSection: dummyFn,
    logVerbose: dummyFn
  };
};
