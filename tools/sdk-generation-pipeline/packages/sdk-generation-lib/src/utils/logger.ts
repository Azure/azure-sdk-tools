import { createLogger, format, Logger, transports } from 'winston';
import { FileTransportInstance } from 'winston/lib/winston/transports';

import { getTaskBasicConfig } from '../types';

const loggerLevels = {
    levels: {
        error: 0,
        warn: 1,
        cmdout: 2,
        cmderr: 3,
        info: 4,
        debug: 5
    },
    colors: {
        error: 'red',
        warn: 'yellow',
        cmdout: 'green underline',
        cmderr: 'yellow underline',
        info: 'green',
        debug: 'blue'
    }
};

type WinstonInfo = {
    level: keyof typeof loggerLevels.levels;
    message: string;
    timestamp: string;
};

const fileTransportInstances: {
    [key: string]: FileTransportInstance
} = {};

export function addFileLog(logger: Logger, logPath: string, taskName: string) {
    const fileTransportInstance = new transports.File({
        level: 'info',
        filename: logPath,
        options: { flags: 'w' },
        format: format.combine(
            format.timestamp({ format: 'YYYY-MM-DD hh:mm:ss' }),
            format.printf((info: WinstonInfo) => {
                const msg = `${info.timestamp} ${info.level} \t${info.message}`;
                return msg;
            })
        )
    });
    fileTransportInstances[taskName] = fileTransportInstance;
    logger.add(fileTransportInstance);
}

export function removeFileLog(logger: Logger, taskName: string) {
    if (!fileTransportInstances[taskName]) {
        throw new Error(`Try to remove non-existed logger transport: ${taskName}`);
    }
    logger.remove(fileTransportInstances[taskName]);
}

export function initializeLogger(logPath: string, taskName: string, addConsoleLog: boolean = true): Logger {
    const logger = createLogger({
        levels: loggerLevels.levels
    });

    addFileLog(logger, logPath, taskName);

    if (addConsoleLog) {
        logger.add(new transports.Console({
            level: 'info',
            format: format.combine(
                format.colorize({ colors: loggerLevels.colors }),
                format.timestamp({ format: 'YYYY-MM-DD hh:mm:ss' }),
                format.printf((info: WinstonInfo) => {
                    const msg = `${info.timestamp} ${info.level} \t${info.message}`;
                    return msg;
                })
            )
        }));
    }
    return logger;
}

// export a default logger, which can be used by pipeline commands
export const logger = initializeLogger(getTaskBasicConfig?.getProperties()?.pipeFullLog, 'pipeline');
