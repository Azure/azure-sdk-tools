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

vi.mock('fs', () => ({
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
    levels: { error: 0, warn: 1, info: 2, debug: 3 }
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

import { describe, it, expect, vi, beforeEach } from 'vitest';
import * as winston from 'winston';
import * as fs from 'fs';
import { SdkAutoOptions } from '../../src/types/Workflow';
import { getSdkAutoContext } from '../../src/automation/entrypoint';

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
        runEnv: 'test',
        workingFolder: '/test/working/folder',
        version: '1.0.0',
        localSpecRepoPath: '/test/spec/path',
        localSdkRepoPath: '/test/sdk/path',
        specRepo: {
          name: 'test-spec-repo'
        },
        sdkName: 'azure-sdk-for-test',
        tspConfigPath: '/test/tspconfig.yaml',
        readmePath: undefined,
        branchPrefix: 'test-branch',
        runMode: 'test',
        sdkReleaseType: 'beta',
        specCommitSha: 'abc123',
        specRepoHttpsUrl: 'https://github.com/test/spec-repo'
      } as SdkAutoOptions;
    });

    it('should create logger with correct transport for test environment', async () => {
      mockOptions.runEnv = 'test';
      
      const context = await getSdkAutoContext(mockOptions);
      
      expect(winston.createLogger).toHaveBeenCalledWith({
        levels: { error: 0, warn: 1, info: 2, debug: 3 }
      });
      expect(mockLogger.add).toHaveBeenCalled();
      expect(context.logger).toBe(mockLogger);
    });

    it('should create logger with correct transport for local environment', async () => {
      mockOptions.runEnv = 'local';
      
      await getSdkAutoContext(mockOptions);
      
      expect(winston.createLogger).toHaveBeenCalled();
      expect(mockLogger.add).toHaveBeenCalled();
    });

    it('should create logger with correct transport for Azure DevOps environment', async () => {
      mockOptions.runEnv = 'azureDevOps';
      
      await getSdkAutoContext(mockOptions);
      
      expect(winston.createLogger).toHaveBeenCalled();
      expect(mockLogger.add).toHaveBeenCalled();
    });

    it('should create log directories if they don\'t exist', async () => {
      vi.mocked(fs.existsSync).mockReturnValue(false);
      
      await getSdkAutoContext(mockOptions);
      
      expect(fs.mkdirSync).toHaveBeenCalledWith(
        expect.stringContaining('out/logs'),
        expect.objectContaining({ recursive: true })
      );
    });

    it('should remove existing log files if they exist', async () => {
      vi.mocked(fs.existsSync).mockReturnValue(true);
      
      await getSdkAutoContext(mockOptions);
      
      expect(fs.rmSync).toHaveBeenCalledTimes(4); // full, filtered, html, vso log files
    });

    it('should not remove log files if they don\'t exist', async () => {
      vi.mocked(fs.existsSync).mockReturnValue(false);
      
      await getSdkAutoContext(mockOptions);
      
      expect(fs.rmSync).not.toHaveBeenCalled();
    });

    it('should generate correct log file names based on file prefix and SDK name', async () => {
      const context = await getSdkAutoContext(mockOptions);
      
      expect(context.fullLogFileName).toContain('test-prefix-full.log');
      expect(context.filteredLogFileName).toContain('test-prefix-filtered.log');
      expect(context.vsoLogFileName).toContain('test-prefix-vso.log');
      expect(context.htmlLogFileName).toContain('test-prefix-test-gen-result.html');
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

    it('should log initialization message with version', async () => {
      await getSdkAutoContext(mockOptions);
      
      expect(mockLogger.info).toHaveBeenCalledWith(
        expect.stringContaining('spec-gen-sdk version: 1.0.0')
      );
    });

    it('should handle tspConfigPath when provided', async () => {
      mockOptions.tspConfigPath = '/test/tspconfig.yaml';
      mockOptions.readmePath = undefined;
      
      const context = await getSdkAutoContext(mockOptions);
      
      expect(context).toBeDefined();
    });

    it('should handle readmePath when provided', async () => {
      mockOptions.tspConfigPath = undefined;
      mockOptions.readmePath = '/test/readme.md';
      
      const context = await getSdkAutoContext(mockOptions);
      
      expect(context).toBeDefined();
    });
  });
});

