import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { BlobServiceClient } from '@azure/storage-blob';
import { DefaultAzureCredential } from '@azure/identity';
import { ChannelConfigManager } from '../src/config/channel.js';

// Mock the Azure modules
vi.mock('@azure/storage-blob');
vi.mock('@azure/identity');

// Mock config module
vi.mock('../src/config/config.js', () => ({
  default: {
    azureStorageUrl: 'https://teststorage.table.core.windows.net',
  },
}));

// Mock logger
vi.mock('../src/logging/logger.js', () => ({
  logger: {
    info: vi.fn(),
    error: vi.fn(),
    debug: vi.fn(),
    warn: vi.fn(),
  },
}));

// Mock timers/promises
vi.mock('timers/promises', () => ({
  setTimeout: vi.fn(() => Promise.resolve()),
}));

// Mock configuration data as YAML string
const mockConfigYaml = `default:
  tenant: default-tenant
  endpoint: https://default.endpoint.com
channels:
  - id: channel1
    name: Test Channel 1
    tenant: tenant1
    endpoint: https://channel1.endpoint.com
  - id: channel2
    name: Test Channel 2
    tenant: tenant2
    endpoint: https://channel2.endpoint.com
`;

describe('ChannelConfigManager', () => {
  let manager: ChannelConfigManager;
  let mockBlobServiceClient: any;
  let mockContainerClient: any;
  let mockBlobClient: any;

  beforeEach(() => {
    manager = new ChannelConfigManager();

    // Create mock blob client chain
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

    // Mock BlobServiceClient constructor
    vi.mocked(BlobServiceClient).mockImplementation(() => mockBlobServiceClient);

    // Mock DefaultAzureCredential
    vi.mocked(DefaultAzureCredential).mockImplementation(() => ({} as any));

    // Reset all mocks
    vi.clearAllMocks();
  });

  afterEach(() => {
    manager.stopWatching();
  });

  describe('Blob Storage Loading and Watching', () => {
    it('should initialize blob client and load configuration', async () => {
      // Mock blob download response
      const mockReadableStream = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from(mockConfigYaml, 'utf8');
        },
      };

      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: mockReadableStream,
      });

      mockBlobClient.getProperties.mockResolvedValue({
        lastModified: new Date('2023-01-01T10:00:00Z'),
      });

      await manager.initialize();

      expect(BlobServiceClient).toHaveBeenCalledWith('https://teststorage.blob.core.windows.net', expect.any(Object));
      expect(mockContainerClient.getBlobClient).toHaveBeenCalledWith('channel.yaml');
      expect(mockBlobClient.download).toHaveBeenCalled();
    });

    it('should handle blob download errors gracefully', async () => {
      mockBlobClient.download.mockRejectedValue(new Error('Blob not found'));
      mockBlobClient.getProperties.mockRejectedValue(new Error('Blob not found'));

      // Initialize should complete, but when trying to get config it should fail
      await manager.initialize();

      // Getting config should throw since no config was loaded
      await expect(manager.getConfig()).rejects.toThrow('Channel configuration is not loaded');
    });

    it('should reload configuration when blob changes', async () => {
      // Initial setup
      const mockReadableStream1 = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('default:\n  tenant: old\n  endpoint: old\nchannels: []', 'utf8');
        },
      };

      const mockReadableStream2 = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('default:\n  tenant: new\n  endpoint: new\nchannels: []', 'utf8');
        },
      };

      mockBlobClient.download
        .mockResolvedValueOnce({ readableStreamBody: mockReadableStream1 })
        .mockResolvedValueOnce({ readableStreamBody: mockReadableStream2 });

      mockBlobClient.getProperties
        // init
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T10:00:00Z') })
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T10:00:00Z') })
        // getConfig
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T10:00:00Z') })
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T10:00:00Z') })
        // getConfig
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T11:00:00Z') })
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T11:00:00Z') });

      await manager.initialize();

      // Verify initial config
      let config = await manager.getConfig();
      expect(config.default.tenant).toBe('old');

      // Simulate time passing and blob change
      config = await manager.getConfig();
      expect(config.default.tenant).toBe('new');
    });

    it('should not reload if blob has not changed', async () => {
      const mockReadableStream = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('default:\n  tenant: test\n  endpoint: test\nchannels: []', 'utf8');
        },
      };

      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: mockReadableStream,
      });

      mockBlobClient.getProperties.mockResolvedValue({
        lastModified: new Date('2023-01-01T10:00:00Z'),
      });

      await manager.initialize();

      // Clear the download mock calls from initialization
      mockBlobClient.download.mockClear();

      // Call getConfig again - should not trigger reload since blob hasn't changed
      await manager.getConfig();

      // download should not be called again
      expect(mockBlobClient.download).not.toHaveBeenCalled();
    });

    it('should handle blob properties check errors gracefully', async () => {
      // Initial setup
      const mockReadableStream = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('default:\n  tenant: test\n  endpoint: test\nchannels: []', 'utf8');
        },
      };

      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: mockReadableStream,
      });

      mockBlobClient.getProperties
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T10:00:00Z') })
        .mockRejectedValueOnce(new Error('Network error'));

      await manager.initialize();

      // Should not throw error when checking for updates fails
      await expect(manager.getConfig()).resolves.toBeDefined();
    });
  });

  describe('Configuration Access', () => {
    beforeEach(async () => {
      // Setup manager with mock data using the YAML string
      const mockReadableStream = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from(mockConfigYaml, 'utf8');
        },
      };

      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: mockReadableStream,
      });

      mockBlobClient.getProperties.mockResolvedValue({
        lastModified: new Date('2023-01-01T10:00:00Z'),
      });

      await manager.initialize();
    });

    it('should return channel config for existing channel', async () => {
      const channelConfig = await manager.getChannelConfig('channel1');

      expect(channelConfig).toEqual({
        id: 'channel1',
        name: 'Test Channel 1',
        tenant: 'tenant1',
        endpoint: 'https://channel1.endpoint.com',
      });
    });

    it('should return default config for non-existing channel', async () => {
      const channelConfig = await manager.getChannelConfig('non-existing');

      expect(channelConfig).toEqual({
        id: 'non-existing',
        name: 'default',
        tenant: 'default-tenant',
        endpoint: 'https://default.endpoint.com',
      });
    });

    it('should return RAG tenant for channel', async () => {
      const tenant = await manager.getRagTenant('channel1');
      expect(tenant).toBe('tenant1');
    });

    it('should return RAG endpoint for channel', async () => {
      const endpoint = await manager.getRagEndpoint('channel1');
      expect(endpoint).toBe('https://channel1.endpoint.com');
    });
  });

  describe('Concurrent Loading', () => {
    it('should handle concurrent getConfig calls', async () => {
      const mockReadableStream1 = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('default:\n  tenant: test\n  endpoint: test\nchannels: []', 'utf8');
        },
      };

      const mockReadableStream2 = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('default:\n  tenant: concurrent\n  endpoint: concurrent\nchannels: []', 'utf8');
        },
      };

      mockBlobClient.download
        .mockResolvedValueOnce({ readableStreamBody: mockReadableStream1 })
        .mockResolvedValueOnce({ readableStreamBody: mockReadableStream2 });

      mockBlobClient.getProperties
        // init
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T10:00:00Z') })
        .mockResolvedValueOnce({ lastModified: new Date('2023-01-01T10:00:00Z') })
        // getConfig
        .mockResolvedValue({ lastModified: new Date('2023-01-01T11:00:00Z') });

      await manager.initialize();

      // Clear previous download calls from initialization
      mockBlobClient.download.mockClear();

      // Setup download for the reload that will be triggered
      mockBlobClient.download.mockResolvedValue({ readableStreamBody: mockReadableStream2 });

      // Make multiple concurrent calls - they should all wait for the same reload
      const promises = [manager.getConfig(), manager.getConfig(), manager.getConfig()];

      const results = await Promise.all(promises);

      // Should only load once due to concurrent loading protection, but all calls should get the same result
      expect(mockBlobClient.download).toHaveBeenCalledTimes(1);
      results.forEach((config) => {
        expect(config.default.tenant).toBe('concurrent');
      });
    });
  });

  describe('Error Handling', () => {
    beforeEach(async () => {
      // Setup basic working manager
      const mockReadableStream = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('default:\n  tenant: test\n  endpoint: test\nchannels: []', 'utf8');
        },
      };

      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: mockReadableStream,
      });

      mockBlobClient.getProperties.mockResolvedValue({
        lastModified: new Date('2023-01-01T10:00:00Z'),
      });

      await manager.initialize();
    });

    it('should handle getRagTenant errors gracefully', async () => {
      // Mock getChannelConfig to throw an error
      vi.spyOn(manager, 'getChannelConfig').mockRejectedValue(new Error('Config error'));

      const result = await manager.getRagTenant('test-channel');

      // Should return fallback value when error occurs
      expect(result).toBe('azure_sdk_qa_bot');
    });

    it('should handle getRagEndpoint errors gracefully', async () => {
      // Mock getChannelConfig to throw an error
      vi.spyOn(manager, 'getChannelConfig').mockRejectedValue(new Error('Config error'));

      const result = await manager.getRagEndpoint('test-channel');

      // Should return fallback value when error occurs
      expect(result).toBe('https://azuresdkbot-dqh7g6btekbfa3hh.eastasia-01.azurewebsites.net');
    });

    it('should validate configuration structure', async () => {
      // Create a new manager that hasn't been initialized yet
      const newManager = new ChannelConfigManager();

      // Mock invalid config - missing default section
      const mockReadableStream = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('channels: []', 'utf8');
        },
      };

      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: mockReadableStream,
      });

      mockBlobClient.getProperties.mockResolvedValue({
        lastModified: new Date('2023-01-01T10:00:00Z'),
      });

      await expect(newManager.initialize()).rejects.toThrow('Failed to load channel configuration');
    });

    it('should handle missing storage URL configuration', async () => {
      // Create a new manager
      const newManager = new ChannelConfigManager();

      // Import the config module and spy on it
      const configModule = await import('../src/config/config.js');
      const configSpy = vi.spyOn(configModule, 'default', 'get').mockReturnValue({
        azureStorageUrl: undefined,
      } as any);

      try {
        await expect(newManager.initialize()).rejects.toThrow('AZURE_STORAGE_URL environment variable is not set');
      } finally {
        // Restore the spy
        configSpy.mockRestore();
      }
    });
  });

  describe('Watch Loop', () => {
    it('should stop watching when stopWatching is called', async () => {
      const mockReadableStream = {
        [Symbol.asyncIterator]: async function* () {
          yield Buffer.from('default:\n  tenant: test\n  endpoint: test\nchannels: []', 'utf8');
        },
      };

      mockBlobClient.download.mockResolvedValue({
        readableStreamBody: mockReadableStream,
      });

      mockBlobClient.getProperties.mockResolvedValue({
        lastModified: new Date('2023-01-01T10:00:00Z'),
      });

      await manager.initialize();

      // Verify watching started
      expect(manager['isWatching']).toBe(true);

      manager.stopWatching();

      // Verify watching stopped
      expect(manager['isWatching']).toBe(false);
    });
  });
});
