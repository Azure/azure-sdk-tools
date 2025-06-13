import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import { WorkflowContext, FailureType } from '../../src/types/Workflow';
import { workflowCallInitScript, workflowGenerateSdk, workflowValidateSdkConfig } from '../../src/automation/workflowSteps';
import * as typespecUtils from '../../src/utils/typespecUtils';
import * as readme from '../../src/utils/readme';
import winston from 'winston';
import { RepoKey } from '../../src/utils/repo';
import Transport from 'winston-transport';
import { SwaggerToSdkConfig } from '../../src/types/SwaggerToSdkConfig';

vi.mock('fs');
vi.mock('path');

describe('workflowSteps', () => {
  let context: WorkflowContext;
  
  beforeEach(() => {
    vi.resetAllMocks();
    
    const logger = {
      log: vi.fn(),
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      add: vi.fn(),
      remove: vi.fn()
    } as unknown as winston.Logger;

    const mockConfig: SwaggerToSdkConfig = {
      advancedOptions: {
        createSdkPullRequests: true,
        closeIntegrationPR: false,
        draftIntegrationPR: true,
        draftGenerationPR: true,
      },
      generateOptions: {
        preprocessDryRunGetPackageName: false,
        parseGenerateOutput: true,
      },
      packageOptions: {
        packageFolderFromFileSearch: false,
      },
      artifactOptions: {}
    };

    context = {
      config: {
        localSpecRepoPath: '/spec',
        localSdkRepoPath: '/sdk',
        sdkName: 'test-sdk',
        tspConfigPath: undefined,
        readmePath: undefined,
        skipSdkGenFromOpenapi: 'false',
        specRepo: {
          owner: 'test',
          name: 'repo'
        } as RepoKey,
        branchPrefix: 'test',
        runMode: 'normal',
        sdkReleaseType: 'preview',
        specCommitSha: 'abc123',
        specRepoHttpsUrl: 'https://github.com/test/repo',
        workingFolder: '/tmp',
        runEnv: 'test',
        version: '1.0.0'
      },
      specRepoConfig: {
        sdkRepositoryMappings: {},
        overrides: {},
        typespecEmitterToSdkRepositoryMapping: {}
      },
      sdkRepoConfig: {
        mainRepository: { owner: 'test', name: 'sdk' },
        mainBranch: 'main',
        integrationRepository: { owner: 'test', name: 'sdk-int' },
        secondaryRepository: { owner: 'test', name: 'sdk-sec' },
        secondaryBranch: 'dev',
        integrationBranchPrefix: 'integration_',
        configFilePath: 'config.json'
      },
      swaggerToSdkConfig: mockConfig,
      logger,
      fullLogFileName: 'full.log',
      filteredLogFileName: 'filtered.log',
      htmlLogFileName: 'report.html',
      vsoLogFileName: 'vso.log',
      isPrivateSpecRepo: false,
      messageCaptureTransport: {} as Transport,
      status: 'inProgress',
      handledPackages: [],
      pendingPackages: [],
      scriptEnvs: {},
      messages: [],
      tmpFolder: '/tmp',
      vsoLogs: new Map()
    };
  });

  describe('workflowValidateSdkConfig', () => {
    it('should throw error when no config paths provided', async () => {
      await expect(workflowValidateSdkConfig(context)).rejects.toThrow(
        "'tspConfigPath' and 'readmePath' are not provided"
      );
    });

    it('should set status to notEnabled when SDK not enabled in tspconfig', async () => {
      context.config.tspConfigPath = 'test/tspconfig.yaml';
      vi.spyOn(fs, 'readFileSync').mockReturnValue('emitters: {}');
      vi.spyOn(typespecUtils, 'findSDKToGenerateFromTypeSpecProject').mockReturnValue([]);
      vi.spyOn(path, 'join').mockReturnValue('/spec/test/tspconfig.yaml');

      await workflowValidateSdkConfig(context);
      expect(context.status).toBe('notEnabled');
    });

    it('should set status to notEnabled when SDK not enabled in readme', async () => {
      context.config.readmePath = 'test/readme.md';
      vi.spyOn(fs, 'readFileSync').mockReturnValue('# Test');
      vi.spyOn(readme, 'findSwaggerToSDKConfiguration').mockReturnValue({ repositories: [] });
      vi.spyOn(path, 'join').mockReturnValue('/spec/test/readme.md');

      await workflowValidateSdkConfig(context);
      expect(context.status).toBe('notEnabled');
    });

    it('should set specConfigPath when SDK enabled in tspconfig', async () => {
      context.config.tspConfigPath = 'test/tspconfig.yaml';
      vi.spyOn(fs, 'readFileSync').mockReturnValue('emitters: {}');
      vi.spyOn(typespecUtils, 'findSDKToGenerateFromTypeSpecProject').mockReturnValue(['test-sdk']);
      vi.spyOn(path, 'join').mockReturnValue('/spec/test/tspconfig.yaml');

      await workflowValidateSdkConfig(context);
      expect(context.specConfigPath).toBe('test/tspconfig.yaml');
    });

    it('should set specConfigPath when SDK enabled in readme', async () => {
      context.config.readmePath = 'test/readme.md';
      vi.spyOn(fs, 'readFileSync').mockReturnValue('# Test');
      vi.spyOn(readme, 'findSwaggerToSDKConfiguration').mockReturnValue({
        repositories: [{
          repo: 'test-sdk',
          after_scripts: []
        }]
      });
      vi.spyOn(path, 'join').mockReturnValue('/spec/test/readme.md');

      await workflowValidateSdkConfig(context);
      expect(context.specConfigPath).toBe('test/readme.md');
    });

    it('should handle when SDK enabled in both configs', async () => {
      context.config.tspConfigPath = 'test/tspconfig.yaml';
      context.config.readmePath = 'test/readme.md';
      
      vi.spyOn(fs, 'readFileSync').mockImplementation((path: fs.PathOrFileDescriptor) => {
        if (path.toString().includes('tspconfig.yaml')) return 'emitters: {}';
        return '# Test';
      });
      
      vi.spyOn(typespecUtils, 'findSDKToGenerateFromTypeSpecProject').mockReturnValue(['test-sdk']);
      vi.spyOn(readme, 'findSwaggerToSDKConfiguration').mockReturnValue({
        repositories: [{
          repo: 'test-sdk',
          after_scripts: []
        }]
      });
      
      vi.spyOn(path, 'join').mockImplementation((_, p) => '/spec/' + p);

      await workflowValidateSdkConfig(context);
      expect(context.specConfigPath).toBe('test/tspconfig.yaml');
      expect(context.isSdkConfigDuplicated).toBe(true);
    });
  });

  describe('workflowGenerateSdk', () => {
    it('should skip when no specConfigPath is set', async () => {
      const loggerErrorSpy = vi.spyOn(context.logger, 'error');
      
      await workflowGenerateSdk(context);
      
      expect(loggerErrorSpy).toHaveBeenCalledWith(
        expect.stringContaining("'tspConfigPath' and 'readmePath' are not provided")
      );
    });

    it('should process typespec project when tspconfig.yaml path is provided', async () => {
      context.specConfigPath = 'test/tspconfig.yaml';
      vi.spyOn(fs, 'existsSync').mockReturnValue(false);
      
      await workflowGenerateSdk(context);
      
      expect(context.pendingPackages).toEqual([]);
    });

    it('should process readme when readme.md path is provided', async () => {
      context.specConfigPath = 'test/readme.md';
      vi.spyOn(fs, 'existsSync').mockReturnValue(false);
      
      await workflowGenerateSdk(context);
      
      expect(context.pendingPackages).toEqual([]);
    });
  });

  describe('workflowCallInitScript', () => {
    it('should throw error when initScript is not configured', async () => {
      context.swaggerToSdkConfig = {
        ...context.swaggerToSdkConfig,
        initOptions: undefined
      };
      
      await expect(workflowCallInitScript(context)).rejects.toThrow(
        'initScript is not configured in the swagger-to-sdk config'
      );
    });

    it('should update scriptEnvs when initOutput contains envs', async () => {
      context.swaggerToSdkConfig = {
        ...context.swaggerToSdkConfig,
        initOptions: {
          initScript: {
            path: 'init.sh',
            envs: [],
          }
        }
      };
      
      vi.mock('../../src/utils/fsUtils', () => ({
        writeTmpJsonFile: vi.fn(),
        deleteTmpJsonFile: vi.fn(),
        readTmpJsonFile: vi.fn().mockReturnValue({
          envs: { TEST_ENV: 'value' }
        })
      }));
      
      vi.mock('../../src/utils/runScript', () => ({
        runSdkAutoCustomScript: vi.fn()
      }));

      await workflowCallInitScript(context);
      
      expect(context.scriptEnvs).toEqual({ TEST_ENV: 'value' });
    });
  });
});
