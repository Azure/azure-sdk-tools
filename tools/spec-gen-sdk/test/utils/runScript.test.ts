import { describe, it, expect, vi, beforeEach } from 'vitest';
import { RunResult, StatusContainer, setSdkAutoStatus, isLineMatch, runSdkAutoCustomScript } from '../../src/utils/runScript';
import { WorkflowContext } from '../../src/automation/workflow';
import { SDKAutomationState } from '../../src/automation/sdkAutomationState';
import path from 'path';

describe('runScript utils', () => {
  describe('setSdkAutoStatus', () => {
    it('should maintain status hierarchy', () => {
      const testCases: [SDKAutomationState, SDKAutomationState, SDKAutomationState][] = [
        ['failed', 'warning', 'failed'],
        ['warning', 'succeeded', 'warning'],
        ['succeeded', 'failed', 'failed'],
        ['inProgress', 'failed', 'failed'],
        ['pending', 'warning', 'warning']
      ];

      testCases.forEach(([initial, newStatus, expected]) => {
        const container: StatusContainer = { status: initial };
        setSdkAutoStatus(container, newStatus);
        expect(container.status).toBe(expected);
      });
    });
  });

  describe('isLineMatch', () => {
    const testLine = 'This is a test error line with number 123';

    it('should handle undefined and boolean filters', () => {
      expect(isLineMatch(testLine, undefined)).toBe(false);
      expect(isLineMatch(testLine, true)).toBe(true);
      expect(isLineMatch(testLine, false)).toBe(false);
    });

    it('should handle RegExp patterns', () => {
      expect(isLineMatch(testLine, /\d+/)).toBe(true);
      expect(isLineMatch(testLine, /error.*\d+/)).toBe(true);
      expect(isLineMatch(testLine, /warning/)).toBe(false);
    });
  });

  describe('runSdkAutoCustomScript', () => {
    const mockContext: WorkflowContext = {
      logger: {
        log: vi.fn(),
        warn: vi.fn(),
        error: vi.fn()
      },
      tmpFolder: '/tmp',
      config: { runEnv: 'local' },
      scriptEnvs: {},
    } as unknown as WorkflowContext;

    const baseOptions = {
      cwd: process.cwd(),
      statusContext: { status: 'succeeded' as SDKAutomationState }
    };

    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('should skip execution when status is failed', async () => {
      const options = {
        ...baseOptions,
        statusContext: { status: 'failed' as SDKAutomationState }
      };

      const result = await runSdkAutoCustomScript(
        mockContext,
        { path: 'echo test' },
        options
      );

      expect(result).toBe('failed');
      expect(mockContext.logger.warn).toHaveBeenCalled();
    });

    // it('should execute successful command', async () => {
    //   const result = await runSdkAutoCustomScript(
    //     mockContext,
    //     { path: 'echo "test success"' },
    //     baseOptions
    //   );

    //   expect(result).toBe('succeeded');
    //   expect(mockContext.logger.log).toHaveBeenCalled();
    // });

    // it('should handle command failure', async () => {
    //   const result = await runSdkAutoCustomScript(
    //     mockContext,
    //     { 
    //       path: 'nonexistent-command',
    //       exitCode: { result: 'error' }
    //     },
    //     baseOptions
    //   );

    //   expect(result).toBe('failed');
    //   expect(mockContext.logger.error).toHaveBeenCalled();
    // });

    it('should handle custom environment variables', async () => {
      process.env.TEST_VAR = 'test_value';
      
      const result = await runSdkAutoCustomScript(
        mockContext,
        { 
          path: 'echo $TEST_VAR',
          envs: ['TEST_VAR']
        },
        baseOptions
      );

      expect(result).toBe('succeeded');
    });

    it('should process script output streams', async () => {
      const result = await runSdkAutoCustomScript(
        mockContext,
        { 
          path: 'echo "error message" >&2',
          stderr: { scriptError: true }
        },
        baseOptions
      );

      expect(result).toBe('failed');
    });
  });
});