import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ChannelConfigManager } from '../src/config/channel.js';
import config from '../src/config/config.js';

// Mock config module
vi.mock('../src/config/config.js', () => ({
  default: {
    azureStorageUrl: 'https://teststorage.table.core.windows.net',
    azureBlobStorageUrl: 'https://teststorage.blob.core.windows.net',
    isLocal: false,
    channelConfigBlobName: 'channel.yaml',
    localRagTenant: undefined,
    localBackendEndpoint: undefined,
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
    config.isLocal = false;
    config.localRagTenant = undefined;
    config.localBackendEndpoint = undefined;
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
      await expect(manager.initialize()).rejects.toThrow('Failed to initialize channel configuration: config could not be loaded from blob storage.');
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

  describe('Bot settings', () => {
    const defaults = { show_confidence_label: false, allow_notify_experts: false, experts: [] };
    const configuredYaml = `${mockConfigYaml}
  - id: configured
    name: Configured channel
    bot_settings:
      show_confidence_label: true
      allow_notify_experts: true
      experts:
        - id: expert-id
          name: Expert Name
      backend_only_setting: ignored
`;
    const enabled = {
      show_confidence_label: true,
      allow_notify_experts: true,
      experts: [{ id: 'expert-id', name: 'Expert Name' }],
    };

    async function load(yaml: string = configuredYaml) {
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(yaml);
      mockBlobClientManager.getBlobLastModifiedTime.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));
      await manager.initialize();
      manager.stopWatching();
    }

    it('defaults missing, null and omitted settings independently', async () => {
      await load(`${mockConfigYaml}
  - id: null-settings
    name: Null
    bot_settings: null
  - id: partial-settings
    name: Partial
    bot_settings:
      show_confidence_label: true
`);
      expect(manager.getBotSettings('channel1')).toEqual(defaults);
      expect(manager.getBotSettings('unknown')).toEqual(defaults);
      expect(manager.getBotSettings('null-settings')).toEqual(defaults);
      expect(manager.getBotSettings('partial-settings')).toEqual({ ...defaults, show_confidence_label: true });
      manager.getBotSettings('channel1').experts.push({ id: 'changed', name: 'Changed' });
      expect(manager.getBotSettings('channel1')).toEqual(defaults);
    });

    it('exposes configured settings while preserving routing fallback and ignores backend-only fields', async () => {
      await load();
      expect(manager.getChannelConfig('configured')).toEqual({
        id: 'configured',
        name: 'Configured channel',
        tenant: 'default-tenant',
        endpoint: 'https://default.endpoint.com',
        bot_settings: { ...enabled, backend_only_setting: 'ignored' },
      });
      expect(manager.getBotSettings('configured')).toEqual(enabled);
      const settings = manager.getBotSettings('configured');
      settings.experts[0].name = 'Changed';
      expect(manager.getBotSettings('configured')).toEqual(enabled);
      expect(manager.getRagTenant('unknown')).toBe('default-tenant');
      expect(manager.getBotSettings('unknown')).toEqual(defaults);
    });

    it('keeps local routing without config and uses matching channel settings when loaded', async () => {
      config.isLocal = true;
      config.localRagTenant = 'local-tenant';
      config.localBackendEndpoint = 'https://local.endpoint.com';
      expect(manager.getRagTenant('configured')).toBe('local-tenant');
      expect(manager.getRagEndpoint('configured')).toBe('https://local.endpoint.com');
      expect(manager.getBotSettings('configured')).toEqual(defaults);
      await load();
      expect(manager.getChannelConfig('configured')).toMatchObject({
        name: 'local',
        tenant: 'local-tenant',
        endpoint: 'https://local.endpoint.com',
        bot_settings: enabled,
      });
      expect(manager.getBotSettings('configured')).toEqual(enabled);
      expect(manager.getBotSettings('unknown')).toEqual(defaults);
    });

    it.each(['empty download', 'download error', 'invalid YAML', 'metadata check'])(
      'keeps cached settings and routing after %s failure, and reloads when the blob changes',
      async (failure) => {
        await load();
        expect(manager.getBotSettings('configured')).toEqual(enabled);
        if (failure === 'metadata check') {
          mockBlobClientManager.getBlobLastModifiedTime.mockRejectedValueOnce(new Error('Unavailable'));
          await manager['checkAndReload']();
        } else {
          if (failure === 'empty download') {
            mockBlobClientManager.downloadBlobContent.mockResolvedValueOnce(undefined);
          } else if (failure === 'download error') {
            mockBlobClientManager.downloadBlobContent.mockRejectedValueOnce(new Error('Unavailable'));
          } else {
            mockBlobClientManager.downloadBlobContent.mockResolvedValueOnce('channels: [');
          }
          await manager['loadConfig']();
        }
        expect(manager.getRagTenant('channel1')).toBe('tenant1');
        expect(manager.getRagEndpoint('channel1')).toBe('https://channel1.endpoint.com');
        expect(manager.getRagTenant('unknown')).toBe('default-tenant');
        expect(manager.getBotSettings('configured')).toEqual(enabled);
        expect(manager.getChannelConfig('configured').bot_settings).toMatchObject(enabled);

        mockBlobClientManager.downloadBlobContent.mockClear();
        await manager['checkAndReload']();
        expect(mockBlobClientManager.downloadBlobContent).not.toHaveBeenCalled();

        mockBlobClientManager.getBlobLastModifiedTime.mockResolvedValue(new Date('2023-01-02T10:00:00Z'));
        mockBlobClientManager.downloadBlobContent.mockResolvedValue(mockConfigYaml);
        await manager['checkAndReload']();
        expect(mockBlobClientManager.downloadBlobContent).toHaveBeenCalledOnce();
        expect(manager.getBotSettings('configured')).toEqual(defaults);
      },
    );

    it('keeps freshly loaded settings when metadata retrieval fails after download', async () => {
      await load();
      mockBlobClientManager.getBlobLastModifiedTime.mockRejectedValueOnce(new Error('Unavailable'));
      await manager['loadConfig']();
      expect(manager.getBotSettings('configured')).toEqual(enabled);
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
