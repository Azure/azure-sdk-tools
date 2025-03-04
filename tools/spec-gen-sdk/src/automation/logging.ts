import * as winston from 'winston';
import { default as Transport } from 'winston-transport';
import { SDKAutomationState } from './sdkAutomationState';

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
const formatLog = (info: WinstonInfo) => {
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

/**
 * Ensures all logs are written to file transports
 */
export const loggerWaitToFinish = async (logger: winston.Logger) => {
  logger.info('Wait for logger transports to complete');

  const fileTransports = logger.transports.filter(
    (transport) => transport instanceof winston.transports.File
  );


  try {
    await waitForWinston(fileTransports);
  } catch (err) {
      console.log(err);
  }
};

const waitForWinston = (fileTransports) => {
  return new Promise<void>((resolve) => {
    (async () => {
      try {
        for (const transport of fileTransports) {
          await closeWinstonTransportAndWaitForFinish(transport);
        }
        resolve();
      } catch (err) {
        console.log(err); 
        resolve();
      }
    })();
  });
};

const closeWinstonTransportAndWaitForFinish = (transport) => {
    if (!transport.close) {
        // e.g. transport.name === 'console'
        return Promise.resolve();
    }
    // e.g. transport.name === 'file'

    return new Promise<void>((resolve) => {
        transport._doneFinish = false;
        function done() {
            if (transport._doneFinish) {
                return; // avoid resolving twice, for example timeout after successful 'finish' event
            }
            transport._doneFinish = true;
            resolve();
        }
        setTimeout(() => { // TODO: use a cancellable setInterval instead
            done();
        }, 5000); // just in case the 'finish' event never occurs

        const finished = () => {
            done();
        };

        transport.once('finish', finished);
        transport.close();
    });  
};

export function vsoAddAttachment(name: string, path: string): void {
  console.log(`##vso[task.addattachment type=Distributedtask.Core.Summary;name=${name};]${path}`);
}

export function vsoLogIssue(message: string, type = "error"): void {
  console.log(`##vso[task.logissue type=${type}]${message}`);
}

