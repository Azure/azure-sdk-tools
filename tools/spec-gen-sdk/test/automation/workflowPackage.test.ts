import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

vi.mock(import('node:fs'), async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    mkdirSync: vi.fn(),
    existsSync: vi.fn(),
    copyFileSync: vi.fn(),
  };
});

vi.mock(import('node:path'), async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    join: vi.fn(),
    relative: vi.fn(),
    basename: vi.fn(),
  };
});

vi.mock('filehound', () => {
  const mockFile = (name: string) => ({ getName: () => name });
  const files = [mockFile('testA.ts'), mockFile('ignore_me.md'), mockFile('testB.ts')];

  const instance: any = {
    paths: vi.fn(() => instance),
    addFilter: vi.fn((filter) => {
      instance.filter = filter;
      return instance;
    }),
    find: vi.fn(async () => files.filter(instance.filter)),
  };

  return {
    default: {
      create: vi.fn(() => instance),
    },
  };
});

vi.mock('../../src/utils/fsUtils', () => {
  const actual = vi.importActual<typeof import('../../src/utils/fsUtils')>('../../src/utils/fsUtils');
  return {
    ...actual,
    deleteTmpJsonFile: vi.fn(),
    readTmpJsonFile: vi.fn(),
    writeTmpJsonFile: vi.fn(),
  };
});

vi.mock('../../src/utils/runScript', () => {
  const actual = vi.importActual<typeof import('../../src/utils/runScript')>('../../src/utils/runScript');
  return {
    ...actual,
    isLineMatch: vi.fn(),
    runSdkAutoCustomScript: vi.fn(),
    setSdkAutoStatus: vi.fn(),
  };
});

vi.mock('../../src/types/InstallInstructionScriptOutput', () => {
  const actual = vi.importActual<typeof import('../../src/types/InstallInstructionScriptOutput')>('../../src/types/InstallInstructionScriptOutput');
  return {
    ...actual,
    getInstallInstructionScriptOutput: vi.fn(),
  };
});

import * as fs from 'node:fs';
import * as path from 'node:path';
import { PackageData } from '../../src/types/PackageData';
import { getInstallInstructionScriptOutput, InstallInstructionScriptOutput } from '../../src/types/InstallInstructionScriptOutput';
import { runSdkAutoCustomScript } from '../../src/utils/runScript';
import {
  workflowPkgCallBuildScript,
  workflowPkgCallChangelogScript,
  workflowPkgCallInstallInstructionScript,
  workflowPkgDetectArtifacts,
  workflowPkgSaveApiViewArtifact,
  workflowPkgSaveSDKArtifact,
} from '../../src/automation/workflowPackage';
import { WorkflowContext } from '../../src/types/Workflow';

describe('workflowPackage', () => {
  let mockContext: WorkflowContext;
  let mockPackage: PackageData;

  beforeEach(() => {
    vi.resetModules();
    mockContext = {
      // tmpFolder: '/mock/tmp/dir',
      logger: {
        log: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        add: vi.fn(),
        remove: vi.fn(),
      },
      config: {
        workingFolder: '/mock/working/dir',
        localSdkRepoPath: '/mock/sdk/repo',
        runEnv: 'test',
      },
      swaggerToSdkConfig: {
        packageOptions: {
          buildScript: {
            command: 'build.sh',
          },
          changelogScript: {
            command: 'changelog.sh',
            breakingChangeDetect: /BREAKING CHANGE/,
          },
        },
        generateOptions: {
          generateScript: 'generate.sh'
        },
        artifactOptions: {
          artifactPathFromFileSearch: {
            searchRegex: /\.jar$/,
            searchFolder: 'sdk/trafficmanager/arm-trafficmanager',
          },
          installInstructionScript: {
            path: 'echo 1',
          },
        },
      },
      sdkRepoConfig: {
        mainRepository: {
          name: 'azure-sdk-for-java',
        },
      },
    } as any;

    mockPackage = {
      serviceName: 'trafficmanager',
      packageName: '@azure/arm-trafficmanager',
      relativeFolderPath: 'sdk/trafficmanager/arm-trafficmanager',
      result: 'succeeded',
      changelogs: [],
      artifactPaths: ['sdk/trafficmanager/arm-trafficmanager/azure-arm-trafficmanager-7.0.0.tgz'],
      readmeMd: ['specification/trafficmanager/resource-manager/readme.md'],
      version: '7.0.0',
      apiViewArtifact: '/mnt/vss/_work/1/s/out/stagedArtifacts/@azure/arm-trafficmanager/arm-trafficmanager.api.json',
      language: 'JavaScript',
      hasBreakingChange: true,
      breakingChangeLabel: 'BreakingChange-JavaScript-Sdk',
      shouldLabelBreakingChange: true,
      areBreakingChangeSuppressed: false,
      presentBreakingChangeSuppressions: [],
      absentBreakingChangeSuppressions: [],
      extraRelativeFolderPaths: [],
      installationInstructions: 'mockInstallationInstructions',
      liteInstallationInstruction: 'mockLiteInstallationInstruction',
    } as any;
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('workflowPkgCallBuildScript', () => {
    it('should skip if buildScript is configured & generateScript is not configured', async () => {
      mockContext.swaggerToSdkConfig.packageOptions.buildScript = undefined;
      await workflowPkgCallBuildScript(mockContext, mockPackage);
      expect(mockContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('GenerateScript configured in swagger_to_sdk_config.json; skipping buildScript.'));
    });

    it('should skip if buildScript is not configured', async () => {
      mockContext.swaggerToSdkConfig.packageOptions.buildScript = undefined;
      mockContext.swaggerToSdkConfig.generateOptions.generateScript = undefined;
      await workflowPkgCallBuildScript(mockContext, mockPackage);
      expect(mockContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('not configured'));
    });

    it('should execute build script with package paths', async () => {
      mockContext.swaggerToSdkConfig.generateOptions.generateScript = undefined;
      await workflowPkgCallBuildScript(mockContext, mockPackage);
      expect(mockContext.logger.log).toHaveBeenCalledWith('section', 'Call BuildScript');
    });
  });

  describe('workflowPkgCallChangelogScript', () => {
    it('should detect breaking changes when present', async () => {
      mockPackage.changelogs = ['Some BREAKING CHANGE detected'];
      await workflowPkgCallChangelogScript(mockContext, mockPackage);
      expect(mockPackage.hasBreakingChange).toBe(true);
    });

    it('should handle missing changelog script configuration', async () => {
      mockContext.swaggerToSdkConfig.packageOptions.changelogScript = undefined;
      mockPackage.changelogs = ['Test changelog'];
      await workflowPkgCallChangelogScript(mockContext, mockPackage);
      expect(mockContext.logger.info).toHaveBeenCalledWith('[Changelog] Test changelog', { showInComment: true });
    });
  });

  describe('workflowPkgDetectArtifacts', () => {
    it('should find artifacts matching search pattern', async () => {
      await workflowPkgDetectArtifacts(mockContext, mockPackage);
      expect(mockPackage.artifactPaths.length).toBeGreaterThan(0);
      expect(mockContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('artifact found'));
    });

    it('should skip if search option is not configured', async () => {
      mockContext.swaggerToSdkConfig.artifactOptions.artifactPathFromFileSearch = undefined;
      await workflowPkgDetectArtifacts(mockContext, mockPackage);
      expect(mockContext.logger.info).toHaveBeenCalledWith('Skip artifact search');
    });
  });

  describe('workflowPkgSaveSDKArtifact', () => {
    it('should skip for Go language', async () => {
      vi.spyOn(path, 'join').mockImplementation(() => 'mocked-join');
      mockContext.sdkRepoConfig.mainRepository.name = 'azure-sdk-for-go';
      await workflowPkgSaveSDKArtifact(mockContext, mockPackage);
      expect(mockPackage.serviceName).toBe('trafficmanager');
      expect(mockContext.stagedArtifactsFolder).toBe('mocked-join');
      expect(fs.copyFileSync).not.toHaveBeenCalled();
    });

    it('should create destination directory and copy artifacts', async () => {
      mockPackage.artifactPaths = ['mock/artifact.jar'];
      await workflowPkgSaveSDKArtifact(mockContext, mockPackage);
      expect(fs.mkdirSync).toHaveBeenCalled();
      expect(mockPackage.serviceName).toBeDefined();
    });
  });

  describe('workflowPkgSaveApiViewArtifact', () => {
    it('should skip if no apiView artifact path', async () => {
      mockPackage.apiViewArtifactPath = undefined;
      await workflowPkgSaveApiViewArtifact(mockContext, mockPackage);
      expect(fs.copyFileSync).not.toHaveBeenCalled();
      expect(path.join).not.toHaveBeenCalled();
    });

    it('should copy apiView artifact to destination', async () => {
      mockPackage.apiViewArtifactPath = 'mock/apiview.json';
      await workflowPkgSaveApiViewArtifact(mockContext, mockPackage);
      expect(fs.mkdirSync).toHaveBeenCalled();
      expect(fs.copyFileSync).toHaveBeenCalled();
      expect(path.basename).toHaveBeenCalledWith('mock/apiview.json');
    });
  });

  describe('workflowPkgCallInstallInstructionScript', () => {
    it('should skip if script is not configured', async () => {
      mockContext.swaggerToSdkConfig.artifactOptions.installInstructionScript = undefined;
      await workflowPkgCallInstallInstructionScript(mockContext, mockPackage);
      expect(mockContext.logger.info).toHaveBeenCalledWith('Skip installInstructionScript');
    });

    it('Handle generate installation instructions faild', async () => {
      vi.mocked(runSdkAutoCustomScript).mockResolvedValue('failed');
      await workflowPkgCallInstallInstructionScript(mockContext, mockPackage);
      expect(mockPackage.installationInstructions).toBe('mockInstallationInstructions');
      expect(mockPackage.liteInstallationInstruction).toBe('mockLiteInstallationInstruction');
      expect(mockContext.logger.log).toHaveBeenNthCalledWith(1, 'section', 'Call InstallInstructionScript');
      expect(mockContext.logger.log).toHaveBeenCalledTimes(2);
    });

    it('should generate installation instructions', async () => {
      vi.mocked(runSdkAutoCustomScript).mockResolvedValue('inProgress');
      vi.mocked(getInstallInstructionScriptOutput).mockReturnValue({
        full: 'full instructions',
        lite: 'lite instructions',
      } as InstallInstructionScriptOutput);
      await workflowPkgCallInstallInstructionScript(mockContext, mockPackage);
      expect(mockPackage.installationInstructions).toBe('full instructions');
      expect(mockPackage.liteInstallationInstruction).toBe('lite instructions');
      expect(mockContext.logger.log).toHaveBeenCalledWith('section', 'Call InstallInstructionScript');
      expect(mockContext.logger.log).toHaveBeenCalledTimes(2);
    });
  });
});
