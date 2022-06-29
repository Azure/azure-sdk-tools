import * as path from 'path';

const logger = {
    error: jest.fn(),
    warn: jest.fn(),
    info: jest.fn()
};

jest.mock('winston', () => ({
    format: {
        colorize: jest.fn(),
        combine: jest.fn(),
        label: jest.fn(),
        timestamp: jest.fn(),
        printf: jest.fn()
    },
    createLogger: jest.fn().mockReturnValue(logger),
    transports: {
        Console: jest.fn()
    }
}));

import * as winston from 'winston';

import { extractAutorestConfigs } from '../../dist/utils/autorestConfigExtractorUtils';

const autorestSingleConfigFilePath = path.join(path.resolve('.'), 'test', 'unit', 'utils', 'autorest-single-config.md');
const autorestMultiConfigFilePath = path.join(path.resolve('.'), 'test', 'unit', 'utils', 'autorest-multi-config.md');
const loggerMock: winston.Logger = winston.createLogger();

describe('autorest config extractor util test', () => {
    it('should extract autorest config from autorest file', async () => {
        const sdkRepo = './azure-sdk-for-js';
        const result = extractAutorestConfigs(autorestSingleConfigFilePath, sdkRepo, loggerMock);
        expect(result?.length).toBeGreaterThan(0);
    });

    it('should extract the first autorest config from autorest file', async () => {
        const sdkRepo = './sdk-repo';
        const result = extractAutorestConfigs(autorestMultiConfigFilePath, sdkRepo, loggerMock);
        expect(result?.length).toBeGreaterThan(0);
        expect(result.includes('azure-sdk-for-js')).toBe(true);
    });

    it('cannot extract autorest config from autorest file', async () => {
        const sdkRepo = './azure-sdk-for-go';
        const result = extractAutorestConfigs(autorestSingleConfigFilePath, sdkRepo, loggerMock);
        expect(result).toBeUndefined();
    });
});
