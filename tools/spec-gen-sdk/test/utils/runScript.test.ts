import { describe, it, expect, vi, beforeEach } from 'vitest';
import { StatusContainer, setSdkAutoStatus, isLineMatch, runSdkAutoCustomScript, listenOnStream } from '../../src/utils/runScript';
import { SDKAutomationState } from '../../src/automation/sdkAutomationState';
import { Readable } from 'stream';
import { WorkflowContext } from '../../src/types/Workflow';

describe('runScript utils', () => {
  describe('setSdkAutoStatus', () => {
    it('should maintain status hierarchy', () => {
      const testCases: [SDKAutomationState, SDKAutomationState, SDKAutomationState][] = [
        ['failed', 'warning', 'failed'],
        ['warning', 'succeeded', 'warning'],
        ['succeeded', 'failed', 'failed'],
        ['inProgress', 'failed', 'failed'],
        ['pending', 'warning', 'warning'],
        ['notEnabled', 'warning', 'notEnabled'],
        ['succeeded', 'notEnabled', 'notEnabled'],
      ];

      testCases.forEach(([initial, newStatus, expected]) => {
        const container: StatusContainer = { status: initial };
        setSdkAutoStatus(container, newStatus);
        expect(container.status).toBe(expected);
      });
    });
  });

  describe('isLineMatch', () => {
    it('should handle undefined and boolean filters', () => {
      const testLine = 'This is a test error line with number 123';
      expect(isLineMatch(testLine, undefined)).toBe(false);
      expect(isLineMatch(testLine, true)).toBe(true);
      expect(isLineMatch(testLine, false)).toBe(false);
    });

    it('should handle RegExp patterns', () => {
      const showInCommentRegExp = /\[AUTOREST\]/;
      const scriptErrorRegExp = /\[ERROR\]/;
      const scriptWarningRegExp = /\[WARNING\]/;
      expect(isLineMatch('[AUTOREST] this is a message', showInCommentRegExp)).toBe(true);
      expect(isLineMatch('[ERROR] this is a message', scriptErrorRegExp)).toBe(true);
      expect(isLineMatch('[WARNING] this is a message', scriptWarningRegExp)).toBe(true);
    });
  });

  describe('listenOnStream', () => {
    let mockContext: WorkflowContext;
    let result: StatusContainer;
    let vsoLogErrors: string[];

    beforeEach(() => {
      mockContext = ({
        logger: {
          log: vi.fn(),
        },
        config: { runEnv: 'local' },
      } as unknown) as WorkflowContext;
      result = { status: 'succeeded' };
      vsoLogErrors = [];
    });

    it('should process stream data correctly', async () => {
      const stream = new Readable();
      stream.push('test line 1\n');
      stream.push('test line 2\n');
      stream.push(null);

      listenOnStream(mockContext, result, '[test]', vsoLogErrors, stream, undefined, 'cmdout');

      await new Promise<void>((resolve) => {
        stream.on('end', () => {
          expect(mockContext.logger.log).toHaveBeenCalledWith('cmdout', '[test] test line 1', expect.any(Object));
          expect(mockContext.logger.log).toHaveBeenCalledWith('cmdout', '[test] test line 2', expect.any(Object));
          resolve();
        });
      });
    });

    it('should handle error patterns', async () => {
      const stream = new Readable();
      stream.push('error: something went wrong\n');
      stream.push(null);

      listenOnStream(mockContext, result, '[test]', vsoLogErrors, stream, { scriptError: /error:/ }, 'cmderr');

      await new Promise<void>((resolve) => {
        stream.on('end', () => {
          expect(result.status).toBe('failed');
          resolve();
        });
      });
    });

    it('should handle warning patterns', async () => {
      const stream = new Readable();
      stream.push('warning: potential issue\n');
      stream.push(null);

      listenOnStream(mockContext, result, '[test]', vsoLogErrors, stream, { scriptWarning: /warning:/ }, 'cmdout');

      await new Promise<void>((resolve) => {
        stream.on('end', () => {
          expect(result.status).toBe('warning');
          resolve();
        });
      });
    });

    it('should collect VSO errors in Azure DevOps environment', async () => {
      mockContext.config.runEnv = 'azureDevOps';
      const stream = new Readable();
      stream.push('error: critical failure\n');
      stream.push(null);

      listenOnStream(mockContext, result, '[test]', vsoLogErrors, stream, { scriptError: /error:/ }, 'cmderr');

      await new Promise<void>((resolve) => {
        stream.on('end', () => {
          expect(vsoLogErrors).toContain('error: critical failure');
          resolve();
        });
      });
    });
  });

  describe('runSdkAutoCustomScript', () => {
    const mockContext: WorkflowContext = ({
      logger: {
        log: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
      },
      tmpFolder: '/tmp',
      config: { runEnv: 'local' },
      scriptEnvs: {},
    } as unknown) as WorkflowContext;

    const baseOptions = {
      cwd: process.cwd(),
      statusContext: { status: 'succeeded' as SDKAutomationState },
    };

    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('should skip execution when status is failed', async () => {
      const options = {
        ...baseOptions,
        statusContext: { status: 'failed' as SDKAutomationState },
      };

      const result = await runSdkAutoCustomScript(mockContext, { path: 'echo test' }, options);

      expect(result).toBe('failed');
      expect(mockContext.logger.warn).toHaveBeenCalled();
    });

    it('should handle custom environment variables', async () => {
      const originalEnv = process.env.TEST_VAR;
      process.env.TEST_VAR = 'test_value';
      const mockRunOptions = {
        path: process.platform === 'win32' ? 'cmd /c echo %TEST_VAR%' : 'sh -c "echo $TEST_VAR"',
        envs: ['TEST_VAR'],
      };
      try {
        const result = await runSdkAutoCustomScript(mockContext, mockRunOptions, baseOptions);

        expect(result).toBe('succeeded');
      } finally {
        if (originalEnv === undefined) {
          delete process.env.TEST_VAR;
        } else {
          process.env.TEST_VAR = originalEnv;
        }
      }
    });

    it('should handle script execution errors', async () => {
      const mockRunOptions = {
        path: process.platform === 'win32' ? 'cmd /c not_a_real_command' : 'sh -c "not_a_real_command"',
        exitCode: { result: 'error' as 'error', showInComment: true },
      };
      const result = await runSdkAutoCustomScript(mockContext, mockRunOptions, baseOptions);

      expect(result).toBe('failed');
    });

    it('should handle warning exit codes', async () => {
      const mockRunOptions = {
        path: process.platform === 'win32' ? 'cmd /c exit 1' : 'sh -c "exit 1"',
        exitCode: { result: 'warning' as 'warning', showInComment: true },
      };
      const result = await runSdkAutoCustomScript(mockContext, mockRunOptions, baseOptions);

      expect(result).toBe('failed');
    });

    it('should handle continueOnFailed option', async () => {
      const mockRunOptions = {
        path: process.platform === 'win32' ? 'cmd /c echo test' : 'sh -c "echo test"',
        envs: ['TEST_VAR'],
      };
      const result = await runSdkAutoCustomScript(mockContext, mockRunOptions, {
        ...baseOptions,
        statusContext: { status: 'failed' },
        continueOnFailed: true,
      });

      expect(result).toBe('succeeded');
    });

    it('should throw exception when path is not provided in RunOptions', async () => {
      const mockRunOptions = {
        command: 'some command',
        envs: ['TEST_VAR'],
      };

      await expect(runSdkAutoCustomScript(mockContext, mockRunOptions, baseOptions))
        .rejects
        .toThrow('Script path is not provided in run options.');
    });
  });
});
