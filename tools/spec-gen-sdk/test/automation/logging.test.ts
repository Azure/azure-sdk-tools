vi.mock('timers/promises', async () => {
  const actual = await vi.importActual<typeof import('timers/promises')>('timers/promises');
  return {
    ...actual,
    setTimeout: vi.fn(() => Promise.resolve()),
  };
});

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as winston from 'winston';
import {
  sdkAutoLogLevels,
  loggerConsoleTransport,
  loggerTestTransport,
  loggerDevOpsTransport,
  CommentCaptureTransport,
  loggerFileTransport,
  loggerWaitToFinish,
  vsoAddAttachment,
  vsoLogIssue,
  vsoLogError,
  vsoLogWarning,
  vsoLogErrors,
  vsoLogWarnings,
  formatLog,
} from '../../src/automation/logging';
import { SDKAutomationState } from '../../src/automation/sdkAutomationState';
import { mkdir, rm } from 'fs/promises';
import { join } from 'path';
import { tmpdir } from 'os';

import { setTimeout } from 'timers/promises';

describe('logging utils', () => {
  describe('formatLog', () => {
    it('should format basic log message correctly', () => {
      const info = {
        level: 'info' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
      };

      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 info \ttest message');
    });

    it('should add C suffix for showInComment', () => {
      const info = {
        level: 'info' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        showInComment: true,
      };

      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 info C \ttest message');
    });

    it('should add E suffix for failed lineResult', () => {
      const info = {
        level: 'error' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        lineResult: 'failed' as SDKAutomationState,
      };

      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 error E \ttest message');
    });

    it('should add W suffix for warning lineResult', () => {
      const info = {
        level: 'warn' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        lineResult: 'warning' as SDKAutomationState,
      };

      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 warn W \ttest message');
    });

    it('should prioritize lineResult over showInComment', () => {
      const info = {
        level: 'error' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        showInComment: true,
        lineResult: 'failed' as SDKAutomationState,
      };

      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 error E \ttest message');
    });

    it('should handle other lineResult states without extra suffix', () => {
      const info = {
        level: 'info' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        lineResult: 'succeeded' as SDKAutomationState,
      };

      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 info \ttest message');
    });
  });

  describe('sdkAutoLogLevels', () => {
    it('should have correct level hierarchy', () => {
      expect(sdkAutoLogLevels.levels.error).toBe(0);
      expect(sdkAutoLogLevels.levels.warn).toBe(1);
      expect(sdkAutoLogLevels.levels.section).toBe(5);
      expect(sdkAutoLogLevels.levels.debug).toBe(50);
    });

    it('should have defined colors for all levels', () => {
      Object.keys(sdkAutoLogLevels.levels).forEach((level) => {
        expect(sdkAutoLogLevels.colors[level as keyof typeof sdkAutoLogLevels.levels]).toBeDefined();
      });
    });
  });

  describe('transport creation', () => {
    it('should create console transport with correct config', () => {
      const transport = loggerConsoleTransport();
      expect(transport).toBeInstanceOf(winston.transports.Console);
      expect(transport.level).toBe('info');
    });

    it('should create test transport with error level', () => {
      const transport = loggerTestTransport();
      expect(transport).toBeInstanceOf(winston.transports.Console);
      expect(transport.level).toBe('error');
    });

    it('should create DevOps transport with endsection level', () => {
      const transport = loggerDevOpsTransport();
      expect(transport).toBeInstanceOf(winston.transports.Console);
      expect(transport.level).toBe('endsection');
    });
  });

  describe('CommentCaptureTransport', () => {
    it('should capture messages based on filter', () => {
      const output: string[] = [];
      const transport = new CommentCaptureTransport({
        extraLevelFilter: ['info', 'error'],
        output,
      });

      transport.log(
        {
          level: 'info',
          message: 'test message',
          timestamp: '12:34:56.789',
        },
        () => {},
      );

      expect(output).toHaveLength(1);
      expect(output[0]).toBe('info\ttest message');
    });

    it('should respect showInComment flag', () => {
      const output: string[] = [];
      const transport = new CommentCaptureTransport({
        extraLevelFilter: ['info'],
        output,
      });

      transport.log(
        {
          level: 'info',
          message: 'test message',
          timestamp: '12:34:56.789',
          showInComment: false,
        },
        () => {},
      );

      expect(output).toHaveLength(0);
    });
  });

  describe('file transport', () => {
    let tmpDir: string;
    let logFile: string;

    beforeEach(async () => {
      tmpDir = join(tmpdir(), 'logging-test-file-transport');
      await mkdir(tmpDir);
      logFile = join(tmpDir, 'test.log');
    });

    afterEach(async () => {
      await rm(tmpDir, { recursive: true, force: true });
    });

    it('should create file transport with correct config', () => {
      const transport = loggerFileTransport(logFile);
      expect(transport).toBeInstanceOf(winston.transports.File);
      expect(transport.filename).toBe('test.log');
      expect(transport.level).toBe('info');
    });
  });

  describe('loggerWaitToFinish', () => {
    afterEach(() => {
      vi.clearAllMocks();
    });

    it('should wait for file transports to complete', async () => {
      const logger = winston.createLogger();
      const transport = loggerFileTransport('test.log');
      const mockEnd = vi.fn();
      transport.end = mockEnd;
      logger.add(transport);

      await loggerWaitToFinish(logger);
      expect(setTimeout).toHaveBeenCalledWith(2000);
      expect(mockEnd).toHaveBeenCalled();
    });
  });

  describe('VSO functions', () => {
    let consoleSpy: any;

    beforeEach(() => {
      consoleSpy = vi.spyOn(console, 'log').mockImplementation(() => {});
    });

    afterEach(() => {
      consoleSpy.mockRestore();
    });

    it('should format vsoAddAttachment correctly', () => {
      vsoAddAttachment('testName', 'testPath');
      expect(consoleSpy).toHaveBeenCalledWith('##vso[task.addattachment type=Distributedtask.Core.Summary;name=testName;]testPath');
    });

    it('should format vsoLogIssue correctly', () => {
      vsoLogIssue('test message');
      expect(consoleSpy).toHaveBeenCalledWith('##vso[task.logissue type=error]test message');

      vsoLogIssue('test message', 'warning');
      expect(consoleSpy).toHaveBeenCalledWith('##vso[task.logissue type=warning]test message');
    });
  });

  describe('VSO logging functions', () => {
    let mockContext;

    beforeEach(() => {
      mockContext = {
        config: {
          runEnv: 'azureDevOps',
        },
        vsoLogs: new Map(),
      };
    });

    describe('vsoLogError', () => {
      it('should add single error to vsoLogs map', () => {
        const message = 'Test error message';
        vsoLogError(mockContext, message);

        expect(mockContext.vsoLogs.get('spec-gen-sdk')).toEqual({
          errors: [message],
        });
      });

      it('should use custom task name when provided', () => {
        const message = 'Test error message';
        const taskName = 'custom-task';
        vsoLogError(mockContext, message, taskName);

        expect(mockContext.vsoLogs.get(taskName)).toEqual({
          errors: [message],
        });
      });

      it('should not log when not in Azure DevOps environment', () => {
        mockContext.config.runEnv = 'local';
        vsoLogError(mockContext, 'Test error');

        expect(mockContext.vsoLogs.size).toBe(0);
      });
    });

    describe('vsoLogWarning', () => {
      it('should add single warning to vsoLogs map', () => {
        const message = 'Test warning message';
        vsoLogWarning(mockContext, message);

        expect(mockContext.vsoLogs.get('spec-gen-sdk')).toEqual({
          warnings: [message],
        });
      });

      it('should use custom task name when provided', () => {
        const message = 'Test warning message';
        const taskName = 'custom-task';
        vsoLogWarning(mockContext, message, taskName);

        expect(mockContext.vsoLogs.get(taskName)).toEqual({
          warnings: [message],
        });
      });

      it('should not log when not in Azure DevOps environment', () => {
        mockContext.config.runEnv = 'local';
        vsoLogWarning(mockContext, 'Test warning');

        expect(mockContext.vsoLogs.size).toBe(0);
      });
    });

    describe('vsoLogErrors', () => {
      it('should add multiple errors to vsoLogs map', () => {
        const errors = ['Error 1', 'Error 2', 'Error 3'];
        vsoLogErrors(mockContext, errors);

        expect(mockContext.vsoLogs.get('spec-gen-sdk')).toEqual({
          errors: errors,
        });
      });

      it('should append errors to existing task entry', () => {
        const initialErrors = ['Error 1'];
        const additionalErrors = ['Error 2', 'Error 3'];

        vsoLogErrors(mockContext, initialErrors);
        vsoLogErrors(mockContext, additionalErrors);

        expect(mockContext.vsoLogs.get('spec-gen-sdk')).toEqual({
          errors: [...initialErrors, ...additionalErrors],
        });
      });
    });

    describe('vsoLogWarnings', () => {
      it('should add multiple warnings to vsoLogs map', () => {
        const warnings = ['Warning 1', 'Warning 2', 'Warning 3'];
        vsoLogWarnings(mockContext, warnings);

        expect(mockContext.vsoLogs.get('spec-gen-sdk')).toEqual({
          warnings: warnings,
        });
      });

      it('should append warnings to existing task entry', () => {
        const initialWarnings = ['Warning 1'];
        const additionalWarnings = ['Warning 2', 'Warning 3'];

        vsoLogWarnings(mockContext, initialWarnings);
        vsoLogWarnings(mockContext, additionalWarnings);

        expect(mockContext.vsoLogs.get('spec-gen-sdk')).toEqual({
          warnings: [...initialWarnings, ...additionalWarnings],
        });
      });

      it('should handle mixed errors and warnings for same task', () => {
        const warnings = ['Warning 1', 'Warning 2'];
        const errors = ['Error 1', 'Error 2'];

        vsoLogWarnings(mockContext, warnings);
        vsoLogErrors(mockContext, errors);

        expect(mockContext.vsoLogs.get('spec-gen-sdk')).toEqual({
          warnings: warnings,
          errors: errors,
        });
      });
    });
  });
});
