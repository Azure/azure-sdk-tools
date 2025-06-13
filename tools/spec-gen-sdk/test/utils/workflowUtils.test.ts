import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { FailureType, WorkflowContext } from '../../src/types/Workflow';
import { setFailureType, getLanguageByRepoName } from '../../src/utils/workflowUtils';

describe('workflowUtils', () => {
  describe('setFailureType', () => {
    it('should set failure type when not already CodegenFailed', () => {
      vi.unmock('../../src/automation/workflow');
      const context = { failureType: undefined } as WorkflowContext;
      setFailureType(context, FailureType.SpecGenSdkFailed);
      expect(context.failureType).toBe(FailureType.SpecGenSdkFailed);
    });

    it('should not override CodegenFailed failure type', () => {
      vi.unmock('../../src/automation/workflow');
      const context = { failureType: FailureType.CodegenFailed } as WorkflowContext;
      setFailureType(context, FailureType.SpecGenSdkFailed);
      expect(context.failureType).toBe(FailureType.CodegenFailed);
    });
  });

  describe('getLanguageByRepoName', () => {
    it('should return "unknown" for empty or undefined repo name', () => {
      expect(getLanguageByRepoName('')).toBe('unknown');
      expect(getLanguageByRepoName(undefined as unknown as string)).toBe('unknown');
    });

    const testCases = [
      { pattern: 'js', expected: 'JavaScript', examples: ['azure-sdk-for-js', 'microsoft-js-repo', 'some-js-library'] },
      { pattern: 'go', expected: 'Go', examples: ['azure-sdk-for-go', 'microsoft-go-repo', 'some-go-library'] },
      { pattern: 'net', expected: '.Net', examples: ['azure-sdk-for-net', 'microsoft-net-repo', 'some-net-library'] },
      { pattern: 'java', expected: 'Java', examples: ['azure-sdk-for-java', 'microsoft-java-repo', 'some-java-library'] },
      { pattern: 'python', expected: 'Python', examples: ['azure-sdk-for-python', 'microsoft-python-repo', 'some-python-library'] }
    ];

    testCases.forEach(({ pattern, expected, examples }) => {
      it(`should detect ${expected} repositories`, () => {
        examples.forEach(repoName => {
          expect(getLanguageByRepoName(repoName)).toBe(expected);
        });
      });
    });

    it('should return original name for unknown language patterns', () => {
      const unknownRepos = [
        'azure-sdk-for-rust',
        'microsoft-cpp-repo',
        'some-unknown-repo'
      ];
      unknownRepos.forEach(repoName => {
        expect(getLanguageByRepoName(repoName)).toBe(repoName);
      });
    });
  });
});
