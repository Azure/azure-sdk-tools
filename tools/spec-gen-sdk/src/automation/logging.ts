import * as winston from 'winston';
import { default as Transport } from 'winston-transport';
import { AppendBlobClient } from '@azure/storage-blob';
import { SdkAutoContext } from './entrypoint';
import { PackageData } from '../types/PackageData';
import { SDKAutomationState } from '../sdkAutomationState';

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
            return `##[group] ${info.message}`;
          case 'endsection':
            return `##[endgroup] ${info.message}`;

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

export const getBlobName = (context: Pick<SdkAutoContext, 'config'>, fileName: string, pkg?: PackageData) => {
  let blobName = `${context.config.specRepo.owner}/${context.config.pullNumber}/${context.config.sdkName}`;
  if (pkg) {
    blobName = `${blobName}/${pkg.name.replace('/', '_')}`;
  }
  return `${blobName}/${fileName}`;
};

export const loggerStorageAccountTransport = async (
  context: Pick<SdkAutoContext, 'blobContainerClient' | 'config'>,
  blobName: string
) => {
  const blobClient = context.blobContainerClient.getAppendBlobClient(blobName);
  if (context.config.runEnv === 'test') {
    return {
      blobTransport: new CommentCaptureTransport({ extraLevelFilter: [], output: [] }),
      blobUrl: blobClient.url,
      blobName
    };
  }

  await blobClient.deleteIfExists();
  await blobClient.create();
  const blobTransport = new StorageBlobTransport({
    format: winston.format.combine(winston.format.timestamp({ format: 'hh:mm:ss.SS' })),
    blobClient
  });

  return {
    blobTransport,
    blobUrl: blobClient.url,
    blobName
  };
};

interface StorageBlobTransportOptions extends winston.transport.TransportStreamOptions {
  blobClient: AppendBlobClient;
}

class StorageBlobTransport extends Transport {
  private bufferedMessages: string[] = [];
  private bufferedCallbacks: (() => void)[] = [];
  private waitToFlushCallbacks: (() => void)[] = [];
  private isWriting: boolean = false;

  constructor(private opts: StorageBlobTransportOptions) {
    super(opts);
  }

  public log(info: WinstonInfo, callback: () => void): void {
    this.bufferedMessages.push(formatLog(info) + '\n');
    // this.bufferedCallbacks.push(callback);
    // tslint:disable-next-line: no-floating-promises
    this.writeBufferedMessages();
    callback();
  }

  public async waitToFlush(): Promise<void> {
    if (!this.isWriting) {
      return;
    }
    return new Promise(resolve => {
      this.waitToFlushCallbacks.push(resolve);
    });
  }

  // public close() {
  //   if (!this.isWriting) {
  //     this.emit('finish');
  //     return;
  //   }

  //   this.bufferedCallbacks.push(() => this.emit('finish'));
  // }

  private async writeBufferedMessages(): Promise<void> {
    if (this.isWriting) {
      return;
    }

    this.isWriting = true;
    await new Promise((resolve) => setTimeout(resolve, 100));
    while (this.bufferedMessages.length > 0 || this.bufferedCallbacks.length > 0) {
      const toWrite = this.bufferedMessages.join('');
      const toCall = this.bufferedCallbacks;
      this.bufferedMessages = [];
      this.bufferedCallbacks = [];

      try {
        if (toWrite.length > 0) {
          await this.opts.blobClient.appendBlock(toWrite, toWrite.length);
        }
      } catch (err) {
        this.emit('error', err);
      }

      for (const cb of toCall) {
        cb();
      }
    }
    this.isWriting = false;

    const waitToFlushCallbacks = this.waitToFlushCallbacks;
    this.waitToFlushCallbacks = [];
    for (const cb of waitToFlushCallbacks) {
      cb();
    }
  }
}

export const loggerWaitToFinish = async (logger: winston.Logger) => {
  logger.info('Wait for logger transports to complete');
  for (const transport of logger.transports) {
    if (transport instanceof StorageBlobTransport) {
      await transport.waitToFlush();
    }
  }
};
