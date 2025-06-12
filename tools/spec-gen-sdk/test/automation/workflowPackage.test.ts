import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

vi.mock(import('fs'), async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    mkdirSync: vi.fn(),
    existsSync: vi.fn(),
    copyFileSync: vi.fn(),
  };
});

vi.mock(import('path'), async (importOriginal) => {
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

import * as fs from 'fs';
import * as path from 'path';
import { WorkflowContext } from '../../src/automation/workflow';
import { PackageData } from '../../src/types/PackageData';
import { getInstallInstructionScriptOutput, InstallInstructionScriptOutput } from '../../src/types/InstallInstructionScriptOutput';
import { deleteTmpJsonFile, readTmpJsonFile, writeTmpJsonFile } from '../../src/utils/fsUtils';
import { isLineMatch, runSdkAutoCustomScript, setSdkAutoStatus } from '../../src/utils/runScript';
import {workflowPkgMain} from '../../src/automation/workflowPackage';
import * as workflowPackageSteps from '../../src/automation/workflowPackageSteps';
import { CommentCaptureTransport } from '../../src/automation/logging';

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

  describe('workflowPackage', () => {
    it('should execute all workflow steps and set status to succeeded', async () => {

      const mockFn1 = vi.spyOn(workflowPackageSteps, 'workflowPkgCallBuildScript').mockResolvedValue(undefined);
      const mockFn2 = vi.spyOn(workflowPackageSteps, 'workflowPkgCallChangelogScript').mockResolvedValue(undefined);
      const mockFn3 = vi.spyOn(workflowPackageSteps, 'workflowPkgDetectArtifacts').mockResolvedValue(undefined);
      const mockFn4 = vi.spyOn(workflowPackageSteps, 'workflowPkgSaveSDKArtifact').mockResolvedValue(undefined);
      const mockFn5 = vi.spyOn(workflowPackageSteps, 'workflowPkgSaveApiViewArtifact').mockResolvedValue(undefined);
      const mockFn6 = vi.spyOn(workflowPackageSteps, 'workflowPkgCallInstallInstructionScript').mockImplementation(() => {
        return Promise.resolve();
      });
      expect(vi.isMockFunction(workflowPackageSteps.workflowPkgCallInstallInstructionScript)).toBe(true);
      await workflowPkgMain(mockContext, mockPackage);
      expect(mockFn1).toHaveBeenCalled();
      expect(mockContext.logger.log).toHaveBeenCalledWith('section', expect.any(String));
      expect(mockContext.logger.add).toHaveBeenCalledWith(expect.any(CommentCaptureTransport));
      expect(mockContext.logger.remove).toHaveBeenCalledWith(expect.any(CommentCaptureTransport));
      expect(mockPackage.messages).toBeUndefined();
    });
  });
});
