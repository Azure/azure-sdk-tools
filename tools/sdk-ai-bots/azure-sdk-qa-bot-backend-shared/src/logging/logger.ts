import winston from 'winston';

export const logger = winston.createLogger({
  // TODO: add env vars for log level
  level: process.env.LOG_LEVEL || 'info',

  format: winston.format.combine(
    winston.format.json(),
    winston.format.timestamp(),
    winston.format.errors({ stack: true }),
    winston.format.splat()
  ),
  defaultMeta: { service: 'azure-sdk-qa-kit-shared' },
  transports: [new winston.transports.Console()],
});
