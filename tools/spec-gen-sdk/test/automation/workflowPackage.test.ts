import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// vi.mock(import('fs'), async (importOriginal) => {
//   const actual = await importOriginal();
//   return {
//     ...actual,
//     mkdirSync: vi.fn(),
//     existsSync: vi.fn(),
//     copyFileSync: vi.fn(),
//   };
// });
vi.mock(import('fs'),  () => {
  const actual = vi.importActual<typeof import('fs')>('fs');
  return {
    ...actual,
    mkdirSync: vi.fn(),
    existsSync: vi.fn(),
    copyFileSync: vi.fn(),
  };
});

vi.mock(import("path"), async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    join: vi.fn(),
    relative: vi.fn(),
    basename: vi.fn()
  }
})

vi.mock(import('../../src/utils/fsUtils'), async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    deleteTmpJsonFile: vi.fn().mockImplementation(() => {}),
    readTmpJsonFile: vi.fn().mockImplementation(() => {}),
    writeTmpJsonFile: vi.fn().mockImplementation(() => {}),
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

vi.mock('filehound', () => ({
  default: {
    create: () => ({
      paths: () => ({
        addFilter: () => ({
          find: () => Promise.resolve(['mock/path/artifact.jar']),
        }),
      }),
    }),
  },
}));

import * as fs from 'fs';
import * as path from 'path';
import { WorkflowContext } from '../../src/automation/workflow';
import { PackageData } from '../../src/types/PackageData';
import { getInstallInstructionScriptOutput, InstallInstructionScriptOutput } from '../../src/types/InstallInstructionScriptOutput';
import { deleteTmpJsonFile, readTmpJsonFile, writeTmpJsonFile } from '../../src/utils/fsUtils';
import { isLineMatch, runSdkAutoCustomScript, setSdkAutoStatus } from '../../src/utils/runScript';
import {
  workflowPkgMain,
  workflowPkgCallBuildScript,
  workflowPkgCallChangelogScript,
  workflowPkgDetectArtifacts,
  workflowPkgSaveSDKArtifact,
  workflowPkgSaveApiViewArtifact,
  workflowPkgCallInstallInstructionScript,
} from '../../src/automation/workflowPackage';
import { CommentCaptureTransport } from '../../src/automation/logging';

describe('workflowPackage', () => {
  let mockContext: WorkflowContext;
  let mockPackage: PackageData;

  beforeEach(() => {
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
        artifactOptions: {
          artifactPathFromFileSearch: {
            searchRegex: /\.jar$/,
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
      result: 'succeeded',
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
      installationInstructions: 'mockInstallationInstructions',
      liteInstallationInstruction: 'mockLiteInstallationInstruction',
    } as any;
  });

  afterEach(() => {
    vi.clearAllMocks();
  });
  /**
  describe('workflowPkgMain', () => {
    it('should execute all workflow steps and set status to succeeded', async () => {
      await workflowPkgMain(mockContext, mockPackage);
      
      expect(mockContext.logger.log).toHaveBeenCalledWith('section', expect.any(String));
      expect(mockContext.logger.add).toHaveBeenCalledWith(expect.any(CommentCaptureTransport));
      expect(mockContext.logger.remove).toHaveBeenCalledWith(expect.any(CommentCaptureTransport));
      expect(mockPackage.messages).toBeDefined();
    });
  });

  describe('workflowPkgCallBuildScript', () => {
    it('should skip if buildScript is not configured', async () => {
      mockContext.swaggerToSdkConfig.packageOptions.buildScript = undefined;
      await workflowPkgCallBuildScript(mockContext, mockPackage);
      expect(mockContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('not configured'));
    });

    it('should execute build script with package paths', async () => {
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
    it('should create destination directory and copy artifacts', async () => {
      mockPackage.artifactPaths = ['mock/artifact.jar'];
      await workflowPkgSaveSDKArtifact(mockContext, mockPackage);
      expect(fs.mkdirSync).toHaveBeenCalled();
      expect(mockPackage.serviceName).toBeDefined();
    });

    it('should skip for Go language', async () => {
      mockContext.sdkRepoConfig.mainRepository.name = 'azure-sdk-for-go';
      await workflowPkgSaveSDKArtifact(mockContext, mockPackage);
      expect(fs.copyFileSync).not.toHaveBeenCalled();
    });
  });

  describe('workflowPkgSaveApiViewArtifact', () => {
    it('should skip if no apiView artifact path', async () => {
      mockPackage.apiViewArtifactPath = undefined;
      await workflowPkgSaveApiViewArtifact(mockContext, mockPackage);
      expect(fs.copyFileSync).not.toHaveBeenCalled();
    });

    it('should copy apiView artifact to destination', async () => {
      mockPackage.apiViewArtifactPath = 'mock/apiview.json';
      await workflowPkgSaveApiViewArtifact(mockContext, mockPackage);
      expect(fs.mkdirSync).toHaveBeenCalled();
      expect(fs.copyFileSync).toHaveBeenCalled();
    });
  });
 */
  describe('workflowPkgCallInstallInstructionScript', () => {
    it('should skip if script is not configured', async () => {
      mockContext.swaggerToSdkConfig.artifactOptions.installInstructionScript = undefined;
      await workflowPkgCallInstallInstructionScript(mockContext, mockPackage);
      expect(mockContext.logger.info).toHaveBeenCalledWith('Skip installInstructionScript');
    });

    it('should not generate installation instructions', async () => {
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
