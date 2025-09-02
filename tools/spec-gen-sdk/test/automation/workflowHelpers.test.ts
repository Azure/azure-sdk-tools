import { describe, it, expect, vi, beforeEach } from 'vitest';
import * as fs from 'node:fs';
import * as runScript from '../../src/utils/runScript';
import * as fsUtils from '../../src/utils/fsUtils';
import { workflowCallGenerateScript, workflowDetectChangedPackages, workflowInitGetSdkSuppressionsYml } from '../../src/automation/workflowHelpers';
import { WorkflowContext } from '../../src/types/Workflow';
import { SDKAutomationState } from '../../src/automation/sdkAutomationState';

vi.mock(import('node:fs'), async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    mkdirSync: vi.fn(),
    existsSync: vi.fn(),
    copyFileSync: vi.fn(),
  };
});
vi.mock('../../src/utils/runScript');
vi.mock('../../src/utils/fsUtils');

describe('workflowHelpers', () => {
  let mockContext: WorkflowContext;

  beforeEach(() => {
    mockContext = {
       config: {
        specRepo: {
          owner: 'azure',
          name: 'azure-rest-api-specs',
        },
        localSpecRepoPath: '/test/spec/Azure/azure-rest-api-specs',
        localSdkRepoPath: '/test/sdk/Azure/azure-sdk-for-go',
        tspConfigPath: 'test/tspconfig.yaml',
        readmePath: 'test/readme.md',
        specCommitSha: '63dd8aa27868240681110792cb0630f3f7acee02',
        specRepoHttpsUrl: 'https://github.com/azure/azure-rest-api-specs',
        pullNumber: '34819',
        sdkName: 'azure-sdk-for-go',
        apiVersion: undefined,
        runMode: 'spec-pull-request',
        sdkReleaseType: 'beta',
        workingFolder: '/test/working/sdkauto',
        headRepoHttpsUrl: undefined,
        headBranch: undefined,
        runEnv: 'azureDevOps',
        branchPrefix: 'sdkAuto',
        version: '0.8.1',
        skipSdkGenFromOpenapi: undefined,
      },
      swaggerToSdkConfig: {
        generateOptions: {
          generateScript: 'generate.js'
        }
      },
      isPrivateSpecRepo: false,
      logger: {
        log: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn()
      },
      pendingPackages: []
    } as any;

    vi.clearAllMocks();
  });

  describe('workflowCallGenerateScript', () => {
    it('should successfully generate SDK with TypeSpec project folder', async () => {
      const changedFiles = ['file1.tsp'];
      const relatedReadmeMdFiles: string[] = [];
      const relatedTypeSpecProjectFolder = ['project1'];

      const mockGenerateOutput = {
        packages: [
          {
            packageName: 'test-package',
            path: ['path/to/package'],
            result: 'succeeded'
          }
        ]
      };

      vi.spyOn(fsUtils, 'writeTmpJsonFile').mockImplementation(() => undefined);
      vi.spyOn(fsUtils, 'deleteTmpJsonFile').mockImplementation(() => undefined);
      vi.spyOn(fsUtils, 'readTmpJsonFile').mockReturnValue(mockGenerateOutput);
      vi.spyOn(runScript, 'runSdkAutoCustomScript').mockResolvedValue('succeeded');

      const result = await workflowCallGenerateScript(
        mockContext,
        changedFiles,
        relatedReadmeMdFiles,
        relatedTypeSpecProjectFolder
      );

      expect(result.status).toBe('succeeded');
      expect(result.generateOutput).toBeDefined();
      expect(result.generateOutput.packages).toHaveLength(1);
      expect(fsUtils.writeTmpJsonFile).toHaveBeenCalled();
      expect(runScript.runSdkAutoCustomScript).toHaveBeenCalled();
    });

    it('should throw error when generateScript is not configured', async () => {
      mockContext.swaggerToSdkConfig.generateOptions.generateScript = undefined;

      await expect(workflowCallGenerateScript(
        mockContext,
        [],
        [],
        []
      )).rejects.toThrow('generateScript is not configured');
    });
  });

  describe('workflowDetectChangedPackages', () => {
    it('should log detected packages', () => {
      mockContext.pendingPackages = [{
        name: 'test-package',
        relativeFolderPath: 'sdk/service1',
        extraRelativeFolderPaths: ['sdk/service1/extra'],
        status: 'succeeded' as SDKAutomationState,
        messages: [],
        isPrivatePackage: false,
        changedFilePaths: ['sdk/service1/src/index.ts'],
        generationBranch: 'feature/test',
        generationRepository: 'azure/azure-sdk',
        generationRepositoryUrl: 'https://github.com/azure/azure-sdk',
        integrationBranch: 'main',
        integrationRepository: 'azure/azure-sdk',
        changelogs: ['CHANGELOG.md'],
        artifactPaths: ['dist/'],
        presentSuppressionLines: [],
        absentSuppressionLines: [],
        breakingChangeItems: [],
        serviceName: 'test-service',
        apiViewArtifactPath: 'sdk/service1/apiview',
        language: 'typescript',
        typespecProject: ['project1'],
        useIntegrationBranch: false,
        mainRepository: 'azure/azure-sdk',
        parseSuppressionLinesErrors: [],
        sdkSuppressionFilePath: 'sdk/service1/suppression.yml',
        isDataPlane: true
      }];

      workflowDetectChangedPackages(mockContext);

      expect(mockContext.logger.log).toHaveBeenCalledWith('section', 'Detect changed packages');
      expect(mockContext.logger.info).toHaveBeenCalledWith('1 packages found after generation:');
      expect(mockContext.logger.info).toHaveBeenCalledWith('\tsdk/service1');
      expect(mockContext.logger.info).toHaveBeenCalledWith('\t- sdk/service1/extra');
    });

    it('should log warning when no packages detected', () => {
      mockContext.pendingPackages = [];

      workflowDetectChangedPackages(mockContext);

      expect(mockContext.logger.warn).toHaveBeenCalled();
    });
  });

  describe('workflowInitGetSdkSuppressionsYml', () => {
    it('should handle valid suppression files', async () => {
      const filterSuppressionFileMap = new Map([
        ['spec1.json', 'suppressions1.yml']
      ]);

      const mockYamlContent = `suppressions:
  azure-sdk-for-go:
    - package: test-package
      breaking-changes:
        - test-breaking-change`;

      vi.spyOn(fs, 'readFileSync').mockReturnValue(Buffer.from(mockYamlContent));

      const result = await workflowInitGetSdkSuppressionsYml(mockContext, filterSuppressionFileMap);

      expect(result.size).toBe(1);
      expect(result.get('spec1.json')).toBeDefined();
      expect(result.get('spec1.json')?.content).toBeDefined();
      expect(result.get('spec1.json')?.errors).toHaveLength(0);
    });

    it('should handle missing suppression files', async () => {
      const filterSuppressionFileMap = new Map([
        ['spec1.json', undefined]
      ]);

      const result = await workflowInitGetSdkSuppressionsYml(mockContext, filterSuppressionFileMap);

      expect(result.size).toBe(1);
      expect(result.get('spec1.json')?.content).toBeNull();
      expect(result.get('spec1.json')?.errors).toContain('No suppression file added.');
    });

    it('should handle file read errors', async () => {
      const filterSuppressionFileMap = new Map([
        ['spec1.json', 'suppressions1.yml']
      ]);

      vi.spyOn(fs, 'readFileSync').mockImplementation(() => {
        throw new Error('File not found');
      });

      const result = await workflowInitGetSdkSuppressionsYml(mockContext, filterSuppressionFileMap);

      expect(mockContext.logger.error).toHaveBeenCalled();
      expect(result.size).toBe(0);
    });
  });
});
