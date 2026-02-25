import { describe, it, expect, vi, beforeEach, afterEach, Mock } from 'vitest';
import { Readable } from 'stream';

// Mock the dependencies before importing the module
vi.mock('@azure/storage-blob', () => ({
  BlobServiceClient: vi.fn(),
}));

vi.mock('../src/common/shared.js', () => ({
  getAzureCredential: vi.fn(),
}));

vi.mock('../src/config/config.js', () => ({
  default: {
    MicrosoftAppId: 'test-bot-id',
    azureBlobStorageUrl: 'https://test.blob.core.windows.net',
    blobContainerName: 'bot-configs',
  },
}));

vi.mock('../src/logging/logger.js', () => ({
  logger: {
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    debug: vi.fn(),
  },
}));

import { BlobClientManager } from '../src/config/blobClient.js';
import { BlobServiceClient } from '@azure/storage-blob';
import { getAzureCredential } from '../src/common/shared.js';
import config from '../src/config/config.js';

describe('BlobClientManager', () => {
  let mockBlobServiceClient: {
    getContainerClient: Mock;
  };
  let mockContainerClient: {
    getBlobClient: Mock;
  };
  let mockBlobClient: {
    download: Mock;
    getProperties: Mock;
  };

  beforeEach(() => {
    vi.clearAllMocks();

    // Reset singleton for each test
    (BlobClientManager as any).instance = null;

    // Setup mock chain
    mockBlobClient = {
      download: vi.fn(),
      getProperties: vi.fn(),
    };

    mockContainerClient = {
      getBlobClient: vi.fn().mockReturnValue(mockBlobClient),
    };

    mockBlobServiceClient = {
      getContainerClient: vi.fn().mockReturnValue(mockContainerClient),
    };

    (BlobServiceClient as unknown as Mock).mockImplementation(() => mockBlobServiceClient);
    (getAzureCredential as Mock).mockResolvedValue({ token: 'test-token' });
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  describe('Singleton Pattern', () => {
    it('should return the same instance on multiple calls', () => {
      const instance1 = BlobClientManager.getInstance();
      const instance2 = BlobClientManager.getInstance();

      expect(instance1).toBe(instance2);
    });

    it('should create a new instance after reset', () => {
      const instance1 = BlobClientManager.getInstance();
      (BlobClientManager as any).instance = null;
      const instance2 = BlobClientManager.getInstance();

      expect(instance1).not.toBe(instance2);
    });
  });

  describe('Initialization', () => {
    it('should initialize blob service client successfully', async () => {
      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      expect(BlobServiceClient).toHaveBeenCalledWith(
        'https://test.blob.core.windows.net',
        expect.anything()
      );
      expect(mockBlobServiceClient.getContainerClient).toHaveBeenCalledWith('bot-configs');
    });

    it('should not reinitialize if already initialized', async () => {
      const manager = BlobClientManager.getInstance();

      await manager.initialize();
      await manager.initialize();

      // BlobServiceClient constructor should only be called once
      expect(BlobServiceClient).toHaveBeenCalledTimes(1);
    });

    it('should throw error when credential initialization fails', async () => {
      (getAzureCredential as Mock).mockRejectedValue(new Error('Credential error'));

      const manager = BlobClientManager.getInstance();

      await expect(manager.initialize()).rejects.toThrow('Credential error');
    });
  });

  describe('getContainerClient', () => {
    it('should throw error if not initialized', () => {
      const manager = BlobClientManager.getInstance();

      expect(() => manager.getContainerClient()).toThrow(
        'Blob client not initialized. Call initialize() first.'
      );
    });

    it('should return container client after initialization', async () => {
      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      const containerClient = manager.getContainerClient();

      expect(containerClient).toBe(mockContainerClient);
    });
  });

  describe('downloadBlobContent', () => {
    const createReadableStream = (content: string): NodeJS.ReadableStream => {
      return Readable.from([Buffer.from(content, 'utf8')]);
    };

    it('should download blob content successfully', async () => {
      const testContent = 'test blob content';
      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: createReadableStream(testContent),
      });

      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      const result = await manager.downloadBlobContent('test.yaml');

      expect(result).toBe(testContent);
      expect(mockContainerClient.getBlobClient).toHaveBeenCalledWith('test.yaml');
    });

    it('should retry on download failure and succeed', async () => {
      const testContent = 'retry success content';

      // Fail first 2 attempts, succeed on third
      mockBlobClient.download
        .mockRejectedValueOnce(new Error('Network error 1'))
        .mockRejectedValueOnce(new Error('Network error 2'))
        .mockResolvedValueOnce({
          readableStreamBody: createReadableStream(testContent),
        });

      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      const result = await manager.downloadBlobContent('test.yaml');

      expect(result).toBe(testContent);
      expect(mockBlobClient.download).toHaveBeenCalledTimes(3);
    });

    it('should return undefined after all retries exhausted', async () => {
      mockBlobClient.download.mockRejectedValue(new Error('Persistent error'));

      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      const result = await manager.downloadBlobContent('test.yaml');

      expect(result).toBeUndefined();
      // Default max retry is 6
      expect(mockBlobClient.download).toHaveBeenCalledTimes(6);
    });

    it('should handle YAML content correctly', async () => {
      const yamlContent = 'default:\n  tenant: test\n  endpoint: https://example.com\nchannels: []';
      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: createReadableStream(yamlContent),
      });

      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      const result = await manager.downloadBlobContent('channel.yaml');

      expect(result).toBe(yamlContent);
    });
  });

  describe('getBlobLastModified', () => {
    it('should return last modified date', async () => {
      const lastModified = new Date('2023-06-15T10:30:00Z');
      mockBlobClient.getProperties.mockResolvedValue({
        lastModified,
      });

      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      const result = await manager.getBlobLastModifiedTime('test.yaml');

      expect(result).toEqual(lastModified);
      expect(mockContainerClient.getBlobClient).toHaveBeenCalledWith('test.yaml');
    });

    it('should return undefined when properties have no lastModified', async () => {
      mockBlobClient.getProperties.mockResolvedValue({});

      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      const result = await manager.getBlobLastModifiedTime('test.yaml');

      expect(result).toBeUndefined();
    });

    it('should propagate errors from getProperties', async () => {
      mockBlobClient.getProperties.mockRejectedValue(new Error('Blob not found'));

      const manager = BlobClientManager.getInstance();
      await manager.initialize();

      await expect(manager.getBlobLastModifiedTime('nonexistent.yaml')).rejects.toThrow('Blob not found');
    });
  });
});
