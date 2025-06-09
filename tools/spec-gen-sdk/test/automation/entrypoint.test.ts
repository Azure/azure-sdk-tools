
import { describe, it, expect, vi, beforeEach, afterEach, Mock } from 'vitest';

vi.mock('fs', async () => {
  const actualFs = await vi.importActual<typeof import('fs')>('fs');
  return {
    ...actualFs,
    existsSync: vi.fn(), // 👈 mock existsSync
  };
});

import * as winston from 'winston';
import * as fs from 'fs';
import {existsSync, mkdirSync, readFileSync, rmSync} from 'fs';
import path from 'path';
import { 
  getSdkAutoContext, 
  sdkAutoMain, 
  getLanguageByRepoName,
  loadConfigContent,
  getSdkRepoConfig,
  vsoLogError,
  vsoLogWarning,
  vsoLogErrors,
  vsoLogWarnings,
  SdkAutoOptions
} from '../../src/automation/entrypoint';
import * as workflow from '../../src/automation/workflow';
import * as logging from '../../src/automation/logging';

describe('entrypoint', () => {
  // Common test options
  const defaultOptions: SdkAutoOptions = {
    specRepo: { owner: 'azure', name: 'azure-rest-api-specs' },
    sdkName: 'azure-sdk-for-js',
    branchPrefix: 'sdkAuto',
    localSpecRepoPath: '/path/to/spec',
    localSdkRepoPath: '/path/to/sdk',
    tspConfigPath: 'path/to/tspconfig.yaml',
    readmePath: 'path/to/readme.md',
    pullNumber: '123',
    apiVersion: '2023-01-01',
    runMode: 'local',
    sdkReleaseType: 'beta',
    specCommitSha: '1234567',
    specRepoHttpsUrl: 'https://github.com/azure/azure-rest-api-specs',
    workingFolder: '/tmp/workdir',
    runEnv: 'local',
    version: '1.0.0'
  };

  const mockSpecConfig = {
    sdkRepositoryMappings: {
      'azure-sdk-for-js': {
        mainRepository: 'azure/azure-sdk-for-js',
        mainBranch: 'main',
        configFilePath: 'swagger_to_sdk_config.json'
      }
    }
  };

  const mockSwaggerConfig = {
    repositories: []
  };

  beforeEach(() => {
    vi.clearAllMocks();

    // Mock filesystem operations
    // vi.spyOn(fs, 'existsSync').mockReturnValue(false);
    (existsSync as Mock).mockReturnValue(false);
    vi.spyOn(fs, 'mkdirSync').mockImplementation(() => undefined);
    vi.spyOn(fs, 'readFileSync').mockImplementation((path: fs.PathOrFileDescriptor) => {
      const filePath = typeof path === 'string' ? path : '';
      if (filePath.includes('specificationRepositoryConfiguration.json')) {
        return Buffer.from(JSON.stringify(mockSpecConfig));
      }
      if (filePath.includes('swagger_to_sdk_config.json')) {
        return Buffer.from(JSON.stringify(mockSwaggerConfig));
      }
      return Buffer.from('');
    });
    vi.spyOn(fs, 'rmSync').mockImplementation(() => undefined);

    // Mock winston logger
    vi.spyOn(winston, 'createLogger').mockReturnValue({
      add: vi.fn(),
      info: vi.fn(),
      error: vi.fn()
    } as any);

    // Mock logging transports
    vi.spyOn(logging, 'loggerConsoleTransport').mockReturnValue({} as any);
    vi.spyOn(logging, 'loggerDevOpsTransport').mockReturnValue({} as any);
    vi.spyOn(logging, 'loggerTestTransport').mockReturnValue({} as any);
    vi.spyOn(logging, 'loggerFileTransport').mockReturnValue({} as any);
    vi.spyOn(logging, 'loggerWaitToFinish').mockResolvedValue();

    // Mock workflow functions
    vi.spyOn(workflow, 'workflowInit').mockResolvedValue({
      status: 'inProgress',
      logger: {} as any,
      messages: [],
      vsoLogs: new Map()
    } as any);
    vi.spyOn(workflow, 'workflowMain').mockResolvedValue();
    vi.spyOn(workflow, 'setFailureType').mockImplementation(() => undefined);
  });

  describe('getSdkAutoContext', () => {
    it('should properly initialize context with local environment', async () => {
      const context = await getSdkAutoContext(defaultOptions);
      
      expect(context.config).toEqual(defaultOptions);
      expect(winston.createLogger).toHaveBeenCalled();
      expect(logging.loggerConsoleTransport).toHaveBeenCalled();
      expect(context.specRepoConfig).toBeDefined();
      expect(context.sdkRepoConfig).toBeDefined();
      expect(context.swaggerToSdkConfig).toBeDefined();
    });

    it('should use Azure DevOps logger in Azure DevOps environment', async () => {
      const options = { ...defaultOptions, runEnv: 'azureDevOps' as const };
      await getSdkAutoContext(options);
      
      expect(logging.loggerDevOpsTransport).toHaveBeenCalled();
    });

    it('should use test logger in test environment', async () => {
      const options = { ...defaultOptions, runEnv: 'test' as const };
      await getSdkAutoContext(options);
      
      expect(logging.loggerTestTransport).toHaveBeenCalled();
    });

    it('should create log directory if it does not exist', async () => {
      vi.spyOn(fs, 'existsSync').mockReturnValue(false);
      await getSdkAutoContext(defaultOptions);
      
      expect(fs.mkdirSync).toHaveBeenCalledWith(
        expect.stringContaining('out/logs'),
        expect.objectContaining({ recursive: true })
      );
    });

    it('should clean up existing log files', async () => {
      vi.spyOn(fs, 'existsSync').mockReturnValue(true);
      await getSdkAutoContext(defaultOptions);
      
      expect(fs.rmSync).toHaveBeenCalled();
    });
  });

  describe('sdkAutoMain', () => {
    it('should execute workflow successfully', async () => {
      const status = await sdkAutoMain(defaultOptions);
      
      expect(workflow.workflowInit).toHaveBeenCalled();
      expect(workflow.workflowMain).toHaveBeenCalled();
      expect(status).toBe('inProgress');
    });

    it('should handle workflow errors', async () => {
      const error = new Error('Workflow failed');
      vi.mocked(workflow.workflowMain).mockRejectedValue(error);

      const status = await sdkAutoMain(defaultOptions);
      
      expect(status).toBe('failed');
    });

    it('should set failure type and log error on workflow failure', async () => {
      const error = new Error('Workflow failed');
      vi.mocked(workflow.workflowMain).mockRejectedValue(error);

      await sdkAutoMain({ ...defaultOptions, runEnv: 'azureDevOps' as const });
      
      expect(workflow.setFailureType).toHaveBeenCalledWith(
        expect.anything(),
        workflow.FailureType.SpecGenSdkFailed
      );
    });
  });

  describe('getLanguageByRepoName', () => {
    const testCases = [
      ['azure-sdk-for-js', 'JavaScript'],
      ['azure-sdk-for-go', 'Go'],
      ['azure-sdk-for-net', '.Net'],
      ['azure-sdk-for-java', 'Java'],
      ['azure-sdk-for-python', 'Python'],
      ['azure-sdk-for-other', 'azure-sdk-for-other'],
      ['', 'unknown'],
      [undefined, 'unknown']
    ];

    testCases.forEach(([input, expected]) => {
      it(`should return ${expected} for ${input}`, () => {
        expect(getLanguageByRepoName(input as string)).toBe(expected);
      });
    });
  });

  describe('loadConfigContent', () => {
    const mockLogger = {
      info: vi.fn(),
      error: vi.fn()
    };

    it('should load and parse JSON file', () => {
      vi.spyOn(fs, 'readFileSync').mockReturnValue(Buffer.from('{"key": "value"}'));
      
      const result = loadConfigContent('config.json', mockLogger as any);
      
      expect(result).toEqual({ key: 'value' });
      expect(mockLogger.info).toHaveBeenCalled();
    });

    it('should throw error for invalid JSON', () => {
      vi.spyOn(fs, 'readFileSync').mockReturnValue(Buffer.from('invalid json'));
      
      expect(() => loadConfigContent('config.json', mockLogger as any)).toThrow();
      expect(mockLogger.error).toHaveBeenCalled();
    });
  });

  describe('getSdkRepoConfig', () => {
    it('should return config for valid SDK name', async () => {
      const config = await getSdkRepoConfig(defaultOptions, mockSpecConfig as any);
      
      expect(config.mainRepository).toBeDefined();
      expect(config.mainBranch).toBe('main');
      expect(config.configFilePath).toBe('swagger_to_sdk_config.json');
    });

    it('should throw error for undefined SDK mapping', async () => {
      const options = { ...defaultOptions, sdkName: 'nonexistent-sdk' };
      
      await expect(getSdkRepoConfig(options, mockSpecConfig as any)).rejects.toThrow();
    });

    it('should handle string-based SDK repository mapping', async () => {
      const simpleConfig = {
        sdkRepositoryMappings: {
          'azure-sdk-for-js': 'azure/azure-sdk-for-js'
        }
      };

      const config = await getSdkRepoConfig(defaultOptions, simpleConfig as any);
      
      expect(config.mainRepository).toEqual({
        owner: 'azure',
        name: 'azure-sdk-for-js'
      });
    });
  });

  describe('VSO Logging', () => {
    const mockContext = {
      config: { runEnv: 'azureDevOps' },
      vsoLogs: new Map(),
    } as any;

    beforeEach(() => {
      mockContext.vsoLogs.clear();
    });

    it('should log single error', () => {
      vsoLogError(mockContext, 'test error');
      
      const logs = mockContext.vsoLogs.get('spec-gen-sdk');
      expect(logs.errors).toEqual(['test error']);
    });

    it('should log single warning', () => {
      vsoLogWarning(mockContext, 'test warning');
      
      const logs = mockContext.vsoLogs.get('spec-gen-sdk');
      expect(logs.warnings).toEqual(['test warning']);
    });

    it('should append multiple errors', () => {
      vsoLogErrors(mockContext, ['error1', 'error2']);
      vsoLogErrors(mockContext, ['error3']);
      
      const logs = mockContext.vsoLogs.get('spec-gen-sdk');
      expect(logs.errors).toEqual(['error1', 'error2', 'error3']);
    });

    it('should append multiple warnings', () => {
      vsoLogWarnings(mockContext, ['warning1', 'warning2']);
      vsoLogWarnings(mockContext, ['warning3']);
      
      const logs = mockContext.vsoLogs.get('spec-gen-sdk');
      expect(logs.warnings).toEqual(['warning1', 'warning2', 'warning3']);
    });

    it('should not log in non-Azure DevOps environment', () => {
      const localContext = { ...mockContext, config: { runEnv: 'local' } };
      
      vsoLogError(localContext, 'test error');
      vsoLogWarning(localContext, 'test warning');
      
      expect(localContext.vsoLogs.size).toBe(0);
    });
  });
});