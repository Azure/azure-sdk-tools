import * as winston from 'winston';
import { default as Transport } from 'winston-transport';
import { SDKAutomationState } from './sdkAutomationState';
import { setTimeout } from 'timers/promises';
import { WorkflowContext } from '../types/Workflow';

export const sdkAutoLogLevels = {
  levels: {
    error: 0,
    warn: 1,
    section: 5, // Log as azure devops section
    command: 6, // Running a command
    cmdout: 7, // Command stdout
    cmderr: 8, // Command stdout
    git: 9, // Perform a git operation
    github: 10, // Perform a github operation
    info: 15,
    endsection: 20,
    debug: 50
  },
  colors: {
    error: 'red',
    warn: 'yellow',
    info: 'green',
    cmdout: 'green underline',
    cmderr: 'yellow underline',
    section: 'magenta bold',
    endsection: 'magenta bold',
    command: 'cyan bold',
    git: 'cyan bold',
    github: 'cyan bold',
    debug: 'blue'
  }
} as const;

type WinstonInfo = {
  level: keyof typeof sdkAutoLogLevels.levels;
  message: string;
  timestamp: string;
  showInComment?: boolean;
  lineResult?: SDKAutomationState;
};

export const formatLog = (info: WinstonInfo) => {
  let extra = info.showInComment ? ' C' : '';
  if (info.lineResult) {
    extra = {
      failed: ' E',
      warning: ' W'
    }[info.lineResult] ?? '';
  }

  return `${info.timestamp} ${info.level}${extra} \t${info.message}`;
};

export const loggerConsoleTransport = () => {
  return new winston.transports.Console({
    level: 'info',
    format: winston.format.combine(
      winston.format.colorize({ colors: sdkAutoLogLevels.colors }),
      winston.format.timestamp({ format: 'hh:mm:ss.SSS' }),
      winston.format.printf(formatLog)
    )
  });
};

export const loggerTestTransport = () => {
  return new winston.transports.Console({
    level: 'error',
    format: winston.format.simple()
  });
};

export const loggerDevOpsTransport = () => {
  return new winston.transports.Console({
    level: 'endsection',
    format: winston.format.combine(
      winston.format.colorize({ colors: sdkAutoLogLevels.colors }),
      winston.format.timestamp({ format: 'hh:mm:ss.SSS' }),
      winston.format.printf((info: WinstonInfo) => {
        const { level } = info;
        const msg = formatLog(info);
        switch (level) {
          case 'error':
          case 'debug':
          case 'command':
            return `##[${level}] ${msg}`;
          case 'warn':
            return `##[warning] ${msg}`;
          case 'section':
          case 'endsection':
            return `##[section] ${info.message}`;
          default:
            return msg;
        }
      })
    )
  });
};

interface ArrayCaptureTransportOptions extends Transport.TransportStreamOptions {
  extraLevelFilter: (keyof typeof sdkAutoLogLevels.levels)[];
  output: string[];
}

export class CommentCaptureTransport extends Transport {
  constructor(private opts: ArrayCaptureTransportOptions) {
    super(opts);
  }

  public log(info: WinstonInfo, callback: () => void): void {
    if (info.showInComment === false) {
      callback(); return;
    }
    if (this.opts.extraLevelFilter.indexOf(info.level) === -1 && info.showInComment === undefined) {
      callback(); return;
    }
    this.opts.output.push(`${info.level}\t${info.message}`);
    callback();
  }
}

export const loggerFileTransport = (fileName: string) => {
  return new winston.transports.File({
    filename: fileName,
    level: 'info',
    format: winston.format.combine(
      winston.format.timestamp({ format: 'hh:mm:ss.SSS' }),
      winston.format.printf(formatLog)
    ),
  });
};

export const loggerWaitToFinish = async (logger: winston.Logger) => {
  logger.info('Wait for logger transports to complete');
  for (const transport of logger.transports) {
    if (transport instanceof winston.transports.File) {
      if (transport.end) {
          await setTimeout(2000);
          transport.end();
        }
    }
  }
};

export function vsoAddAttachment(name: string, path: string): void {
  console.log(`##vso[task.addattachment type=Distributedtask.Core.Summary;name=${name};]${path}`);
}

export function vsoLogIssue(message: string, type = "error"): void {
  console.log(`##vso[task.logissue type=${type}]${message}`);
}

export function vsoLogError(context: WorkflowContext, message, task: string = "spec-gen-sdk"): void {
  vsoLogErrors(context, [message], task);
}

export function vsoLogWarning(context: WorkflowContext, message, task: string = "spec-gen-sdk"): void {
  vsoLogWarnings(context, [message], task);
}

export function vsoLogErrors(
  context: WorkflowContext,
  errors: string[],
  task: string = "spec-gen-sdk"
): void {
  if (context.config.runEnv !== 'azureDevOps') {
    return;
  }
  if (!context.vsoLogs.has(task)) {
    // Create a new entry with the initial errors
    context.vsoLogs.set(task, { errors: [...errors] });
    return;
  }

  // If the task already exists, merge the new errors into the existing array
  const logEntry = context.vsoLogs.get(task);
  if (logEntry?.errors) {
    logEntry.errors.push(...errors);
  } else {
    logEntry!.errors = [...errors];
  }
}

export function vsoLogWarnings(
  context: WorkflowContext,
  warnings: string[],
  task: string = "spec-gen-sdk"
): void {
  if (context.config.runEnv !== 'azureDevOps') {
    return;
  }
  if (!context.vsoLogs.has(task)) {
    // Create a new entry with the initial errors
    context.vsoLogs.set(task, { warnings: [...warnings] });
    return;
  }

  // If the task already exists, merge the new errors into the existing array
  const logEntry = context.vsoLogs.get(task);
  if (logEntry?.warnings) {
    logEntry.warnings.push(...warnings);
  } else {
    logEntry!.warnings = [...warnings];
  }
}