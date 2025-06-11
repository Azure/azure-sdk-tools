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
  formatLog
} from '../../src/automation/logging';
import { SDKAutomationState } from '../../src/automation/sdkAutomationState';
import { mkdir, rm } from 'fs/promises';
import { join } from 'path';
import { tmpdir } from 'os';

vi.mock('timers/promises', async () => {
  const actual = await vi.importActual<typeof import('timers/promises')>('timers/promises');

  return {
    ...actual,
    setTimeout: vi.fn(() => Promise.resolve()), 
  };
});


import { setTimeout } from 'timers/promises';

describe('logging utils', () => {
  describe('formatLog', () => {
    it('should format basic log message correctly', () => {
      const info = {
        level: 'info' as const,
        message: 'test message',
        timestamp: '12:34:56.789'
      };
      
      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 info \ttest message');
    });

    it('should add C suffix for showInComment', () => {
      const info = {
        level: 'info' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        showInComment: true
      };
      
      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 info C \ttest message');
    });

    it('should add E suffix for failed lineResult', () => {
      const info = {
        level: 'error' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        lineResult: 'failed' as SDKAutomationState
      };
      
      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 error E \ttest message');
    });

    it('should add W suffix for warning lineResult', () => {
      const info = {
        level: 'warn' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        lineResult: 'warning' as SDKAutomationState
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
        lineResult: 'failed' as SDKAutomationState
      };
      
      const result = formatLog(info);
      expect(result).toBe('12:34:56.789 error E \ttest message');
    });

    it('should handle other lineResult states without extra suffix', () => {
      const info = {
        level: 'info' as const,
        message: 'test message',
        timestamp: '12:34:56.789',
        lineResult: 'succeeded' as SDKAutomationState
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
      Object.keys(sdkAutoLogLevels.levels).forEach(level => {
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
        output
      });

      transport.log({ 
        level: 'info', 
        message: 'test message', 
        timestamp: '12:34:56.789' 
      }, () => {});

      expect(output).toHaveLength(1);
      expect(output[0]).toBe('info\ttest message');
    });

    it('should respect showInComment flag', () => {
      const output: string[] = [];
      const transport = new CommentCaptureTransport({
        extraLevelFilter: ['info'],
        output
      });

      transport.log({ 
        level: 'info', 
        message: 'test message', 
        timestamp: '12:34:56.789',
        showInComment: false
      }, () => {});

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
      expect(consoleSpy).toHaveBeenCalledWith(
        '##vso[task.addattachment type=Distributedtask.Core.Summary;name=testName;]testPath'
      );
    });

    it('should format vsoLogIssue correctly', () => {
      vsoLogIssue('test message');
      expect(consoleSpy).toHaveBeenCalledWith(
        '##vso[task.logissue type=error]test message'
      );

      vsoLogIssue('test message', 'warning');
      expect(consoleSpy).toHaveBeenCalledWith(
        '##vso[task.logissue type=warning]test message'
      );
    });
  });
});