// vi.mock('../../src/automation/workflow', async () => {
//   const actualWorkflow = await vi.importActual<typeof import('../../src/automation/workflow')>('../../src/automation/workflow');
//   return {
//     ...actualWorkflow,
//     // setFailureType: vi.fn(),
//     // workflowInit: vi.fn(),
//     workflowMain: vi.fn(),
//   };
// });

// vi.mock('../../src/types/SpecConfig', async () => {
//   const actualSpecConfig = await vi.importActual<typeof import('../../src/types/SpecConfig')>('../../src/types/SpecConfig');
//   return {
//     ...actualSpecConfig,
//     getSpecConfig: vi.fn(),
//   };
// });

// vi.mock('../../src/types/SwaggerToSdkConfig', async () => {
//   const actualSwaggerToSdkConfig = await vi.importActual<typeof import('../../src/types/SwaggerToSdkConfig')>('../../src/types/SwaggerToSdkConfig');
//   return {
//     ...actualSwaggerToSdkConfig,
//     getSwaggerToSdkConfig: vi.fn(),
//   };
// });

vi.mock('fs', async () => {
  const actualFs = await vi.importActual<typeof import('fs')>('fs');
  return {
    ...actualFs,
    existsSync: vi.fn(),
    mkdirSync: vi.fn(),
    readFileSync: vi.fn(),
    rmSync: vi.fn()
  };
});

// vi.mock('../../src/automation/logging', async () => {
//   const actualLogging = await vi.importActual<typeof import('../../src/automation/logging')>('../../src/automation/logging');
//   return {
//     ...actualLogging,
//     loggerConsoleTransport: vi.fn(),
//     loggerDevOpsTransport: vi.fn(),
//     loggerFileTransport: vi.fn(),
//     loggerTestTransport: vi.fn(),
//     loggerWaitToFinish: vi.fn()
//   };
// });
import { describe, it, expect, vi, beforeEach, afterEach, Mock } from 'vitest';


import * as winston from 'winston';
import {existsSync, mkdirSync, readFileSync, rmSync, type PathOrFileDescriptor} from 'fs';
import {
  loggerConsoleTransport,
  loggerDevOpsTransport,
  loggerFileTransport,
  loggerTestTransport,
  loggerWaitToFinish
}  from '../../src/automation/logging';
import {
  setFailureType,
  workflowInit,
  workflowMain
} from '../../src/automation/workflow';
import * as entrypoint from '../../src/automation/entrypoint';
import { type SpecConfig, type SdkRepoConfig, getSpecConfig, specConfigPath } from '../../src/types/SpecConfig';
import { getSwaggerToSdkConfig, type SwaggerToSdkConfig } from '../../src/types/SwaggerToSdkConfig';

describe('entrypoint', () => {
  const defaultOptions: entrypoint.SdkAutoOptions = {
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

  beforeEach(() => {
    vi.clearAllMocks();

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
    // // Mock fs 
    // vi.mocked(existsSync).mockReturnValue(false);
    // vi.mocked(mkdirSync).mockImplementation(() => undefined);
    // vi.mocked(readFileSync).mockImplementation((path: PathOrFileDescriptor) => {
    //   const filePath = typeof path === 'string' ? path : '';
    //   if (filePath.includes('specificationRepositoryConfiguration.json')) {
    //     return Buffer.from(JSON.stringify(mockSpecConfig));
    //   }
    //   if (filePath.includes('swagger_to_sdk_config.json')) {
    //     return Buffer.from(JSON.stringify(mockSwaggerConfig));
    //   }
    //   return 'aaa';
    // });
    // vi.mocked(rmSync).mockImplementation(() => undefined);

    // // Mock winston logger
    // vi.spyOn(winston, 'createLogger').mockReturnValue({
    //   add: vi.fn(),
    //   info: vi.fn(),
    //   error: vi.fn()
    // } as any);

    // // Mock logging transports
    // vi.mocked(loggerConsoleTransport).mockReturnValue({} as any);
    // vi.mocked(loggerDevOpsTransport).mockReturnValue({} as any);
    // vi.mocked(loggerTestTransport ).mockReturnValue({} as any);
    // vi.mocked(loggerFileTransport).mockReturnValue({} as any);
    // vi.mocked(loggerWaitToFinish).mockResolvedValue(void 0);


    // // Mock SpecConfig and SdkRepoConfig
    // vi.mocked(getSpecConfig).mockReturnValue(mockSpecConfig as any);

    // // Mock SwaggerToSdkConfig
    // vi.mocked(getSwaggerToSdkConfig).mockReturnValue(mockSwaggerConfig as any);

    // // Mock workflow functions
    // // (workflowInit).mockResolvedValue({
    // //   status: 'inProgress',
    // //   logger: {} as any,
    // //   messages: [],
    // //   vsoLogs: new Map()
    // // } as any);
    // vi.mocked(workflowMain).mockResolvedValue(void 0);
    // // (setFailureType).mockImplementation(() => undefined);
  });

  describe('getSdkAutoContext', () => {
    it('should properly initialize context with local environment', async () => {
      vi.spyOn(entrypoint, 'loadConfigContent').mockImplementation(() => {
        return 'mocked content';
      });
//       vi.mocked(readFileSync).mockReturnValue("aaaaaaaa")
      const context = await entrypoint.getSdkAutoContext(defaultOptions);

     expect(readFileSync).toHaveBeenCalled()

      expect(context.config).toEqual(defaultOptions);
      expect(winston.createLogger).toHaveBeenCalled();
      expect(loggerConsoleTransport).toHaveBeenCalled();
      expect(context.specRepoConfig).toBeDefined();
      expect(context.sdkRepoConfig).toBeDefined();
      expect(context.swaggerToSdkConfig).toBeDefined();
    });
/**
    it('should use Azure DevOps logger in Azure DevOps environment', async () => {
      const options = { ...defaultOptions, runEnv: 'azureDevOps' as const };
      await getSdkAutoContext(options);
      
      expect(loggerDevOpsTransport).toHaveBeenCalled();
    });

    it('should use test logger in test environment', async () => {
      const options = { ...defaultOptions, runEnv: 'test' as const };
      await getSdkAutoContext(options);
      
      expect(loggerTestTransport).toHaveBeenCalled();
    });

    it('should create log directory if it does not exist', async () => {
      (existsSync).mockReturnValue(false);
      await getSdkAutoContext(defaultOptions);
      
      expect(mkdirSync).toHaveBeenCalledWith(
        expect.stringContaining('out/logs'),
        expect.objectContaining({ recursive: true })
      );
    });

    it('should clean up existing log files', async () => {
      (existsSync).mockReturnValue(true);
      await getSdkAutoContext(defaultOptions);
      
      expect(rmSync).toHaveBeenCalled();
    });
     */
  });
/**
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

   */
});