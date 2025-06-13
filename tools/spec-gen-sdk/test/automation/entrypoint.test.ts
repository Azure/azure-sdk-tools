import { describe, expect, test, vi, beforeEach } from 'vitest';
import { sdkAutoMain } from '../../src/automation/entrypoint';
import * as workflow from '../../src/automation/workflow';
import * as logging from '../../src/automation/logging';
import * as reportStatus from '../../src/automation/reportStatus';
import { WorkflowContext, FailureType } from '../../src/types/Workflow';
import { SdkAutoOptions, SdkAutoContext } from '../../src/types/Entrypoint';
import { Repository } from '../../src/utils/repo';
import * as winston from 'winston';
import Transport from 'winston-transport';

// Mock all imported modules
vi.mock('../../src/automation/workflow');
vi.mock('../../src/automation/logging');
vi.mock('../../src/automation/reportStatus');

describe('sdkAutoMain', () => {
  const mockLogger = {
    info: vi.fn(),
    error: vi.fn(),
    debug: vi.fn()
  } as unknown as winston.Logger;

  const mockSpecRepo: Repository = {
    owner: 'Azure',
    name: 'azure-rest-api-specs'
  };

  const mockOptions: SdkAutoOptions = {
    specRepo: mockSpecRepo,
    sdkName: 'test-sdk',
    branchPrefix: 'test',
    localSpecRepoPath: '/test/spec/path',
    localSdkRepoPath: '/test/sdk/path',
    runMode: 'test',
    sdkReleaseType: 'preview',
    specCommitSha: 'abc123',
    specRepoHttpsUrl: 'https://github.com/test/spec',
    workingFolder: '/test/working',
    runEnv: 'test',
    version: '1.0.0'
  };

  const mockContext: SdkAutoContext = {
    config: mockOptions,
    logger: mockLogger,
    fullLogFileName: 'full.log',
    filteredLogFileName: 'filtered.log',
    htmlLogFileName: 'report.html',
    vsoLogFileName: 'vso.log',
    specRepoConfig: {} as any,
    sdkRepoConfig: {} as any,
    swaggerToSdkConfig: {} as any,
    isPrivateSpecRepo: false
  };

  const mockWorkflowContext: WorkflowContext = {
    ...mockContext,
    pendingPackages: [],
    handledPackages: [],
    status: 'pending',
    messages: [],
    messageCaptureTransport: {} as Transport,
    scriptEnvs: {},
    tmpFolder: '/tmp/test',
    vsoLogs: new Map()
  };

  beforeEach(() => {
    vi.clearAllMocks();
    
    // Setup default mock implementations
    vi.mocked(workflow.getSdkAutoContext).mockResolvedValue(mockContext);
    vi.mocked(workflow.workflowInit).mockResolvedValue(mockWorkflowContext);
    vi.mocked(workflow.workflowMain).mockResolvedValue();
    vi.mocked(logging.loggerWaitToFinish).mockResolvedValue();
  });

  test('should successfully complete the workflow', async () => {
    const status = await sdkAutoMain(mockOptions);

    expect(workflow.getSdkAutoContext).toHaveBeenCalledWith(mockOptions);
    expect(workflow.workflowInit).toHaveBeenCalledWith(mockContext);
    expect(workflow.workflowMain).toHaveBeenCalledWith(mockWorkflowContext);
    expect(reportStatus.saveFilteredLog).toHaveBeenCalledWith(mockWorkflowContext);
    expect(reportStatus.generateHtmlFromFilteredLog).toHaveBeenCalledWith(mockWorkflowContext);
    expect(reportStatus.generateReport).toHaveBeenCalledWith(mockWorkflowContext);
    expect(reportStatus.saveVsoLog).toHaveBeenCalledWith(mockWorkflowContext);
    expect(logging.loggerWaitToFinish).toHaveBeenCalledWith(mockContext.logger);
    expect(status).toBe('pending');
  });

  test('should handle workflow initialization error', async () => {
    const error = new Error('Init failed');
    vi.mocked(workflow.workflowInit).mockRejectedValue(error);

    const status = await sdkAutoMain(mockOptions);

    expect(status).toBeUndefined();
    expect(mockContext.logger.error).toHaveBeenCalled();
    expect(logging.loggerWaitToFinish).toHaveBeenCalledWith(mockContext.logger);
  });

  test('should handle workflow execution error', async () => {
    const error = new Error('Execution failed');
    vi.mocked(workflow.workflowMain).mockRejectedValue(error);

    const status = await sdkAutoMain(mockOptions);

    expect(status).toBe('failed');
    expect(mockWorkflowContext.status).toBe('failed');
    expect(mockWorkflowContext.failureType).toBe(FailureType.SpecGenSdkFailed);
    expect(mockWorkflowContext.messages).toContain(error.message);
    expect(logging.vsoLogError).toHaveBeenCalled();
    expect(logging.loggerWaitToFinish).toHaveBeenCalledWith(mockContext.logger);
  });

  test('should preserve notEnabled status on error', async () => {
    const error = new Error('Execution failed');
    mockWorkflowContext.status = 'notEnabled';
    vi.mocked(workflow.workflowMain).mockRejectedValue(error);

    const status = await sdkAutoMain(mockOptions);

    expect(status).toBe('notEnabled');
    expect(mockWorkflowContext.status).toBe('notEnabled');
    expect(mockWorkflowContext.failureType).toBe(FailureType.SpecGenSdkFailed);
    expect(logging.vsoLogError).toHaveBeenCalled();
    expect(logging.loggerWaitToFinish).toHaveBeenCalledWith(mockContext.logger);
  });
});