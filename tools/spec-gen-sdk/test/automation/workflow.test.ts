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

vi.mock('../../src/utils/workflowUtils', () => ({
  loadConfigContent: vi.fn(() => ({})),
  getSdkRepoConfig: vi.fn(() => ({
    mainRepository: {
      name: 'test-repo'
    }
  })),
}));

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as winston from 'winston';
import * as fs from 'fs';
import * as path from 'path';
import { getSdkAutoContext, workflowInit } from '../../src/automation/workflow';
import { loggerTestTransport } from '../../src/automation/logging';
import { SdkAutoOptions } from '../../src/types/Entrypoint';
import { findNodeAtLocation } from 'jsonc-parser';

describe('workflow module', () => {
  /**
  describe('getSdkAutoContext', () => {
    let mockOptions: SdkAutoOptions;

    beforeEach(() => {
      vi.clearAllMocks();
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
        readmePath: undefined
      } as SdkAutoOptions;
    });

    it('should create logger with correct transport based on runEnv', async () => {
      await getSdkAutoContext(mockOptions);
      expect(winston.createLogger).toHaveBeenCalled();
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
      expect(fs.rmSync).toHaveBeenCalled();
    });

    it('should set isPrivateSpecRepo correctly', async () => {
      const context = await getSdkAutoContext(mockOptions);
      expect(context.isPrivateSpecRepo).toBe(false);

      mockOptions.specRepo.name = 'test-spec-repo-pr';
      const contextWithPr = await getSdkAutoContext(mockOptions);
      expect(contextWithPr.isPrivateSpecRepo).toBe(true);
    });
  });
 */
  describe('workflowInit', () => {
    let mockContext;

    beforeEach(() => {
      vi.clearAllMocks();
      mockContext = {
        config: {
          workingFolder: '/test/working/folder',
          tspConfigPath: '/test/tspconfig.yaml',
          readmePath: undefined
        },
        sdkRepoConfig: {
          mainRepository: {
            name: 'test-repo'
          }
        },
        logger: {
          add: vi.fn()
        }
      };
    });

    it('should create tmp folder', async () => {
      await workflowInit(mockContext);
      expect(fs.mkdirSync).toHaveBeenCalledWith(
        expect.stringContaining('test-repo_tmp'),
        expect.objectContaining({ recursive: true })
      );
    });

    it('should initialize workflow context with correct defaults', async () => {
      const result = await workflowInit(mockContext);
      expect(result).toMatchObject({
        pendingPackages: [],
        handledPackages: [],
        vsoLogs: expect.any(Map),
        status: 'inProgress',
        messages: [],
        tmpFolder: expect.stringContaining('test-repo_tmp'),
      });
    });

    it('should set up script environment variables', async () => {
      const result = await workflowInit(mockContext);
      expect(result.scriptEnvs).toMatchObject({
        USER: expect.any(String),
        HOME: expect.any(String),
        PATH: expect.any(String),
        SHELL: expect.any(String),
        TMPDIR: expect.stringContaining('test-repo_tmp')
      });
    });
  });
});
