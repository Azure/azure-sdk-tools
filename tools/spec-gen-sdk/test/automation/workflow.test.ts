vi.mock('node:fs', () => ({
  existsSync: vi.fn(),
  mkdirSync: vi.fn(),
  rmSync: vi.fn(),
  readFileSync: vi.fn(() => Buffer.from('mock content')),
}));

vi.mock('node:path', () => ({
  ...vi.importActual('node:path'),
  join: vi.fn((...args) => args.join('/')),
  resolve: vi.fn((p) => `/resolved/${p}`),
}));

vi.mock('../../src/utils/typespecUtils');
vi.mock('../../src/utils/runScript');
vi.mock('../../src/utils/fsUtils');
vi.mock('../../src/automation/logging');
vi.mock('../../src/utils/readme');
vi.mock('../../src/types/InitOutput');
vi.mock('../../src/types/sdkSuppressions');

vi.mock(import('../../src/utils/messageUtils'), async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    toolError: vi.fn(),
  };
});

vi.mock('../../src/types/PackageData', () => ({
  getPackageData: vi.fn().mockReturnValue({ name: 'test-package', relativeFolderPath: 'test-package', extraRelativeFolderPaths: [] }),
}));

vi.mock('../../src/automation/workflowPackage', () => ({
  workflowPkgMain: vi.fn().mockImplementation(() => {}),
}));

vi.mock('../../src/utils/workflowUtils', () => ({
  setFailureType: vi.fn(),
  loadConfigContent: vi.fn(() => ({})),
  getSdkRepoConfig: vi.fn(() => ({
    configFilePath: 'swagger_to_sdk_config.json',
    mainRepository: {
      name: 'test-repo',
    },
  })),
}));

vi.mock('../../src/automation/workflowHelpers', () => ({
  workflowCallGenerateScript: vi.fn().mockResolvedValue({
    status: 'succeeded',
    generateInput: {
      specFolder: 'spec',
      headSha: 'sha123',
      repoHttpsUrl: 'https://github.com/test/repo',
      changedFiles: [],
      runMode: 'test',
      sdkReleaseType: 'beta',
      installInstructionInput: {
        isPublic: true,
        downloadUrlPrefix: '',
        downloadCommandTemplate: 'downloadCommand',
      },
    },
    generateOutput: {
      packages: [{ packageName: 'test-package', path: ['test'], result: 'succeeded' }],
    },
  }),
  workflowInitGetSdkSuppressionsYml: vi.fn().mockResolvedValue(new Map()),
  workflowDetectChangedPackages: vi.fn().mockImplementation(() => {}),
}));

vi.mock('../../src/types/validator', () => ({
  getTypeTransformer: () => (data) => data,
}));

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs';
import { workflowInit, workflowMain, workflowValidateSdkConfig, workflowCallInitScript, workflowGenerateSdk } from '../../src/automation/workflow';
import { SdkAutoContext, WorkflowContext, FailureType } from '../../src/types/Workflow';
import { CommentCaptureTransport } from '../../src/automation/logging';

// Import mocked modules
import { findSDKToGenerateFromTypeSpecProject } from '../../src/utils/typespecUtils';
import { runSdkAutoCustomScript, setSdkAutoStatus } from '../../src/utils/runScript';
import { deleteTmpJsonFile, readTmpJsonFile, writeTmpJsonFile } from '../../src/utils/fsUtils';
import { getPackageData } from '../../src/types/PackageData';
import { workflowPkgMain } from '../../src/automation/workflowPackage';
import { findSwaggerToSDKConfiguration } from '../../src/utils/readme';
import { getInitOutput } from '../../src/types/InitOutput';
import { configError, configWarning, toolError } from '../../src/utils/messageUtils';
import { setFailureType } from '../../src/utils/workflowUtils';
import { workflowCallGenerateScript, workflowDetectChangedPackages, workflowInitGetSdkSuppressionsYml } from '../../src/automation/workflowHelpers';

describe('workflow', () => {
  let mockSdkAutoContext: SdkAutoContext;
  let mockWorkflowContext: WorkflowContext;
  let mockLogger: any;
  let mockCommentCaptureTransport: any;

  beforeEach(() => {
    vi.clearAllMocks();

    // Setup mock logger
    mockLogger = {
      log: vi.fn(),
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      add: vi.fn(),
      remove: vi.fn(),
    };

    // Setup mock transport
    mockCommentCaptureTransport = {
      extraLevelFilter: ['command', 'error', 'warn'],
      level: 'debug',
      output: [],
    };

    vi.mocked(CommentCaptureTransport).mockImplementation(() => mockCommentCaptureTransport);

    mockSdkAutoContext = {
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
        sdkName: 'azure-sdk-for-test',
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
      logger: mockLogger,
      fullLogFileName: '/test/sdkauto/out/logs/containerservice-resource-manager-microsoft-containerservice-aks-full.log',
      filteredLogFileName: '/test/sdkauto/out/logs/containerservice-resource-manager-microsoft-containerservice-aks-filtered.log',
      vsoLogFileName: '/test/sdkauto/out/logs/containerservice-resource-manager-microsoft-containerservice-aks-vso.log',
      htmlLogFileName: '/test/sdkauto/out/logs/containerservice-resource-manager-microsoft-containerservice-aks-go-gen-result.html',
      specRepoConfig: { name: 'test-spec-config' },
      sdkRepoConfig: {
        integrationRepository: {
          name: 'azure-sdk-for-go',
          owner: 'azure-sdk',
        },
        mainRepository: {
          name: 'azure-sdk-for-go',
          owner: 'Azure',
        },
        configFilePath: 'eng/swagger_to_sdk_config.json',
        integrationBranchPrefix: 'sdkAutomation',
        mainBranch: 'main',
        secondaryRepository: {
          name: 'azure-sdk-for-go',
          owner: 'Azure',
        },
        secondaryBranch: 'main',
      },
      swaggerToSdkConfig: {
        initOptions: {
          initScript: 'init-script.js',
        },
      },
      isPrivateSpecRepo: false,
    } as any;

    mockWorkflowContext = {
      ...mockSdkAutoContext,
      pendingPackages: [],
      handledPackages: [],
      vsoLogs: new Map(),
      specConfigPath: 'test/tspconfig.yaml',
      status: 'inProgress',
      messages: [],
      messageCaptureTransport: mockCommentCaptureTransport,
      tmpFolder: '/test/tmp',
      scriptEnvs: {
        USER: 'testuser',
        HOME: '/home/testuser',
        PATH: '/usr/bin',
        SHELL: '/bin/bash',
        NODE_OPTIONS: '--max-old-space-size=4096',
        TMPDIR: '/test/tmp',
      },
    } as any;
  });

  describe('workflowInit', () => {
    it('should initialize workflow context correctly', async () => {
      const result = await workflowInit(mockSdkAutoContext);

      expect(result).toMatchObject({
        ...mockSdkAutoContext,
        pendingPackages: [],
        handledPackages: [],
        status: 'inProgress',
        specConfigPath: mockSdkAutoContext.config.tspConfigPath,
        tmpFolder: expect.stringContaining('/test/working/sdkauto/azure-sdk-for-go_tmp'),
      });

      expect(result.vsoLogs).toBeInstanceOf(Map);
      expect(result.messages).toEqual([]);
      expect(result.scriptEnvs).toHaveProperty('TMPDIR');
      expect(fs.mkdirSync).toHaveBeenCalledWith(expect.stringContaining('/test/working/sdkauto/azure-sdk-for-go_tmp'), { recursive: true });
    });

    it('should use readmePath when tspConfigPath is not provided', async () => {
      const contextWithoutTsp = {
        ...mockSdkAutoContext,
        config: {
          ...mockSdkAutoContext.config,
          tspConfigPath: undefined,
        },
      };

      const result = await workflowInit(contextWithoutTsp);

      expect(result.specConfigPath).toBe(contextWithoutTsp.config.readmePath);
    });
  });

  describe('workflowMain', () => {
    it('should skip execution when status is notEnabled', async () => {
      mockWorkflowContext.status = 'notEnabled';

      await workflowMain(mockWorkflowContext);

      expect(setSdkAutoStatus).not.toHaveBeenCalled();
    });
  });

  describe('workflowValidateSdkConfig', () => {
    beforeEach(() => {
      vi.mocked(fs.readFileSync).mockImplementation(() => Buffer.from('mock content'));
    });

    it('should throw error when neither tspConfigPath nor readmePath are provided', async () => {
      mockWorkflowContext.config.tspConfigPath = undefined;
      mockWorkflowContext.config.readmePath = undefined;

      await expect(workflowValidateSdkConfig(mockWorkflowContext)).rejects.toThrow(/.*'tspConfigPath' and 'readmePath' are not provided.*/);
    });

    it('should set status to notEnabled when tspconfig does not contain supported SDK', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = undefined;

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.status).toBe('notEnabled');
      expect(mockWorkflowContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('cannot find supported emitter in tspconfig.yaml'));
    });

    it('should set status to notEnabled when readme does not contain supported SDK', async () => {
      mockWorkflowContext.config.tspConfigPath = undefined;
      mockWorkflowContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({ repositories: [] });

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.status).toBe('notEnabled');
      expect(mockWorkflowContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining("'swagger-to-sdk' section cannot be found"));
    });

    it('should set specConfigPath to tspConfigPath when tspconfig contains supported SDK', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = undefined;

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.status).toBe('inProgress');
      expect(mockWorkflowContext.specConfigPath).toBe('some/path/tspconfig.yaml');
      expect(mockWorkflowContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('SDK to generate:azure-sdk-for-test'));
    });

    it('should set specConfigPath to readmePath when readme contains supported SDK', async () => {
      mockWorkflowContext.config.tspConfigPath = undefined;
      mockWorkflowContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.status).toBe('inProgress');
      expect(mockWorkflowContext.specConfigPath).toBe('some/path/readme.md');
      expect(mockWorkflowContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('SDK to generate:azure-sdk-for-test'));
    });

    it('should mark as duplicated when both tspconfig and readme contain supported SDK', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.status).toBe('inProgress');
      expect(mockWorkflowContext.isSdkConfigDuplicated).toBe(true);
      expect(mockWorkflowContext.specConfigPath).toBe('some/path/tspconfig.yaml');
      expect(mockWorkflowContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('SDK generation configuration is enabled for both'));
    });

    it('should set status to notEnabled when both configs exist but neither contains supported SDK', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [],
      });

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.status).toBe('notEnabled');
      expect(mockWorkflowContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('No SDKs are enabled for generation'));
    });

    it('should respect skipSdkGenFromOpenapi flag when tspconfig does not support SDK', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = 'some/path/readme.md';
      mockWorkflowContext.config.skipSdkGenFromOpenapi = 'true';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.status).toBe('notEnabled');
      expect(mockWorkflowContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('cannot find supported emitter in tspconfig.yaml'));
    });

    it('should handle missing files gracefully by throwing error for tspconfig', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = undefined;

      vi.mocked(fs.readFileSync).mockImplementation(() => {
        throw new Error('File not found');
      });

      await expect(workflowValidateSdkConfig(mockWorkflowContext)).rejects.toThrow();
    });

    it('should handle missing files gracefully by throwing error for readme', async () => {
      mockWorkflowContext.config.tspConfigPath = undefined;
      mockWorkflowContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(fs.readFileSync).mockImplementation(() => {
        throw new Error('File not found');
      });

      await expect(workflowValidateSdkConfig(mockWorkflowContext)).rejects.toThrow();
    });

    it('should handle management paths differently for samples URL', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/.Management/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = undefined;

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('https://aka.ms/azsdk/tspconfig-sample-mpg'));
    });

    it('should handle data plane URLs for samples', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = undefined;

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);
      vi.mocked(fs.readFileSync).mockImplementation(() => Buffer.from('mock content'));

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('https://aka.ms/azsdk/tspconfig-sample-dpg'));
    });

    it('should only check the supported SDK once with findSDKToGenerateFromTypeSpecProject function', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = undefined;

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(findSDKToGenerateFromTypeSpecProject).toHaveBeenCalledTimes(1);
      expect(findSDKToGenerateFromTypeSpecProject).toHaveBeenCalledWith('mock content', { name: 'test-spec-config' });
    });

    it('should only check the supported SDK once with findSwaggerToSDKConfiguration function', async () => {
      mockWorkflowContext.config.tspConfigPath = undefined;
      mockWorkflowContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(findSwaggerToSDKConfiguration).toHaveBeenCalledTimes(1);
      expect(findSwaggerToSDKConfiguration).toHaveBeenCalledWith('mock content');
    });

    it('should prefer TypeSpec config when both configs are available and valid', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.specConfigPath).toBe('some/path/tspconfig.yaml');
    });

    it('should handle when a different SDK is enabled in tspconfig than the one requested', async () => {
      mockWorkflowContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockWorkflowContext.config.readmePath = undefined;
      mockWorkflowContext.config.sdkName = 'azure-sdk-for-test';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-other']);

      await workflowValidateSdkConfig(mockWorkflowContext);

      expect(mockWorkflowContext.status).toBe('notEnabled');
    });
  });

  describe('workflowCallInitScript', () => {
    beforeEach(() => {
      vi.mocked(writeTmpJsonFile).mockImplementation(() => {});
      vi.mocked(deleteTmpJsonFile).mockImplementation(() => {});
      vi.mocked(readTmpJsonFile).mockReturnValue({ envs: { TEST_ENV: 'test_value' } });
      vi.mocked(getInitOutput).mockReturnValue({ envs: { TEST_ENV: 'test_value' } });
      vi.mocked(runSdkAutoCustomScript).mockResolvedValue('succeeded');
    });

    it('should execute init script successfully', async () => {
      await workflowCallInitScript(mockWorkflowContext);

      expect(writeTmpJsonFile).toHaveBeenCalledWith(mockWorkflowContext, 'initInput.json', {});
      expect(deleteTmpJsonFile).toHaveBeenCalledWith(mockWorkflowContext, 'initOutput.json');
      expect(mockLogger.log).toHaveBeenCalledWith('section', 'Call initScript');
      expect(runSdkAutoCustomScript).toHaveBeenCalledWith(
        mockWorkflowContext,
        'init-script.js',
        expect.objectContaining({
          cwd: mockWorkflowContext.config.localSdkRepoPath,
          statusContext: mockWorkflowContext,
          argTmpFileList: ['initInput.json', 'initOutput.json'],
        }),
      );
      expect(mockLogger.add).toHaveBeenCalledWith(mockCommentCaptureTransport);
      expect(mockLogger.remove).toHaveBeenCalledWith(mockCommentCaptureTransport);
    });

    it('should merge environment variables from init output', async () => {
      const initialEnvs = { ...mockWorkflowContext.scriptEnvs };

      await workflowCallInitScript(mockWorkflowContext);

      expect(mockWorkflowContext.scriptEnvs).toEqual({
        ...initialEnvs,
        TEST_ENV: 'test_value',
      });
    });

    it('should throw error when initScript is not configured', async () => {
      mockWorkflowContext.swaggerToSdkConfig.initOptions = undefined;
      vi.mocked(toolError).mockImplementation(() => 'Init script not configured');
      vi.mocked(setFailureType).mockImplementation(() => {});

      await expect(workflowCallInitScript(mockWorkflowContext)).rejects.toThrow();

      expect(toolError).toHaveBeenCalledWith(expect.stringContaining('initScript is not configured'));
      expect(setFailureType).toHaveBeenCalledWith(mockWorkflowContext, FailureType.SpecGenSdkFailed);
    });

    it('should handle missing init output gracefully', async () => {
      vi.mocked(readTmpJsonFile).mockReturnValue(undefined);

      await workflowCallInitScript(mockWorkflowContext);

      expect(getInitOutput).not.toHaveBeenCalled();
      // Environment should remain unchanged
      expect(mockWorkflowContext.scriptEnvs).not.toHaveProperty('TEST_ENV');
    });
  });

  describe('workflowGenerateSdk', () => {
    it('should generate SDK for tspconfig path', async () => {
      mockWorkflowContext.specConfigPath = 'test/tspconfig.yaml';
      
      await workflowGenerateSdk(mockWorkflowContext);
      
      expect(mockLogger.log).toHaveBeenCalledWith('info', 'Handle the following spec config: test/tspconfig.yaml');
      expect(workflowCallGenerateScript).toHaveBeenCalledWith(mockWorkflowContext, [], [], ['test']);
      expect(mockWorkflowContext.pendingPackages).toHaveLength(0);
      expect(mockWorkflowContext.handledPackages).toHaveLength(1);
    });

    it('should generate SDK for readme path', async () => {
      mockWorkflowContext.specConfigPath = 'test/readme.md';

      await workflowGenerateSdk(mockWorkflowContext);

      expect(workflowCallGenerateScript).toHaveBeenCalledWith(mockWorkflowContext, [], ['test/readme.md'], []);
    });

    it('should handle generation failure gracefully', async () => {
      vi.mocked(workflowCallGenerateScript).mockResolvedValue({
        status: 'failed',
        generateInput: {
          specFolder: 'spec',
          headSha: 'sha123',
          repoHttpsUrl: 'https://github.com/test/repo',
          changedFiles: [],
          runMode: 'test',
          sdkReleaseType: 'beta',
          installInstructionInput: {
            isPublic: true,
            downloadUrlPrefix: '',
            downloadCommandTemplate: 'downloadCommand',
          },
        },
        generateOutput: undefined as any,
      });

      await workflowGenerateSdk(mockWorkflowContext);

      expect(mockLogger.warn).toHaveBeenCalledWith(expect.stringContaining('Package processing is skipped'));
      expect(workflowPkgMain).not.toHaveBeenCalled();
    });

    it('should handle suppression file when it exists', async () => {
      mockWorkflowContext.specConfigPath = 'test/tspconfig.yaml';
      vi.mocked(fs.existsSync).mockReturnValue(true);
      // Make sure we don't use the failed mock that might be set from previous tests
      vi.mocked(workflowCallGenerateScript).mockResolvedValue({
        status: 'succeeded',
        generateInput: {
          specFolder: 'spec',
          headSha: 'sha123',
          repoHttpsUrl: 'https://github.com/test/repo',
          changedFiles: [],
          runMode: 'test',
          sdkReleaseType: 'beta',
          installInstructionInput: {
            isPublic: true,
            downloadUrlPrefix: '',
            downloadCommandTemplate: 'downloadCommand',
          },
        },
        generateOutput: {
          packages: [{ packageName: 'test-package', path: ['test'], result: 'succeeded' }],
        },
      });

      await workflowGenerateSdk(mockWorkflowContext);

      expect(workflowInitGetSdkSuppressionsYml).toHaveBeenCalledWith(mockWorkflowContext, expect.any(Map));
    });

    it('should log error when no config path is provided', async () => {
      mockWorkflowContext.specConfigPath = undefined;

      await workflowGenerateSdk(mockWorkflowContext);

      expect(mockLogger.error).toHaveBeenCalledWith(expect.stringContaining('not provided'));
    });

    it('should process all packages sequentially', async () => {
      vi.mocked(workflowCallGenerateScript).mockResolvedValue({
        status: 'succeeded',
        generateInput: {
          specFolder: 'spec',
          headSha: 'sha123',
          repoHttpsUrl: 'https://github.com/test/repo',
          changedFiles: [],
          runMode: 'test',
          sdkReleaseType: 'beta',
          installInstructionInput: {
            isPublic: true,
            downloadUrlPrefix: '',
            downloadCommandTemplate: 'downloadCommand',
          },
        },
        generateOutput: {
          packages: [
            { packageName: 'package1', path: ['test1'], result: 'succeeded' },
            { packageName: 'package2', path: ['test2'], result: 'succeeded' },
          ],
        },
      });
      vi.mocked(getPackageData)
        .mockReturnValueOnce({ name: 'package1', relativeFolderPath: 'package1', extraRelativeFolderPaths: [] } as any)
        .mockReturnValueOnce({ name: 'package2', relativeFolderPath: 'package2', extraRelativeFolderPaths: [] } as any);

      await workflowGenerateSdk(mockWorkflowContext);

      expect(workflowPkgMain).toHaveBeenCalledTimes(2);
      expect(mockWorkflowContext.handledPackages).toHaveLength(2);
      expect(mockWorkflowContext.pendingPackages).toHaveLength(0);
    });
  });
});
