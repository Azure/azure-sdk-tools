import * as winston from 'winston';
import {getTaskBasicConfig, TaskBasicConfig} from "../types/taskBasicConfig";

function getLogger() {
    const config: TaskBasicConfig = getTaskBasicConfig.getProperties();
    const sdkAutoLogLevels = {
        levels: {
            error: 0,
            warn: 1,
            section: 5, // Log as azure devops section
            command: 6, // Running a command
            cmdout: 7, // Command stdout
            cmderr: 8, // Command stdout
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
            debug: 'blue'
        }
    }

    const logger = winston.createLogger({
        levels: sdkAutoLogLevels.levels
    });

    type WinstonInfo = {
        level: keyof typeof sdkAutoLogLevels.levels;
        message: string;
        timestamp: string;
        storeLog?: boolean;
    };

    logger.add(new winston.transports.File({
        level: 'info',
        filename: config.pipeFullLog,
        options: { flags: 'w' },
        format: winston.format.combine(
            winston.format.timestamp({format: 'YYYY-MM-DD hh:mm:ss'}),
            winston.format.printf((info: WinstonInfo) => {
                const msg = `${info.timestamp} ${info.level} \t${info.message}`;
                return msg;
            })
        )
    }));

    logger.add(new winston.transports.Console({
        level: 'endsection',
        format: winston.format.combine(
            winston.format.colorize({ colors: sdkAutoLogLevels.colors }),
            winston.format.timestamp({ format: 'YYYY-MM-DD hh:mm:ss' }),
            winston.format.printf((info: WinstonInfo) => {
                const {level} = info;
                let msg = `${info.timestamp} ${info.level} \t${info.message}`;
                switch (level) {
                    case 'error':
                    case 'debug':
                    case 'command':
                        msg = `##[${level}] ${msg}`;
                    case 'warn':
                        msg = `##[warning] ${msg}`;
                    case 'section':
                        msg = `##[group] ${info.message}`;
                    case 'endsection':
                        msg = `##[endgroup] ${info.message}`;
                }
                return msg;
            })
        )
    }))
    return logger;
}

export const logger: winston.Logger = getLogger();
