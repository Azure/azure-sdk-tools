vi.mock('node:fs', () => ({
  existsSync: vi.fn(),
  readFileSync: vi.fn(() => Buffer.from('{{mock handlebars template}}')),
  rmSync: vi.fn(),
  writeFileSync: vi.fn(),
}));

vi.mock('node:path', () => ({
  ...vi.importActual('node:path'),
  join: vi.fn((...args) => args.join('/')),
}));

vi.mock('../../src/utils/runScript');
vi.mock('../../src/utils/utils');
vi.mock('../../src/automation/logging');
vi.mock('../../src/utils/fsUtils');
vi.mock('../../src/utils/messageUtils');
vi.mock('../../src/utils/workflowUtils');
vi.mock('../../src/utils/reportStatusUtils');
vi.mock('marked', () => ({
  marked: vi.fn(() => '<p>mocked markdown</p>'),
}));

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs';
import { generateReport, saveVsoLog, saveFilteredLog, generateHtmlFromFilteredLog } from '../../src/automation/reportStatus';
import { WorkflowContext, FailureType } from '../../src/types/Workflow';
import { SDKAutomationState } from '../../src/automation/sdkAutomationState';
import { PackageData } from '../../src/types/PackageData';

// Import mocked modules
import { setSdkAutoStatus } from '../../src/utils/runScript';
import { extractPathFromSpecConfig, mapToObject } from '../../src/utils/utils';
import { vsoAddAttachment, vsoLogError, vsoLogWarning } from '../../src/automation/logging';
import { deleteTmpJsonFile, writeTmpJsonFile } from '../../src/utils/fsUtils';
import { toolError, toolWarning } from '../../src/utils/messageUtils';
import { setFailureType } from '../../src/utils/workflowUtils';
import { commentDetailView, renderHandlebarTemplate } from '../../src/utils/reportStatusUtils';
import { marked } from 'marked';

describe('reportStatus', () => {
  let mockWorkflowContext: WorkflowContext;
  let mockLogger: any;
  let mockPackageData: PackageData;

  beforeEach(() => {
    vi.clearAllMocks();
    
    mockLogger = {
      log: vi.fn(),
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
    };

    mockPackageData = {
      name: 'test-package',
      serviceName: 'test-service',
      relativeFolderPath: 'packages/test-package',
      extraRelativeFolderPaths: [],
      status: 'succeeded' as SDKAutomationState,
      messages: [],
      hasBreakingChange: false,
      isPrivatePackage: false,
      artifactPaths: ['artifact1.zip', 'artifact2.zip'],
      readmeMd: ['README.md'],
      typespecProject: undefined,
      version: '1.0.0',
      apiViewArtifactPath: 'apiview.json',
      language: 'JavaScript',
      isBetaMgmtSdk: false,
      isDataPlane: false,
      presentSuppressionLines: [],
      absentSuppressionLines: [],
      installationInstructions: 'npm install test-package',
      liteInstallationInstruction: undefined,
      // Required fields for SDKRepositoryPackageData
      changedFilePaths: [],
      generationBranch: 'generation-branch',
      generationRepository: 'test-repo',
      generationRepositoryUrl: 'https://github.com/test/repo',
      integrationBranch: 'integration-branch',
      integrationRepository: 'test-integration-repo',
    } as unknown as PackageData;

    mockWorkflowContext = {
      config: {
        specRepo: { owner: 'Azure', name: 'azure-rest-api-specs' },
        sdkName: 'azure-sdk-for-js',
        branchPrefix: 'sdkAuto',
        localSpecRepoPath: '/spec/repo',
        localSdkRepoPath: '/sdk/repo',
        tspConfigPath: '/spec/tspconfig.yaml',
        readmePath: '/spec/README.md',
        pullNumber: '123',
        apiVersion: '2023-01-01',
        runMode: 'test',
        sdkReleaseType: 'beta',
        specCommitSha: 'abc123',
        specRepoHttpsUrl: 'https://github.com/Azure/azure-rest-api-specs',
        workingFolder: '/tmp/working',
        headRepoHttpsUrl: 'https://github.com/user/azure-rest-api-specs',
        headBranch: 'feature-branch',
        runEnv: 'azureDevOps' as const,
        version: '1.0.0',
        skipSdkGenFromOpenapi: 'false',
      },
      logger: mockLogger,
      fullLogFileName: '/tmp/full.log',
      filteredLogFileName: '/tmp/filtered.log',
      htmlLogFileName: '/tmp/report.html',
      vsoLogFileName: '/tmp/vso.log',
      specRepoConfig: {
        sdkRepositoryMappings: {},
        overrides: {},
        typespecEmitterToSdkRepositoryMapping: {},
      },
      sdkRepoConfig: {
        configFilePath: 'swagger_to_sdk_config.json',
        mainRepository: { owner: 'test', name: 'test-repo' },
        mainBranch: 'main',
        integrationRepository: { owner: 'test', name: 'test-integration-repo' },
        secondaryRepository: { owner: 'test', name: 'test-secondary-repo' },
        secondaryBranch: 'secondary',
        integrationBranchPrefix: 'integration-',
      },
      swaggerToSdkConfig: {
        packageOptions: {
          breakingChangesLabel: 'breaking-change',
        },
      },
      isPrivateSpecRepo: false,
      stagedArtifactsFolder: '/tmp/artifacts',
      sdkArtifactFolder: '/tmp/sdk-artifacts',
      isSdkConfigDuplicated: false,
      specConfigPath: 'specification/test/resource-manager',
      pendingPackages: [],
      handledPackages: [mockPackageData],
      status: 'succeeded' as SDKAutomationState,
      failureType: undefined,
      messages: [],
      messageCaptureTransport: {} as any,
      scriptEnvs: {},
      tmpFolder: '/tmp',
      vsoLogs: new Map(),
    } as unknown as WorkflowContext;

    // Mock external dependencies
    vi.mocked(extractPathFromSpecConfig).mockReturnValue('test-spec-config');
    vi.mocked(mapToObject).mockReturnValue({});
    vi.mocked(toolError).mockImplementation((msg) => `ERROR: ${msg}`);
    vi.mocked(toolWarning).mockImplementation((msg) => `WARNING: ${msg}`);
    vi.mocked(renderHandlebarTemplate).mockReturnValue('mocked comment body');
    // commentDetailView will be the mocked compiled function from the module
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('generateReport', () => {
    it('should generate report successfully with valid context', () => {
      vi.mocked(fs.existsSync).mockReturnValue(false);

      generateReport(mockWorkflowContext);

      expect(mockLogger.log).toHaveBeenCalledWith('section', 'Generate report');
      expect(mockLogger.log).toHaveBeenCalledWith('endsection', 'Generate report');
      expect(setSdkAutoStatus).toHaveBeenCalledWith(mockWorkflowContext, 'succeeded');
      expect(deleteTmpJsonFile).toHaveBeenCalledWith(mockWorkflowContext, 'execution-report.json');
      expect(writeTmpJsonFile).toHaveBeenCalledWith(
        mockWorkflowContext,
        'execution-report.json',
        expect.objectContaining({
          packages: expect.arrayContaining([
            expect.objectContaining({
              serviceName: 'test-service',
              packageName: 'test-package',
              result: 'succeeded',
              language: 'JavaScript',
            }),
          ]),
          executionResult: 'succeeded',
          fullLogPath: '/tmp/full.log',
          filteredLogPath: '/tmp/filtered.log',
          vsoLogPath: '/tmp/vso.log',
        })
      );
    });

    it('should handle breaking changes correctly', () => {
      const packageWithBreakingChange = {
        ...mockPackageData,
        hasBreakingChange: true,
        isBetaMgmtSdk: false,
        isDataPlane: false,
        presentSuppressionLines: [],
        absentSuppressionLines: [],
      };
      
      mockWorkflowContext.handledPackages = [packageWithBreakingChange];

      generateReport(mockWorkflowContext);

      expect(writeTmpJsonFile).toHaveBeenCalledWith(
        mockWorkflowContext,
        'execution-report.json',
        expect.objectContaining({
          packages: expect.arrayContaining([
            expect.objectContaining({
              shouldLabelBreakingChange: true,
              areBreakingChangeSuppressed: false,
            }),
          ]),
        })
      );
    });

    it('should handle suppressed breaking changes correctly', () => {
      const packageWithSuppressedBreakingChange = {
        ...mockPackageData,
        hasBreakingChange: true,
        isBetaMgmtSdk: false,
        isDataPlane: false,
        presentSuppressionLines: ['suppression1', 'suppression2'],
        absentSuppressionLines: [],
      };
      
      mockWorkflowContext.handledPackages = [packageWithSuppressedBreakingChange];

      generateReport(mockWorkflowContext);

      expect(writeTmpJsonFile).toHaveBeenCalledWith(
        mockWorkflowContext,
        'execution-report.json',
        expect.objectContaining({
          packages: expect.arrayContaining([
            expect.objectContaining({
              shouldLabelBreakingChange: false,
              areBreakingChangeSuppressed: true,
            }),
          ]),
        })
      );
    });

    it('should write markdown file when pullNumber is provided', () => {
      vi.mocked(fs.existsSync).mockReturnValue(false);

      generateReport(mockWorkflowContext);

      expect(fs.writeFileSync).toHaveBeenCalledWith(
        '/tmp/working/out/logs/test-spec-config-package-report.md',
        expect.stringContaining('## Package Name\ntest-package\n')
      );
      expect(vsoAddAttachment).toHaveBeenCalledWith(
        'Generation Summary for specification-test-resource-manager',
        '/tmp/working/out/logs/test-spec-config-package-report.md'
      );
    });

    it('should remove existing markdown file before writing new one', () => {
      vi.mocked(fs.existsSync).mockReturnValue(true);

      generateReport(mockWorkflowContext);

      expect(fs.rmSync).toHaveBeenCalledWith('/tmp/working/out/logs/test-spec-config-package-report.md');
      expect(fs.writeFileSync).toHaveBeenCalled();
    });

    it('should handle markdown write errors', () => {
      const writeError = new Error('Write failed');
      vi.mocked(fs.writeFileSync).mockImplementation(() => {
        throw writeError;
      });

      generateReport(mockWorkflowContext);

      expect(toolError).toHaveBeenCalledWith('Fails to write markdown file. Details: Error: Write failed');
      expect(mockLogger.error).toHaveBeenCalledWith('ERROR: Fails to write markdown file. Details: Error: Write failed');
      expect(vsoLogError).toHaveBeenCalled();
    });

    it('should skip markdown generation when no pullNumber', () => {
      mockWorkflowContext.config.pullNumber = undefined;

      generateReport(mockWorkflowContext);

      expect(fs.writeFileSync).not.toHaveBeenCalled();
      expect(vsoAddAttachment).not.toHaveBeenCalled();
    });

    it('should handle failed status', () => {
      mockWorkflowContext.status = 'failed';

      generateReport(mockWorkflowContext);

      expect(mockLogger.error).toHaveBeenCalledWith(
        'The generation process failed for specification-test-resource-manager. Refer to the full log for details.'
      );
      expect(vsoLogError).toHaveBeenCalled();
    });

    it('should handle notEnabled status', () => {
      mockWorkflowContext.status = 'notEnabled';

      generateReport(mockWorkflowContext);

      expect(mockLogger.warn).toHaveBeenCalledWith(
        'WARNING: SDK configuration is not enabled for specification-test-resource-manager. Refer to the full log for details.'
      );
      expect(vsoLogWarning).toHaveBeenCalled();
    });

    it('should handle local run environment (no vsoLogPath)', () => {
      mockWorkflowContext.config.runEnv = 'local';

      generateReport(mockWorkflowContext);

      expect(writeTmpJsonFile).toHaveBeenCalledWith(
        mockWorkflowContext,
        'execution-report.json',
        expect.not.objectContaining({
          vsoLogPath: expect.anything(),
        })
      );
    });
  });

  describe('saveVsoLog', () => {
    it('should save VSO log successfully', () => {
      const mockVsoLogs = new Map([
        ['task1', { errors: ['error1'], warnings: ['warning1'] }],
        ['task2', { errors: ['error2'] }],
      ]);
      mockWorkflowContext.vsoLogs = mockVsoLogs;
      vi.mocked(mapToObject).mockReturnValue({ task1: { errors: ['error1'], warnings: ['warning1'] } });

      saveVsoLog(mockWorkflowContext);

      expect(mockLogger.log).toHaveBeenCalledWith('section', 'Save log to /tmp/vso.log');
      expect(mockLogger.log).toHaveBeenCalledWith('endsection', 'Save log to /tmp/vso.log');
      expect(mapToObject).toHaveBeenCalledWith(mockVsoLogs);
      expect(fs.writeFileSync).toHaveBeenCalledWith(
        '/tmp/vso.log',
        JSON.stringify({ task1: { errors: ['error1'], warnings: ['warning1'] } }, null, 2)
      );
    });

    it('should handle write errors', () => {
      const writeError = new Error('Write failed');
      vi.mocked(fs.writeFileSync).mockImplementation(() => {
        throw writeError;
      });

      saveVsoLog(mockWorkflowContext);

      expect(toolError).toHaveBeenCalledWith('Fails to write log to /tmp/vso.log. Details: Error: Write failed');
      expect(mockLogger.error).toHaveBeenCalledWith('ERROR: Fails to write log to /tmp/vso.log. Details: Error: Write failed');
    });
  });

  describe('saveFilteredLog', () => {
    it('should save filtered log successfully', () => {
      saveFilteredLog(mockWorkflowContext);

      expect(mockLogger.log).toHaveBeenCalledWith('section', 'Save filtered log');
      expect(mockLogger.log).toHaveBeenCalledWith('endsection', 'Save filtered log status');
      expect(renderHandlebarTemplate).toHaveBeenCalledWith(
        commentDetailView,
        mockWorkflowContext,
        expect.objectContaining({
          hasBreakingChange: false,
          showLiteInstallInstruction: false,
        })
      );
      expect(fs.writeFileSync).toHaveBeenCalledWith(
        '/tmp/filtered.log',
        expect.stringContaining('"type":"Markdown"')
      );
      expect(fs.writeFileSync).toHaveBeenCalledWith(
        '/tmp/filtered.log',
        expect.stringContaining('"mode":"replace"')
      );
      expect(fs.writeFileSync).toHaveBeenCalledWith(
        '/tmp/filtered.log',
        expect.stringContaining('"level":"Info"')
      );
      expect(fs.writeFileSync).toHaveBeenCalledWith(
        '/tmp/filtered.log',
        expect.stringContaining('"message":"mocked comment body"')
      );
    });

    it('should handle pending packages', () => {
      const pendingPackage = { ...mockPackageData, name: 'pending-package' };
      mockWorkflowContext.pendingPackages = [pendingPackage];

      saveFilteredLog(mockWorkflowContext);

      expect(setSdkAutoStatus).toHaveBeenCalledWith(mockWorkflowContext, 'failed');
      expect(setFailureType).toHaveBeenCalledWith(mockWorkflowContext, FailureType.SpecGenSdkFailed);
      expect(toolError).toHaveBeenCalledWith('The following packages are still pending in code generation.');
      expect(mockLogger.error).toHaveBeenCalledWith('ERROR: The following packages are still pending in code generation.');
      expect(mockLogger.error).toHaveBeenCalledWith('\tpending-package');
      expect(mockWorkflowContext.handledPackages).toContain(pendingPackage);
    });

    it('should detect breaking changes across packages', () => {
      const packageWithBreakingChange = {
        ...mockPackageData,
        hasBreakingChange: true,
        liteInstallationInstruction: 'lite install command',
      };
      mockWorkflowContext.handledPackages = [packageWithBreakingChange];

      saveFilteredLog(mockWorkflowContext);

      expect(renderHandlebarTemplate).toHaveBeenCalledWith(
        commentDetailView,
        mockWorkflowContext,
        expect.objectContaining({
          hasBreakingChange: true,
          showLiteInstallInstruction: true,
        })
      );
    });

    it('should map different status levels correctly', () => {
      const testCases = [
        { status: 'pending' as SDKAutomationState, expectedLevel: 'Error' },
        { status: 'inProgress' as SDKAutomationState, expectedLevel: 'Error' },
        { status: 'failed' as SDKAutomationState, expectedLevel: 'Error' },
        { status: 'warning' as SDKAutomationState, expectedLevel: 'Warning' },
        { status: 'succeeded' as SDKAutomationState, expectedLevel: 'Info' },
        { status: 'notEnabled' as SDKAutomationState, expectedLevel: 'Warning' },
      ];

      testCases.forEach(({ status, expectedLevel }) => {
        vi.clearAllMocks();
        mockWorkflowContext.status = status;

        saveFilteredLog(mockWorkflowContext);

        expect(fs.writeFileSync).toHaveBeenCalledWith(
          '/tmp/filtered.log',
          expect.stringContaining(`"level":"${expectedLevel}"`)
        );
        expect(fs.writeFileSync).toHaveBeenCalledWith(
          '/tmp/filtered.log',
          expect.stringContaining('"type":"Markdown"')
        );
        expect(fs.writeFileSync).toHaveBeenCalledWith(
          '/tmp/filtered.log',
          expect.stringContaining('"message":"mocked comment body"')
        );
      });
    });

    it('should handle write errors', () => {
      const writeError = new Error('Write failed');
      vi.mocked(fs.writeFileSync).mockImplementation(() => {
        throw writeError;
      });

      saveFilteredLog(mockWorkflowContext);

      expect(toolError).toHaveBeenCalledWith('Fails to write log to /tmp/filtered.log. Details: Error: Write failed');
      expect(mockLogger.error).toHaveBeenCalledWith('ERROR: Fails to write log to /tmp/filtered.log. Details: Error: Write failed');
      expect(vsoLogError).toHaveBeenCalled();
    });
  });

  describe('generateHtmlFromFilteredLog', () => {
    beforeEach(() => {
      const mockMessageRecord = {
        type: 'Markdown',
        mode: 'replace',
        level: 'Info',
        message: '> [!NOTE]\n> This is a note block\n\n<ul><li>List item 1</li><li>List item 2</li></ul>',
        time: new Date(),
      };
      vi.mocked(fs.readFileSync).mockReturnValue(Buffer.from(JSON.stringify(mockMessageRecord)));
      vi.mocked(marked).mockReturnValue('<p>processed markdown</p>');
    });

    it('should generate HTML from filtered log successfully', () => {
      generateHtmlFromFilteredLog(mockWorkflowContext);

      expect(mockLogger.log).toHaveBeenCalledWith('section', 'Generate HTML from filtered log');
      expect(mockLogger.log).toHaveBeenCalledWith('endsection', 'Generate HTML from filtered log');
      expect(fs.readFileSync).toHaveBeenCalledWith('/tmp/filtered.log');
      expect(fs.writeFileSync).toHaveBeenCalledWith(
        '/tmp/report.html',
        expect.stringContaining('<!DOCTYPE html>'),
        'utf-8'
      );
      expect(fs.writeFileSync).toHaveBeenCalledWith(
        '/tmp/report.html',
        expect.stringContaining('spec-gen-sdk-js result'),
        'utf-8'
      );
    });

    it('should parse note blocks correctly', () => {
      generateHtmlFromFilteredLog(mockWorkflowContext);

      const htmlContent = vi.mocked(fs.writeFileSync).mock.calls[0][1] as string;
      expect(htmlContent).toMatchSnapshot();
    });

    it('should handle markdown without note blocks', () => {
      const mockMessageRecord = {
        type: 'Markdown',
        mode: 'replace',
        level: 'Info',
        message: '# Simple Markdown\n\nThis is a simple paragraph.',
        time: new Date(),
      };
      vi.mocked(fs.readFileSync).mockReturnValue(Buffer.from(JSON.stringify(mockMessageRecord)));

      generateHtmlFromFilteredLog(mockWorkflowContext);

      expect(marked).toHaveBeenCalledWith('# Simple Markdown\n\nThis is a simple paragraph.');
    });

    it('should handle read errors', () => {
      const readError = new Error('Read failed');
      vi.mocked(fs.readFileSync).mockImplementation(() => {
        throw readError;
      });

      generateHtmlFromFilteredLog(mockWorkflowContext);

      expect(toolError).toHaveBeenCalledWith("Fails to generate html log '/tmp/report.html'. Details: Error: Read failed");
      expect(mockLogger.error).toHaveBeenCalledWith("ERROR: Fails to generate html log '/tmp/report.html'. Details: Error: Read failed");
      expect(vsoLogError).toHaveBeenCalled();
    });

    it('should handle JSON parse errors', () => {
      vi.mocked(fs.readFileSync).mockReturnValue(Buffer.from('invalid json'));

      generateHtmlFromFilteredLog(mockWorkflowContext);

      expect(toolError).toHaveBeenCalledWith(expect.stringContaining("Fails to generate html log '/tmp/report.html'. Details:"));
      expect(mockLogger.error).toHaveBeenCalled();
      expect(vsoLogError).toHaveBeenCalled();
    });

    it('should handle write errors', () => {
      const writeError = new Error('Write failed');
      vi.mocked(fs.writeFileSync).mockImplementation(() => {
        throw writeError;
      });

      generateHtmlFromFilteredLog(mockWorkflowContext);

      expect(toolError).toHaveBeenCalledWith("Fails to generate html log '/tmp/report.html'. Details: Error: Write failed");
      expect(mockLogger.error).toHaveBeenCalledWith("ERROR: Fails to generate html log '/tmp/report.html'. Details: Error: Write failed");
      expect(vsoLogError).toHaveBeenCalled();
    });

    it('should generate correct page title for different SDK names', () => {
      mockWorkflowContext.config.sdkName = 'azure-sdk-for-python';

      generateHtmlFromFilteredLog(mockWorkflowContext);

      const htmlContent = vi.mocked(fs.writeFileSync).mock.calls[0][1] as string;
      expect(htmlContent).toContain('<title>spec-gen-sdk-python result</title>');
    });

  });
});
