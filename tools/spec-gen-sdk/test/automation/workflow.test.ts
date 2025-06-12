import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
vi.mock('../src/types/validator', () => ({
  getTypeTransformer: () => (data) => data,
}));
// vi.mock('path', () => {
//   return {
//     ...vi.importActual('path'),
//     join: vi.fn()
//   };
// });
vi.mock('path', async () => {
  const actual = await vi.importActual<typeof import('path')>('path');
  return {
    ...actual,
    join: vi.fn((...args) => args.join('/')),
  };
});
// vi.mock(import('path'), async (importOriginal) => {
//   const actual = await importOriginal();
//   return {
//     ...actual,
//     join: vi.fn(),
//     relative: vi.fn(),
//     basename: vi.fn(),
//   };
// });

vi.mock('fs', () => {
  const actual = vi.importActual('fs');
  return {
    ...actual,
    readFileSync: vi.fn((path) => {
      // For template files, return a mock template
      if (path.includes('templates')) {
        return Buffer.from('Mock template content');
      }
      return Buffer.from('mock content');
    }),
    existsSync: vi.fn(() => true),
  };
});

vi.mock('../../src/utils/typespecUtils', () => {
  const actual = vi.importActual<typeof import('../../src/utils/typespecUtils')>('../../src/utils/typespecUtils');
  return {
    ...actual,
    findSDKToGenerateFromTypeSpecProject: vi.fn(),
  };
});

vi.mock('../../src/utils/readme', () => {
  const actual = vi.importActual<typeof import('../../src/utils/readme')>('../../src/utils/readme');
  return {
    ...actual,
    findSwaggerToSDKConfiguration: vi.fn(),
  };
});

vi.mock('../../src/utils/runScript', () => {
  const actual = vi.importActual<typeof import('../../src/utils/runScript')>('../../src/utils/runScript');
  return {
    ...actual,
    runSdkAutoCustomScript: vi.fn().mockResolvedValue('succeeded'),
    setSdkAutoStatus: vi.fn(),
  };
});

vi.mock('../../src/utils/fsUtils', () => {
  const actual = vi.importActual<typeof import('../../src/utils/fsUtils')>('../../src/utils/fsUtils');
  return {
    ...actual,
    writeTmpJsonFile: vi.fn(),
    readTmpJsonFile: vi.fn(),
    deleteTmpJsonFile: vi.fn(),
  };
});

import * as fs from 'fs';
import * as path from 'path';
// import path from 'path';
import {
  WorkflowContext,
  workflowInit,
  workflowMain,
  workflowValidateSdkConfig,
  workflowCallInitScript,
  workflowGenerateSdk,
  FailureType,
  setFailureType,
} from '../../src/automation/workflow';
import { findSDKToGenerateFromTypeSpecProject } from '../../src/utils/typespecUtils';
import { findSwaggerToSDKConfiguration } from '../../src/utils/readme';
import { CommentCaptureTransport } from '../../src/automation/logging';
import { getInitOutput } from '../../src/types/InitOutput';
import { getGenerateOutput } from '../../src/types/GenerateOutput';
import * as fsUtils from '../../src/utils/fsUtils';

describe('workflow', () => {
  let mockContext: any;

  beforeEach(() => {
    vi.mocked(path.join).mockImplementation((...args) => args.join('/'));
    vi.mocked(fs.readFileSync).mockReturnValue(Buffer.from('mock content'));
    vi.mocked(fs.existsSync).mockReturnValue(true);
    mockContext = {
      config: {
        workingFolder: '/mock/working/dir',
        localSdkRepoPath: '/mock/sdk/repo',
        localSpecRepoPath: '/mock/spec/repo',
        tspConfigPath: 'mock/tspconfig.yaml',
        readmePath: 'mock/readme.md',
        sdkName: 'azure-sdk-for-test',
        specCommitSha: 'mock-sha',
        runMode: 'test',
        sdkReleaseType: 'beta',
      },
      sdkRepoConfig: {
        mainRepository: {
          name: 'test-repo',
        },
      },
      specRepoConfig: {},
      logger: {
        log: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
        add: vi.fn(),
        remove: vi.fn(),
      },
      status: 'inProgress',
      specConfigPath: undefined,
      isSdkConfigDuplicated: undefined,
    };
  });

  afterEach(() => {
    // vi.restoreAllMocks();
    // vi.resetModules();
    vi.clearAllMocks();
  });

  describe('setFailureType', () => {
    it('should set failure type when not already CodegenFailed', () => {
      const context = { failureType: undefined } as WorkflowContext;
      setFailureType(context, FailureType.SpecGenSdkFailed);
      expect(context.failureType).toBe(FailureType.SpecGenSdkFailed);
    });

    it('should not override CodegenFailed failure type', () => {
      const context = { failureType: FailureType.CodegenFailed } as WorkflowContext;
      setFailureType(context, FailureType.SpecGenSdkFailed);
      expect(context.failureType).toBe(FailureType.CodegenFailed);
    });
  });

  /**
  describe('workflowInit', () => {
    it('should initialize workflow context with required fields', async () => {
      vi.spyOn(path, 'join').mockReturnValue('/mock/tmp/dir');

      const context = await workflowInit(mockContext);

      expect(context.pendingPackages).toEqual([]);
      expect(context.handledPackages).toEqual([]);
      expect(context.status).toBe('inProgress');
      expect(context.messages).toBeDefined();
      expect(context.messageCaptureTransport).toBeInstanceOf(CommentCaptureTransport);
      expect(context.tmpFolder).toBe('/mock/tmp/dir');
      expect(fs.mkdirSync).toHaveBeenCalledWith('/mock/tmp/dir', { recursive: true });
    });
  });

  describe('workflowMain', () => {
    it('should execute workflow steps in correct order', async () => {
      const validateSpy = vi.spyOn(require('../../src/automation/workflow'), 'workflowValidateSdkConfig');
      const initSpy = vi.spyOn(require('../../src/automation/workflow'), 'workflowCallInitScript');
      const genSpy = vi.spyOn(require('../../src/automation/workflow'), 'workflowGenerateSdk');

      await workflowMain(mockContext);

      expect(validateSpy).toHaveBeenCalled();
      expect(initSpy).toHaveBeenCalled();
      expect(genSpy).toHaveBeenCalled();
    });

    it('should skip remaining steps if status is notEnabled', async () => {
      const validateSpy = vi.spyOn(require('../../src/automation/workflow'), 'workflowValidateSdkConfig')
        .mockImplementation(async () => {
          mockContext.status = 'notEnabled';
        });
      const initSpy = vi.spyOn(require('../../src/automation/workflow'), 'workflowCallInitScript');

      await workflowMain(mockContext);

      expect(validateSpy).toHaveBeenCalled();
      expect(initSpy).not.toHaveBeenCalled();
    });
  });
 */
  describe('workflowValidateSdkConfig', () => {
    it('should throw error when neither tspConfigPath nor readmePath are provided', async () => {
      // vi.spyOn(path, 'join').mockImplementation(() => 'mocked-join');
      // vi.spyOn(path, 'join').mockImplementation((...args: string[]): any => {
      //   const joined = args.join('/');
      //   // Instead of returning undefined, return a special string to simulate "not found"
      //   if (joined.includes(mockContext.config.tspConfigPath)) {
      //     return undefined;
      //   } else if (joined.includes(mockContext.config.readmePath)) {
      //     return undefined;
      //   } else {
      //     return 'aaa';
      //   }
      // });
      //   vi.mock('path', async () => {
      //     const actual = await vi.importActual<typeof import('path')>('path');
      //     return {
      //       ...actual,
      //       join: vi.fn(() => 'mocked-join'),
      //     };
      //   });
      //   const { workflowValidateSdkConfig } = await import('../../src/automation/workflow');
      // console.warn('path.join is mocked');
      vi.mocked(path.join).mockImplementation((...args: string[]): any => {
        const joined = args.join('/');
        // Instead of returning undefined, return a special string to simulate "not found"
        if (joined.includes(mockContext.config.tspConfigPath)) {
          return undefined;
        } else if (joined.includes(mockContext.config.readmePath)) {
          return undefined;
        } else {
          return joined;
        }
      });
      await expect(workflowValidateSdkConfig(mockContext)).rejects.toThrow(/.*'tspConfigPath' and 'readmePath' are not provided.*/);
    });

    // it('reset join', async () => {
    //   vi.unmock('path');

    //   const { workflowValidateSdkConfig } = await import('../../src/automation/workflow');
    //   await expect(workflowValidateSdkConfig(mockContext)).rejects.toThrow(/.*'tspConfigPath' and 'readmePath' are not provided.*/);

    // //   expect(path.join('a', 'b')).not.toBe('mocked'); // ✅ 已恢复
    // });
    it('should set status to notEnabled when tspconfig does not contain supported SDK', async () => {
      mockContext.config.tspConfigPath = 'path/tspconfig.yaml';
      mockContext.config.readmePath = undefined;
      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.status).toBe('notEnabled');
      expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('cannot find supported emitter in tspconfig.yaml'));
    });

    it('should set status to notEnabled when readme does not contain supported SDK', async () => {
      mockContext.config.tspConfigPath = undefined;
      mockContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({ repositories: [] });

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.status).toBe('notEnabled');
      expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining("'swagger-to-sdk' section cannot be found"));
    });

    it('should handle TypeSpec config path correctly', async () => {
      mockContext.config.tspConfigPath = 'path/tspconfig.yaml';
      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.status).toBe('inProgress');
      expect(mockContext.specConfigPath).toBe('path/tspconfig.yaml');
      expect(mockContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('SDK to generate:azure-sdk-for-test'));
    });

    it('should handle readme path correctly', async () => {
      mockContext.config.tspConfigPath = undefined;
      mockContext.config.readmePath = 'path/readme.md';
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.status).toBe('inProgress');
      expect(mockContext.specConfigPath).toBe('path/readme.md');
      expect(mockContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('SDK to generate:azure-sdk-for-test'));
    });

    it('should mark as duplicated when both tspconfig and readme contain supported SDK', async () => {
      mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.status).toBe('inProgress');
      expect(mockContext.isSdkConfigDuplicated).toBe(true);
      expect(mockContext.specConfigPath).toBe('some/path/tspconfig.yaml');
      expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('SDK generation configuration is enabled for both'));
    });

    it('should set status to notEnabled when both configs exist but neither contains supported SDK', async () => {
      mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [],
      });

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.status).toBe('notEnabled');
      expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('No SDKs are enabled for generation'));
    });

    it('should respect skipSdkGenFromOpenapi flag when tspconfig does not support SDK', async () => {
      mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockContext.config.readmePath = 'some/path/readme.md';
      mockContext.config.skipSdkGenFromOpenapi = 'true';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.status).toBe('notEnabled');
      expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('cannot find supported emitter in tspconfig.yaml'));
    });

    it('should handle missing files gracefully by throwing error for tspconfig', async () => {
      mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockContext.config.readmePath = undefined;

      vi.mocked(fs.readFileSync).mockImplementation(() => {
        throw new Error('File not found');
      });

      await expect(workflowValidateSdkConfig(mockContext)).rejects.toThrow();
    });

    it('should handle missing files gracefully by throwing error for readme', async () => {
      mockContext.config.tspConfigPath = undefined;
      mockContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(fs.readFileSync).mockImplementation(() => {
        throw new Error('File not found');
      });

      await expect(workflowValidateSdkConfig(mockContext)).rejects.toThrow();
    });

    it('should handle management paths differently for samples URL', async () => {
      mockContext.config.tspConfigPath = 'some/path/.Management/tspconfig.yaml';
      mockContext.config.readmePath = undefined;

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('https://aka.ms/azsdk/tspconfig-sample-mpg'));
    });

    it('should handle data plane URLs for samples', async () => {
      mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockContext.config.readmePath = undefined;

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('https://aka.ms/azsdk/tspconfig-sample-dpg'));
    });

    it('should only check the supported SDK once with findSDKToGenerateFromTypeSpecProject function', async () => {
      mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockContext.config.readmePath = undefined;

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);

      await workflowValidateSdkConfig(mockContext);

      expect(findSDKToGenerateFromTypeSpecProject).toHaveBeenCalledTimes(1);
      expect(findSDKToGenerateFromTypeSpecProject).toHaveBeenCalledWith('mock content', {});
    });

    it('should only check the supported SDK once with findSwaggerToSDKConfiguration function', async () => {
      mockContext.config.tspConfigPath = undefined;
      mockContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockContext);

      expect(findSwaggerToSDKConfiguration).toHaveBeenCalledTimes(1);
      expect(findSwaggerToSDKConfiguration).toHaveBeenCalledWith('mock content');
    });

    it('should prefer TypeSpec config when both configs are available and valid', async () => {
      mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockContext.config.readmePath = 'some/path/readme.md';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);
      vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
        repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
      });

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.specConfigPath).toBe('some/path/tspconfig.yaml');
    });

    it('should handle when a different SDK is enabled in tspconfig than the one requested', async () => {
      mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
      mockContext.config.readmePath = undefined;
      mockContext.config.sdkName = 'azure-sdk-for-test';

      vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-other']);

      await workflowValidateSdkConfig(mockContext);

      expect(mockContext.status).toBe('notEnabled');
    });
  });

  describe('workflowGenerateSdk', () => {
    beforeEach(() => {
      mockContext.swaggerToSdkConfig = {
        generateOptions: {
          generateScript: {
            command: 'generate.sh',
          },
        },
      };
      mockContext.messageCaptureTransport = new CommentCaptureTransport({
        extraLevelFilter: ['error', 'warn'],
        level: 'debug',
        output: [],
      });
      mockContext.pendingPackages = [];
      mockContext.handledPackages = [];
    });

    it('should handle missing generate script configuration', async () => {
      mockContext.specConfigPath = undefined;
      // mockContext.swaggerToSdkConfig.generateOptions.generateScript = undefined;
      // await expect(workflowGenerateSdk(mockContext)).rejects.toThrow(/generateScript is not configured/);
      // expect(setFailureType).toHaveBeenCalledOnce();
      await workflowGenerateSdk(mockContext);
      expect(mockContext.logger.error).toHaveBeenCalledWith(expect.stringContaining("'tspConfigPath' and 'readmePath' are not provided. Please provide at least one of them."));
    });

    it('should process generate output and update packages', async () => {
      vi.mocked(fsUtils.readTmpJsonFile).mockReturnValue({
        packages: [
          {
            packageName: 'test-package',
            path: ['path/to/package'],
            result: 'succeeded',
          },
        ],
      });
      mockContext.specConfigPath = 'mock/spec/config/path';
      await workflowGenerateSdk(mockContext);

      expect(mockContext.logger.log).toHaveBeenCalledWith('section', 'Detect changed packages');
      expect(mockContext.handledPackages.length).toBeGreaterThan(0);
    });

    it('should warn when no packages are detected', async () => {
      vi.mocked(fsUtils.readTmpJsonFile).mockReturnValue({ packages: [] });

      await workflowGenerateSdk(mockContext);

      expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('No package detected after generation'));
    });
  });

  /**
  describe('workflowCallInitScript', () => {
    beforeEach(() => {
      mockContext.swaggerToSdkConfig = {
        initOptions: {
          initScript: {
            command: 'init.sh'
          }
        }
      };
      mockContext.messageCaptureTransport = new CommentCaptureTransport({
        extraLevelFilter: ['error', 'warn'],
        level: 'debug',
        output: []
      });
    });

    it('should handle missing init script configuration', async () => {
      mockContext.swaggerToSdkConfig.initOptions.initScript = undefined;

      await expect(workflowCallInitScript(mockContext)).rejects.toThrow(
        /initScript is not configured/
      );
    });

    it('should execute init script and update scriptEnvs', async () => {
      vi.mocked(fsUtils.readTmpJsonFile).mockReturnValue({
        envs: { TEST_ENV: 'test-value' }
      });

      await workflowCallInitScript(mockContext);

      expect(mockContext.logger.log).toHaveBeenCalledWith('section', 'Call initScript');
      expect(mockContext.scriptEnvs).toHaveProperty('TEST_ENV', 'test-value');
    });
  });


  describe('workflowDetectChangedPackages', () => {
    beforeEach(() => {
      mockContext.pendingPackages = [
        { relativeFolderPath: 'pkg1', extraRelativeFolderPaths: [] },
        { relativeFolderPath: 'pkg2', extraRelativeFolderPaths: ['extra1', 'extra2'] }
      ];
      mockContext.handledPackages = [];
    });

    it('should log detected packages', () => {
      require('../../src/automation/workflow').workflowDetectChangedPackages(mockContext);

      expect(mockContext.logger.log).toHaveBeenCalledWith('section', 'Detect changed packages');
      expect(mockContext.logger.info).toHaveBeenCalledWith(`2 packages found after generation:`);
      expect(mockContext.logger.info).toHaveBeenCalledWith(`\tpkg1`);
      expect(mockContext.logger.info).toHaveBeenCalledWith(`\tpkg2`);
      expect(mockContext.logger.info).toHaveBeenCalledWith(`\t- extra1`);
      expect(mockContext.logger.info).toHaveBeenCalledWith(`\t- extra2`);
    });

    it('should warn when no packages are detected', () => {
      mockContext.pendingPackages = [];
      require('../../src/automation/workflow').workflowDetectChangedPackages(mockContext);

      expect(mockContext.logger.warn).toHaveBeenCalledWith(
        expect.stringContaining('No package detected after generation')
      );
    });
  });

  describe('workflowCallGenerateScript', () => {
    beforeEach(() => {
      mockContext.config.specRepoHttpsUrl = 'https://github.com/test/repo';
      mockContext.scriptEnvs = {};
      mockContext.swaggerToSdkConfig = {
        generateOptions: {
          generateScript: {
            command: 'generate.sh'
          }
        }
      };
    });

    it('should handle missing generate script configuration', async () => {
      mockContext.swaggerToSdkConfig.generateOptions.generateScript = undefined;

      const { workflow } = require('../../src/automation/workflow');
      await expect(workflow.workflowCallGenerateScript(
        mockContext, [], [], []
      )).rejects.toThrow(/generateScript is not configured/);
    });

    it('should prepare generate input and process output', async () => {
      const mockOutput = {
        packages: [{
          packageName: 'test-package',
          path: ['path/to/package'],
          result: 'succeeded'
        }]
      };
      vi.mocked(fsUtils.readTmpJsonFile).mockReturnValue(mockOutput);

      const result = await require('../../src/automation/workflow').workflowCallGenerateScript(
        mockContext,
        ['file1.ts'],
        ['readme1.md'],
        ['typespec1']
      );

      expect(result.status).toBe('succeeded');
      expect(result.generateOutput).toBeDefined();
      expect(fsUtils.writeTmpJsonFile).toHaveBeenCalledWith(
        mockContext,
        'generateInput.json',
        expect.objectContaining({
          specFolder: expect.any(String),
          headSha: 'mock-sha',
          changedFiles: ['file1.ts']
        })
      );
    });
  });

  describe('workflowInitGetSdkSuppressionsYml', () => {
    beforeEach(() => {
      vi.mocked(fs.readFileSync).mockReturnValue(Buffer.from('suppressions:\n  package1:\n    - id: "test"'));
    });

    it('should process suppression files from configuration', async () => {
      const suppressionMap = new Map();
      suppressionMap.set('path/config.yaml', 'path/suppressions.yml');

      const result = await require('../../src/automation/workflow').workflowInitGetSdkSuppressionsYml(
        mockContext,
        suppressionMap
      );

      expect(result.size).toBeGreaterThan(0);
      expect(mockContext.logger.log).toHaveBeenCalledWith('section', 'Load SDK suppressions');
    });

    it('should handle file read errors', async () => {
      const suppressionMap = new Map();
      suppressionMap.set('path/config.yaml', 'invalid/path.yml');
      vi.mocked(fs.readFileSync).mockImplementation(() => {
        throw new Error('File not found');
      });

      await require('../../src/automation/workflow').workflowInitGetSdkSuppressionsYml(
        mockContext,
        suppressionMap
      );

      expect(mockContext.logger.error).toHaveBeenCalledWith(
        expect.stringContaining('Fails to read SDK suppressions file')
      );
    });
  });
 */
});
