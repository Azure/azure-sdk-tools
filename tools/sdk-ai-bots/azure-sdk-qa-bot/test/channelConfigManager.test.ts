import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ChannelConfigManager } from '../src/config/channel.js';

// Mock config module
vi.mock('../src/config/config.js', () => ({
  default: {
    azureStorageUrl: 'https://teststorage.table.core.windows.net',
    azureBlobStorageUrl: 'https://teststorage.blob.core.windows.net',
    isLocal: false,
    channelConfigBlobName: 'channel.yaml',
  },
}));

// Mock BlobClientManager
const mockBlobClientManager = {
  downloadBlobContent: vi.fn(),
  getBlobLastModifiedTime: vi.fn(),
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
    vi.clearAllMocks();
    // Use fake timers to control the watch loop that periodically checks for config changes.
    // Without fake timers, the watch loop's setTimeout would run continuously during tests,
    // causing tests to hang or behave unpredictably. With fake timers, we can use
    // vi.advanceTimersByTimeAsync() to manually trigger reload checks.
    vi.useFakeTimers();
    manager = new ChannelConfigManager();
  });

  afterEach(() => {
    // Stop the watch loop to prevent it from running after the test completes
    manager.stopWatching();
    // Restore real timers for subsequent tests
    vi.useRealTimers();
    vi.resetAllMocks();
  });

  describe('Blob Storage Loading and Watching', () => {
    it('should initialize blob client and load configuration', async () => {
      // Mock BlobClientManager methods
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(mockConfigYaml);
      mockBlobClientManager.getBlobLastModifiedTime.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      expect(mockBlobClientManager.downloadBlobContent).toHaveBeenCalledWith('channel.yaml');
    });

    it('should handle blob download errors gracefully when download returns undefined', async () => {
      // Simulate download with undefined content (all retries exhausted)
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(undefined);

      // Initialize should throw since no config to fall back to
      await expect(manager.initialize()).rejects.toThrow('Failed to initialize channel configuration');
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
      mockBlobClientManager.getBlobLastModifiedTime
        .mockResolvedValueOnce(new Date('2023-01-01T10:00:00Z'))
        // Second call indicates blob changed
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'));

      await manager.initialize();

      // Verify initial config loaded
      let config = manager.getConfig();
      expect(config.default.tenant).toBe('test');

      // Now simulate download failure during reload
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(undefined);

      // Trigger reload by accessing config (blob has changed)
      config = manager.getConfig();

      // Should still have the old config
      expect(config.default.tenant).toBe('test');
    });

    it('should reload configuration when blob changes', async () => {
      const oldConfig = 'default:\n  tenant: old\n  endpoint: old\nchannels: []';
      const newConfig = 'default:\n  tenant: new\n  endpoint: new\nchannels: []';

      mockBlobClientManager.downloadBlobContent
        .mockResolvedValueOnce(oldConfig)
        .mockResolvedValueOnce(newConfig);

      mockBlobClientManager.getBlobLastModifiedTime
        .mockResolvedValueOnce(new Date('2023-01-01T10:00:00Z'))
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'))
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'));
      await manager.initialize();

      // Verify initial config
      let config = manager.getConfig();
      expect(config.default.tenant).toBe('old');

      // Advance timer to trigger reload check
      await vi.advanceTimersByTimeAsync(5000);

      config = manager.getConfig();
      expect(config.default.tenant).toBe('new');
    });

    it('should not reload if blob has not changed', async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModifiedTime.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      // Clear the download mock calls from initialization
      mockBlobClientManager.downloadBlobContent.mockClear();

      // Call getConfig again - should not trigger reload since blob hasn't changed
      manager.getConfig();

      // download should not be called again
      expect(mockBlobClientManager.downloadBlobContent).not.toHaveBeenCalled();
    });

    it('should handle blob properties check errors gracefully', async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModifiedTime
        .mockResolvedValueOnce(new Date('2023-01-01T10:00:00Z'))
        .mockRejectedValueOnce(new Error('Network error'));

      await manager.initialize();

      // Should not throw error when checking for updates fails
      expect(manager.getConfig()).toBeDefined();
    });
  });

  describe('Configuration Access', () => {
    beforeEach(async () => {
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(mockConfigYaml);
      mockBlobClientManager.getBlobLastModifiedTime.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();
    });

    it('should return channel config for existing channel', () => {
      const channelConfig = manager.getChannelConfig('channel1');

      expect(channelConfig).toEqual({
        id: 'channel1',
        name: 'Test Channel 1',
        tenant: 'tenant1',
        endpoint: 'https://channel1.endpoint.com',
      });
    });

    it('should return default config for non-existing channel', () => {
      const channelConfig = manager.getChannelConfig('non-existing');

      expect(channelConfig).toEqual({
        id: 'non-existing',
        name: 'default',
        tenant: 'default-tenant',
        endpoint: 'https://default.endpoint.com',
      });
    });

    it('should return RAG tenant for channel', () => {
      const tenant = manager.getRagTenant('channel1');
      expect(tenant).toBe('tenant1');
    });

    it('should return RAG endpoint for channel', () => {
      const endpoint = manager.getRagEndpoint('channel1');
      expect(endpoint).toBe('https://channel1.endpoint.com');
    });
  });

  describe('Concurrent Loading', () => {
    it('should handle concurrent getConfig calls', async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';

      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModifiedTime.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      // Make multiple calls - they should all return the same config
      const results = [manager.getConfig(), manager.getConfig(), manager.getConfig()];

      // All calls should get the same result
      results.forEach((config) => {
        expect(config.default.tenant).toBe('test');
      });
    });
  });

  describe('Watch Loop', () => {
    it('should stop watching when stopWatching is called', async () => {
      const testConfig = 'default:\n  tenant: test\n  endpoint: test\nchannels: []';
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(testConfig);
      mockBlobClientManager.getBlobLastModifiedTime.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      // Verify watching started
      expect(manager['isWatching']).toBe(true);

      manager.stopWatching();

      // Verify watching stopped
      expect(manager['isWatching']).toBe(false);
    });
  });
});
