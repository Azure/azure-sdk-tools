import path from 'path';
import { spawn } from 'child_process';
import { FailureType, setFailureType, WorkflowContext } from '../automation/workflow';
import { RunLogFilterOptions, RunLogOptions, RunOptions } from '../types/SwaggerToSdkConfig';
import { Readable } from 'stream';
import { SDKAutomationState } from '../sdkAutomationState';

export type RunResult = Exclude<SDKAutomationState, 'inProgress' | 'pending'>;
export type StatusContainer = { status: SDKAutomationState };
const resultLevelMap: { [result in SDKAutomationState]: number } = {
  pending: -2,
  inProgress: -1,
  succeeded: 0,
  warning: 1,
  failed: 2
};
const resultLogLevelMap: { [result in RunResult]: string } = {
  succeeded: 'info',
  warning: 'warn',
  failed: 'error'
};
export const setSdkAutoStatus = (result: StatusContainer, newResult: SDKAutomationState) => {
  result.status = resultLevelMap[result.status] > resultLevelMap[newResult] ? result.status : newResult;
};

export const runSdkAutoCustomScript = async (
  context: WorkflowContext,
  runOptions: RunOptions,
  options: {
    cwd: string;
    fallbackName?: string;
    argTmpFileList?: string[];
    argList?: string[];
    statusContext: StatusContainer;
    continueOnFailed?: boolean;
  }
): Promise<SDKAutomationState> => {
  const scriptPath = runOptions.path;
  const cwdAbsolutePath = path.resolve(options.cwd);
  const args = (options.argTmpFileList ?? []).map((fileName) =>
    path.relative(cwdAbsolutePath, path.resolve(path.join(context.tmpFolder, fileName)))
  );
  if (options.argList) {
    args.push(...options.argList);
  }
  if (options.statusContext.status === 'failed' && !options.continueOnFailed) {
    context.logger.warn(`Warning: Skip command for failed context: ${scriptPath} ${args.join(' ')}. Please ensure the script runs successfully to proceed the SDK generation.`);
    return 'failed';
  }

  context.logger.log('command', `${scriptPath} ${args.join(' ')}`);
  context.logger.log('info', `Config: ${JSON.stringify({ ...runOptions, path: undefined })}`);
  const result: StatusContainer = { status: 'succeeded' };

  const env = {
    PWD: path.resolve(options.cwd),
    ...context.scriptEnvs
  };
  for (const extraEnv of runOptions.envs ?? []) {
    if (!(extraEnv in env)) {
      env[extraEnv] = process.env[extraEnv];
    }
  }
  const scriptSplit = scriptPath.split(' ');
  args.unshift(...scriptSplit.splice(1));

  // tslint:disable: no-null-keyword
  let cmdRet: { code: number | null; signal: NodeJS.Signals | null } = {
    code: null,
    signal: null
  };

  try {
    const child = spawn(scriptSplit[0], args, {
      cwd: options.cwd,
      shell: false,
      stdio: ['ignore', 'pipe', 'pipe'],
      env
    });

    const prefix = `[${runOptions.logPrefix ?? options.fallbackName ?? path.basename(scriptPath)}]`;
    listenOnStream(context, result, prefix, child.stdout, runOptions.stdout, 'cmdout');
    listenOnStream(context, result, prefix, child.stderr, runOptions.stderr, 'cmderr');

    cmdRet = await new Promise((resolve) => {
      // tslint:disable-next-line: no-shadowed-variable
      child.on('exit', (code, signal) => {
        resolve({ code, signal });
      });
    });
  } catch (e) {
    cmdRet.code = -1;
    const scriptName = scriptPath.split("/").pop();
    context.logger.error(`RuntimeError: exception is thrown while running customized language ${scriptName} script. Stack: ${e.stack}. Please refer to the detail log in pipeline run or local console for more information.`);
  }

  let showInComment = false;
  if ((cmdRet.code !== 0 || cmdRet.signal !== null) && runOptions.exitCode !== undefined) {
    if (runOptions.exitCode.result === 'error') {
      setSdkAutoStatus(result, 'failed');
      setFailureType(context, FailureType.CodegenFailed);
    } else if (runOptions.exitCode.result === 'warning') {
      setSdkAutoStatus(result, 'warning');
    }
    if (runOptions.exitCode.showInComment) {
      showInComment = true;
    }
  }

  context.logger.log(
    resultLogLevelMap[result.status],
    `Script return with result [${result.status}] code [${cmdRet.code}] signal [${cmdRet.signal}] cwd [${options.cwd}]: ${scriptPath}`,
    { showInComment }
  );

  if (options.statusContext) {
    setSdkAutoStatus(options.statusContext, result.status);
  }

  return result.status;
};

const listenOnStream = (
  context: WorkflowContext,
  result: StatusContainer,
  prefix: string,
  stream: Readable,
  opts: RunLogOptions | undefined,
  logType: 'cmdout' | 'cmderr'
) => {
  const addLine = (line: string) => {
    if (line.length === 0) {
      return;
    }
    let lineResult: RunResult = 'succeeded';
    let _showInComment = false;
    if (opts !== undefined) {
      if (isLineMatch(line, opts.scriptError)) {
        lineResult = 'failed';
      } else if (isLineMatch(line, opts.scriptWarning)) {
        lineResult = 'warning';
      }
      if (isLineMatch(line, opts.showInComment) || lineResult !== 'succeeded') {
        _showInComment = true;
      }
    }
    setSdkAutoStatus(result, lineResult);
    context.logger.log(logType, `${prefix} ${line}`, { showInComment: _showInComment, lineResult });
  };

  let cacheLine = '';
  stream.on('data', (data) => {
    const newData = cacheLine + data.toString();
    const lastIdx = newData.lastIndexOf('\n');
    if (lastIdx === -1) {
      return;
    }
    const lines = newData.slice(0, lastIdx).split('\n');
    cacheLine = newData.slice(lastIdx + 1);
    for (const line of lines) {
      addLine(line);
    }
  });
  stream.on('end', () => {
    addLine(cacheLine);
  });
};

export const isLineMatch = (line: string, filter: RunLogFilterOptions | undefined) => {
  if (filter === undefined) {
    return false;
  }
  if (typeof filter === 'boolean') {
    return filter;
  }
  if (typeof filter === 'string') {
    filter = new RegExp(filter);
  }
  return filter.exec(line) !== null;
};
