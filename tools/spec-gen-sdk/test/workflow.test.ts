import { workflowValidateSdkConfig } from '../src/automation/workflow';
import { findSDKToGenerateFromTypeSpecProject } from '../src/utils/typespecUtils';
import { findSwaggerToSDKConfiguration } from '../src/utils/readme';
import * as fs from 'fs';
import * as path from 'path';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Mock validator
vi.mock('../src/types/validator', () => ({
  getTypeTransformer: () => (data) => data,
}));

// Mock dependencies
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

vi.mock('../src/utils/typespecUtils', () => ({
  findSDKToGenerateFromTypeSpecProject: vi.fn(),
}));

vi.mock('../src/utils/readme', () => ({
  findSwaggerToSDKConfiguration: vi.fn(),
}));

// Mock path module
vi.mock('path', () => ({
  ...vi.importActual('path'),
  join: vi.fn((...args) => args.join('/')),
}));

// Create a standalone test for workflowValidateSdkConfig
describe('workflowValidateSdkConfig', () => {
  // Setup mock context for testing
  let mockContext;

  beforeEach(() => {
    vi.clearAllMocks();

    // Default mock implementations
    vi.mocked(fs.readFileSync).mockReturnValue(Buffer.from('mock content'));
    vi.mocked(fs.existsSync).mockReturnValue(true);

    // Create a fresh mock context for each test
    mockContext = {
      logger: {
        log: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
      },
      config: {
        localSpecRepoPath: '/mock/spec/repo',
        tspConfigPath: undefined,
        readmePath: undefined,
        sdkName: 'azure-sdk-for-test',
        skipSdkGenFromOpenapi: undefined,
      },
      specRepoConfig: {},
      status: 'inProgress',
      specConfigPath: undefined,
      isSdkConfigDuplicated: undefined,
    };
  });

  it('should throw error when neither tspConfigPath nor readmePath are provided', async () => {
    // Arrange
    mockContext.config.tspConfigPath = undefined;
    mockContext.config.readmePath = undefined;

    // Act & Assert
    await expect(workflowValidateSdkConfig(mockContext)).rejects.toThrow(/.*'tspConfigPath' and 'readmePath' are not provided.*/);
  });

  it('should set status to notEnabled when tspconfig does not contain supported SDK', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = undefined;

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.status).toBe('notEnabled');
    expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('cannot find supported emitter in tspconfig.yaml'));
  });

  it('should set status to notEnabled when readme does not contain supported SDK', async () => {
    // Arrange
    mockContext.config.tspConfigPath = undefined;
    mockContext.config.readmePath = 'some/path/readme.md';

    vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({ repositories: [] });

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.status).toBe('notEnabled');
    expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining("'swagger-to-sdk' section cannot be found"));
  });

  it('should set specConfigPath to tspConfigPath when tspconfig contains supported SDK', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = undefined;

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.status).toBe('inProgress');
    expect(mockContext.specConfigPath).toBe('some/path/tspconfig.yaml');
    expect(mockContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('SDK to generate:azure-sdk-for-test'));
  });

  it('should set specConfigPath to readmePath when readme contains supported SDK', async () => {
    // Arrange
    mockContext.config.tspConfigPath = undefined;
    mockContext.config.readmePath = 'some/path/readme.md';

    vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
      repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
    });

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.status).toBe('inProgress');
    expect(mockContext.specConfigPath).toBe('some/path/readme.md');
    expect(mockContext.logger.info).toHaveBeenCalledWith(expect.stringContaining('SDK to generate:azure-sdk-for-test'));
  });

  it('should mark as duplicated when both tspconfig and readme contain supported SDK', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = 'some/path/readme.md';

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);
    vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
      repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
    });

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.status).toBe('inProgress');
    expect(mockContext.isSdkConfigDuplicated).toBe(true);
    expect(mockContext.specConfigPath).toBe('some/path/tspconfig.yaml');
    expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('SDK generation configuration is enabled for both'));
  });

  it('should set status to notEnabled when both configs exist but neither contains supported SDK', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = 'some/path/readme.md';

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);
    vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
      repositories: [],
    });

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.status).toBe('notEnabled');
    expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('No SDKs are enabled for generation'));
  });

  it('should respect skipSdkGenFromOpenapi flag when tspconfig does not support SDK', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = 'some/path/readme.md';
    mockContext.config.skipSdkGenFromOpenapi = 'true';

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);
    vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
      repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
    });

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.status).toBe('notEnabled');
    expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('cannot find supported emitter in tspconfig.yaml'));
  });

  it('should handle missing files gracefully by throwing error for tspconfig', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = undefined;

    // Simulate file read error
    vi.mocked(fs.readFileSync).mockImplementation(() => {
      throw new Error('File not found');
    });

    // Act & Assert
    await expect(workflowValidateSdkConfig(mockContext)).rejects.toThrow();
  });

  it('should handle missing files gracefully by throwing error for readme', async () => {
    // Arrange
    mockContext.config.tspConfigPath = undefined;
    mockContext.config.readmePath = 'some/path/readme.md';

    // Simulate file read error
    vi.mocked(fs.readFileSync).mockImplementation(() => {
      throw new Error('File not found');
    });

    // Act & Assert
    await expect(workflowValidateSdkConfig(mockContext)).rejects.toThrow();
  });

  it('should handle management paths differently for samples URL', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/.Management/tspconfig.yaml';
    mockContext.config.readmePath = undefined;

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('https://aka.ms/azsdk/tspconfig-sample-mpg'));
  });

  it('should handle data plane URLs for samples', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = undefined;

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue([]);

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.logger.warn).toHaveBeenCalledWith(expect.stringContaining('https://aka.ms/azsdk/tspconfig-sample-dpg'));
  });

  it('should only check the supported SDK once with findSDKToGenerateFromTypeSpecProject function', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = undefined;

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(findSDKToGenerateFromTypeSpecProject).toHaveBeenCalledTimes(1);
    expect(findSDKToGenerateFromTypeSpecProject).toHaveBeenCalledWith('mock content', {});
  });

  it('should only check the supported SDK once with findSwaggerToSDKConfiguration function', async () => {
    // Arrange
    mockContext.config.tspConfigPath = undefined;
    mockContext.config.readmePath = 'some/path/readme.md';

    vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
      repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
    });

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(findSwaggerToSDKConfiguration).toHaveBeenCalledTimes(1);
    expect(findSwaggerToSDKConfiguration).toHaveBeenCalledWith('mock content');
  });

  it('should prefer TypeSpec config when both configs are available and valid', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = 'some/path/readme.md';

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-test']);
    vi.mocked(findSwaggerToSDKConfiguration).mockReturnValue({
      repositories: [{ repo: 'azure-sdk-for-test', after_scripts: [] }],
    });

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.specConfigPath).toBe('some/path/tspconfig.yaml');
  });

  it('should handle when a different SDK is enabled in tspconfig than the one requested', async () => {
    // Arrange
    mockContext.config.tspConfigPath = 'some/path/tspconfig.yaml';
    mockContext.config.readmePath = undefined;
    mockContext.config.sdkName = 'azure-sdk-for-test';

    vi.mocked(findSDKToGenerateFromTypeSpecProject).mockReturnValue(['azure-sdk-for-other']);

    // Act
    await workflowValidateSdkConfig(mockContext);

    // Assert
    expect(mockContext.status).toBe('notEnabled');
  });
});
