import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ChannelConfigManager } from '../src/config/channel.js';

// Mock config module
vi.mock('../src/config/config.js', () => ({
  default: {
    azureStorageUrl: 'https://teststorage.table.core.windows.net',
    azureBlobStorageUrl: 'https://teststorage.blob.core.windows.net',
    isLocal: false,
  },
}));

// Mock BlobClientManager
const mockBlobClientManager = {
  downloadBlobContent: vi.fn(),
  getBlobLastModified: vi.fn(),
};

vi.mock('../src/config/blobClient.js', () => ({
  BlobClientManager: {
    getInstance: vi.fn(() => mockBlobClientManager),
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

  beforeEach(() => {
    manager = new ChannelConfigManager();

    // Reset all mocks
    vi.clearAllMocks();
  });

  afterEach(() => {
    manager.stopWatching();
  });

  describe('Blob Storage Loading and Watching', () => {
    it('should initialize blob client and load configuration', async () => {
      // Mock BlobClientManager methods
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(mockConfigYaml);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      expect(mockBlobClientManager.downloadBlobContent).toHaveBeenCalledWith('channel.yaml');
    });

    it('should handle blob download errors gracefully when download returns undefined', async () => {
      // Simulate download with undefined content (all retries exhausted)
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(undefined);

      // Initialize should throw since no config to fall back to
      await expect(manager.initialize()).rejects.toThrow('Failed to load channel configuration');
    });

    it('should handle blob download errors gracefully when download throws', async () => {
      // Simulate download failure with an error
      mockBlobClientManager.downloadBlobContent.mockRejectedValue(new Error('Blob not found'));

      // Initialize should throw since config cannot be loaded
      await expect(manager.initialize()).rejects.toThrow('Failed to load channel configuration');
    });

    it('should keep old config when download fails during reload', async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';

      // First load succeeds
      mockBlobClientManager.downloadBlobContent.mockResolvedValueOnce(testConfig);
      mockBlobClientManager.getBlobLastModified
        .mockResolvedValueOnce(new Date('2023-01-01T10:00:00Z'))
        // Second call indicates blob changed
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'));

      await manager.initialize();

      // Verify initial config loaded
      let config = await manager.getConfig();
      expect(config.default.tenant).toBe('test');

      // Now simulate download failure during reload
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(undefined);

      // Trigger reload by accessing config (blob has changed)
      config = await manager.getConfig();

      // Should still have the old config
      expect(config.default.tenant).toBe('test');
    });

    it('should reload configuration when blob changes', async () => {
      const oldConfig = 'default:\n  tenant: old\n  endpoint: old\nchannels: []';
      const newConfig = 'default:\n  tenant: new\n  endpoint: new\nchannels: []';

      mockBlobClientManager.downloadBlobContent
        .mockResolvedValueOnce(oldConfig)
        .mockResolvedValueOnce(newConfig);

      mockBlobClientManager.getBlobLastModified
        .mockResolvedValueOnce(new Date('2023-01-01T10:00:00Z'))
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'))
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'));
      await manager.initialize();

      // Verify initial config
      let config = await manager.getConfig();
      expect(config.default.tenant).toBe('old');

      // Simulate time passing and blob change
      config = await manager.getConfig();
      expect(config.default.tenant).toBe('new');
    });

    it('should not reload if blob has not changed', async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      // Clear the download mock calls from initialization
      mockBlobClientManager.downloadBlobContent.mockClear();

      // Call getConfig again - should not trigger reload since blob hasn't changed
      await manager.getConfig();

      // download should not be called again
      expect(mockBlobClientManager.downloadBlobContent).not.toHaveBeenCalled();
    });

    it('should handle blob properties check errors gracefully', async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModified
        .mockResolvedValueOnce(new Date('2023-01-01T10:00:00Z'))
        .mockRejectedValueOnce(new Error('Network error'));

      await manager.initialize();

      // Should not throw error when checking for updates fails
      await expect(manager.getConfig()).resolves.toBeDefined();
    });
  });

  describe('Configuration Access', () => {
    beforeEach(async () => {
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(mockConfigYaml);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

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
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';

      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      // Make multiple concurrent calls - they should all return the same config
      const promises = [manager.getConfig(), manager.getConfig(), manager.getConfig()];

      const results = await Promise.all(promises);

      // All calls should get the same result
      results.forEach((config) => {
        expect(config.default.tenant).toBe('test');
      });
    });
  });

  describe('Error Handling', () => {
    beforeEach(async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

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
      mockBlobClientManager.downloadBlobContent.mockResolvedValue('channels: []');
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await expect(newManager.initialize()).rejects.toThrow('Failed to load channel configuration');
    });
  });

  describe('Watch Loop', () => {
    it('should stop watching when stopWatching is called', async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      // Verify watching started
      expect(manager['isWatching']).toBe(true);

      manager.stopWatching();

      // Verify watching stopped
      expect(manager['isWatching']).toBe(false);
    });
  });
});
