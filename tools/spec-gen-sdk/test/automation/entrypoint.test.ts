vi.mock('winston', () => ({
  createLogger: vi.fn(() => ({
    add: vi.fn(),
    info: vi.fn()
  })),
  transports: {
    Console: vi.fn(),
    File: vi.fn(),
    Http: vi.fn()
  },
  format: {
    simple: vi.fn(),
    combine: vi.fn(),
    printf: vi.fn(),
    timestamp: vi.fn(() => ({
      printf: vi.fn()
    })),
  }
}));

vi.mock('node:fs', () => ({
  existsSync: vi.fn(),
  mkdirSync: vi.fn(),
  rmSync: vi.fn(),
  readFileSync: vi.fn(() => Buffer.from('mock content')),
}));

vi.mock('../../src/utils/runScript', () => ({
  setSdkAutoStatus: vi.fn(),
}));

vi.mock('../../src/automation/logging', () => ({
  loggerConsoleTransport: vi.fn(),
  loggerDevOpsTransport: vi.fn(),
  loggerFileTransport: vi.fn(),
  loggerTestTransport: vi.fn(),
  sdkAutoLogLevels: {
    levels: { error: 0, warn: 1, info: 2, debug: 3, testLevel: 5 }
  }
}));

vi.mock('../../src/types/SpecConfig', () => ({
  getSpecConfig: vi.fn(() => ({
    name: 'test-spec-config'
  })),
  specConfigPath: 'spec-config.json'
}));

vi.mock('../../src/types/SwaggerToSdkConfig', () => ({
  getSwaggerToSdkConfig: vi.fn(() => ({
    name: 'test-swagger-to-sdk-config'
  }))
}));

vi.mock('../../src/utils/utils', () => ({
  extractPathFromSpecConfig: vi.fn(() => 'test-prefix')
}));

vi.mock('../../src/utils/workflowUtils', () => ({
  loadConfigContent: vi.fn(() => ({})),
  getSdkRepoConfig: vi.fn(() => ({
    configFilePath: 'swagger_to_sdk_config.json',
    mainRepository: {
      name: 'test-repo'
    }
  })),
}));

import { describe, it, expect, vi, beforeEach, test } from 'vitest';
import * as winston from 'winston';
import * as fs from 'node:fs';
import { SdkAutoOptions } from '../../src/types/Workflow';
import { getSdkAutoContext } from '../../src/automation/entrypoint';
import { loggerConsoleTransport, loggerDevOpsTransport, loggerTestTransport } from '../../src/automation/logging';

describe("entrypoint", () => {
  describe('getSdkAutoContext', () => {
    let mockOptions: SdkAutoOptions;
    let mockLogger: any;

    beforeEach(() => {
      vi.clearAllMocks();
      
      mockLogger = {
        add: vi.fn(),
        info: vi.fn(),
        error: vi.fn(),
        warn: vi.fn(),
        debug: vi.fn()
      };
      
      vi.mocked(winston.createLogger).mockReturnValue(mockLogger);
      
      mockOptions = {
        specRepo: {    
          owner: "azure",
          name: "azure-rest-api-specs",
        },
        localSpecRepoPath: '/test/spec/Azure/azure-rest-api-spec',
        localSdkRepoPath: '/test/sdk/Azure/azure-sdk-for-go',
        tspConfigPath: '/test/tspconfig.yaml',
        readmePath: undefined,
        specCommitSha: "61d28a327868244686110792cb0630f3f7acee02",
        specRepoHttpsUrl: "https://github.com/azure/azure-rest-api-specs",
        pullNumber: "34819",
        sdkName: "azure-sdk-for-go",
        apiVersion: undefined,
        runMode: 'spec-pull-request',
        sdkReleaseType: 'beta',
        workingFolder: '/test/working/folder',
        headRepoHttpsUrl: undefined,
        headBranch: undefined,
        runEnv: 'test',
        branchPrefix: 'sdkAuto',
        version: '1.0.0',  
        skipSdkGenFromOpenapi: undefined,
      } as SdkAutoOptions;
    });

    it('should create logger with correct transport for local environment', async () => {
      mockOptions.runEnv = 'local';
      
      await getSdkAutoContext(mockOptions);
      
      expect(winston.createLogger).toHaveBeenCalledWith({
        levels: { error: 0, warn: 1, info: 2, debug: 3, testLevel: 5 },
      });
      expect(mockLogger.add).toHaveBeenCalled();
      expect(loggerConsoleTransport).toHaveBeenCalledOnce();
    });
    
    it('should create logger with correct transport for Azure DevOps environment', async () => {
      mockOptions.runEnv = 'azureDevOps';
      
      await getSdkAutoContext(mockOptions);
      
      expect(winston.createLogger).toHaveBeenCalledWith({
        levels: { error: 0, warn: 1, info: 2, debug: 3, testLevel: 5 },
      });
      expect(mockLogger.add).toHaveBeenCalled();
      expect(loggerDevOpsTransport).toHaveBeenCalledOnce();
    });

    it('should create logger with correct transport for test environment', async () => {
      mockOptions.runEnv = 'test';
      
      const context = await getSdkAutoContext(mockOptions);
      
      expect(winston.createLogger).toHaveBeenCalledWith({
        levels: { error: 0, warn: 1, info: 2, debug: 3, testLevel: 5 },
      });
      expect(context.logger).toBe(mockLogger);
      expect(mockLogger.add).toHaveBeenCalled();
      expect(loggerTestTransport).toHaveBeenCalledOnce();
    });

    it('should create log directories if they don\'t exist', async () => {
      const mockedExistsSync = vi.mocked(fs.existsSync);
      mockedExistsSync.mockImplementation((path: fs.PathLike) => {
        if (typeof path === 'string' && path.includes('out/logs')) {
          return false;
        } else {
          return true;
        }
      });
      
      await getSdkAutoContext(mockOptions);
      
      expect(fs.mkdirSync).toHaveBeenCalledWith(
        expect.stringContaining('out/logs'),
        expect.objectContaining({ recursive: true })
      );
    });

    it('should generate correct log file names based on file prefix and SDK name', async () => {
      const context = await getSdkAutoContext(mockOptions);
      
      expect(context.fullLogFileName).toContain('test-prefix-full.log');
      expect(context.filteredLogFileName).toContain('test-prefix-filtered.log');
      expect(context.vsoLogFileName).toContain('test-prefix-vso.log');
      expect(context.htmlLogFileName).toContain('test-prefix-go-gen-result.html');
    });

    it('should remove existing log files if they exist', async () => {
      const mockedExistsSync = vi.mocked(fs.existsSync);
      mockedExistsSync.mockImplementation((path: fs.PathLike) => {
        if (typeof path === 'string' && path.includes('test-prefix-full.log')) {
          return true;
        } else if (typeof path === 'string' && path.includes('test-prefix-filtered.log')) {
          return true;
        } else if (typeof path === 'string' && path.includes('test-prefix-vso.log')) {
          return true;
        } else if (typeof path === 'string' && path.includes('test-prefix-go-gen-result.html')) {
          return true;
        } else {
          return false;
        }
      });
      
      await getSdkAutoContext(mockOptions);
      
      expect(fs.rmSync).toHaveBeenCalledTimes(4); // fullLogFileName, filteredLogFileName, htmlLogFileName, vsoLogFileName
    });

    it('should not remove log files if they don\'t exist', async () => {
      vi.mocked(fs.existsSync).mockReturnValue(false);
      
      await getSdkAutoContext(mockOptions);
      
      expect(fs.rmSync).not.toHaveBeenCalled();
    });

    it('should log initialization message with version', async () => {
      await getSdkAutoContext(mockOptions);
      
      expect(mockLogger.info).toHaveBeenCalledWith(
        expect.stringContaining('spec-gen-sdk version: 1.0.0')
      );
    });

    it('should return complete SdkAutoContext with all required properties', async () => {
      const context = await getSdkAutoContext(mockOptions);
      
      expect(context).toHaveProperty('config', mockOptions);
      expect(context).toHaveProperty('logger', mockLogger);
      expect(context).toHaveProperty('fullLogFileName');
      expect(context).toHaveProperty('filteredLogFileName');
      expect(context).toHaveProperty('htmlLogFileName');
      expect(context).toHaveProperty('vsoLogFileName');
      expect(context).toHaveProperty('specRepoConfig');
      expect(context).toHaveProperty('sdkRepoConfig');
      expect(context).toHaveProperty('swaggerToSdkConfig');
      expect(context).toHaveProperty('isPrivateSpecRepo');
    });

    it('should set isPrivateSpecRepo to false for regular spec repos', async () => {
      mockOptions.specRepo.name = 'test-spec-repo';
      
      const context = await getSdkAutoContext(mockOptions);
      
      expect(context.isPrivateSpecRepo).toBe(false);
    });

    it('should set isPrivateSpecRepo to true for PR spec repos', async () => {
      mockOptions.specRepo.name = 'test-spec-repo-pr';
      
      const context = await getSdkAutoContext(mockOptions);
      
      expect(context.isPrivateSpecRepo).toBe(true);
    });
  });
});

