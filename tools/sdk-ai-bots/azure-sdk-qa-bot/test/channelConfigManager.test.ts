import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import fs from 'fs/promises';
import fsSync from 'fs';
import { ChannelConfigManager } from '../src/config/channel.js';

// Mock the file system modules
vi.mock('fs/promises');
vi.mock('fs');

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
  let mockStats: any;

  beforeEach(() => {
    manager = new ChannelConfigManager();

    // Mock file stats
    mockStats = {
      mtime: new Date('2023-01-01T10:00:00Z'),
    };

    // Reset all mocks
    vi.clearAllMocks();
  });

  afterEach(() => {
    manager.stopWatching();
  });

  describe('File Loading and Watching', () => {
    it('should load configuration on initialize', async () => {
      // Mock file reading using mockConfig
      vi.mocked(fs.readFile).mockResolvedValue(mockConfigYaml);
      vi.mocked(fs.stat).mockResolvedValue(mockStats as any);
      vi.mocked(fsSync.watchFile).mockReturnValue({} as any);

      await manager.initialize();

      expect(fs.readFile).toHaveBeenCalledWith(expect.stringContaining('channel.yaml'), 'utf8');
      expect(fsSync.watchFile).toHaveBeenCalled();
    });

    it('should start watching file changes', async () => {
      // Mock file reading and stats
      vi.mocked(fs.readFile).mockResolvedValue('default:\n  tenant: test\n  endpoint: test\nchannels: []');
      vi.mocked(fs.stat).mockResolvedValue(mockStats as any);

      const watchFileSpy = vi.mocked(fsSync.watchFile).mockReturnValue({} as any);

      await manager.initialize();

      expect(watchFileSpy).toHaveBeenCalledWith(
        expect.stringContaining('channel.yaml'),
        { interval: 5000 },
        expect.any(Function)
      );
    });

    it('should reload configuration when file changes', async () => {
      // Initial load
      vi.mocked(fs.readFile).mockResolvedValueOnce('default:\n  tenant: old\n  endpoint: old\nchannels: []');
      vi.mocked(fs.stat).mockResolvedValueOnce({ mtime: new Date('2023-01-01T10:00:00Z') } as any);

      let watchCallback: ((curr: any, prev: any) => void) | undefined;
      vi.mocked(fsSync.watchFile).mockImplementation((...args: any[]) => {
        const callback = args[args.length - 1];
        if (typeof callback === 'function') {
          watchCallback = callback;
        }
        return {} as any;
      });

      await manager.initialize();

      // Simulate file change
      vi.mocked(fs.readFile).mockResolvedValueOnce('default:\n  tenant: new\n  endpoint: new\nchannels: []');
      vi.mocked(fs.stat).mockResolvedValueOnce({ mtime: new Date('2023-01-01T11:00:00Z') } as any);

      // Trigger the watch callback
      if (watchCallback) {
        watchCallback({ mtime: new Date('2023-01-01T11:00:00Z') }, { mtime: new Date('2023-01-01T10:00:00Z') });
      }

      // Verify config was reloaded
      const config = await manager.getConfig();
      expect(config.default.tenant).toBe('new');
    });

    it('should not reload if file has not changed', async () => {
      // Mock initial load
      vi.mocked(fs.readFile).mockResolvedValue('default:\n  tenant: test\n  endpoint: test\nchannels: []');
      vi.mocked(fs.stat).mockResolvedValue({ mtime: new Date('2023-01-01T10:00:00Z') } as any);
      vi.mocked(fsSync.watchFile).mockReturnValue({} as any);

      await manager.initialize();

      // Clear the readFile mock calls from initialization
      vi.mocked(fs.readFile).mockClear();

      // Call getConfig again - should not trigger reload since file hasn't changed
      await manager.getConfig();

      // readFile should not be called again
      expect(fs.readFile).not.toHaveBeenCalled();
    });

    it('should handle file reading errors gracefully', async () => {
      vi.mocked(fs.readFile).mockRejectedValue(new Error('File not found'));
      vi.mocked(fs.stat).mockResolvedValue(mockStats as any);

      await expect(manager.initialize()).rejects.toThrow('Failed to load channel configuration');
    });

    it('should stop watching when stopWatching is called', async () => {
      // Mock initial setup
      vi.mocked(fs.readFile).mockResolvedValue('default:\n  tenant: test\n  endpoint: test\nchannels: []');
      vi.mocked(fs.stat).mockResolvedValue(mockStats as any);
      vi.mocked(fsSync.watchFile).mockReturnValue({} as any);
      const unwatchFileSpy = vi.mocked(fsSync.unwatchFile).mockImplementation(() => {});

      await manager.initialize();
      manager.stopWatching();

      expect(unwatchFileSpy).toHaveBeenCalledWith(expect.stringContaining('channel.yaml'));
    });
  });

  describe('Configuration Access', () => {
    beforeEach(async () => {
      // Setup manager with mock data using the YAML string
      vi.mocked(fs.readFile).mockResolvedValue(mockConfigYaml);
      vi.mocked(fs.stat).mockResolvedValue(mockStats as any);
      vi.mocked(fsSync.watchFile).mockReturnValue({} as any);

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
      vi.mocked(fs.readFile).mockResolvedValue('default:\n  tenant: test\n  endpoint: test\nchannels: []');
      vi.mocked(fs.stat).mockResolvedValue(mockStats as any);
      vi.mocked(fsSync.watchFile).mockReturnValue({} as any);

      await manager.initialize();

      // Clear previous calls
      vi.mocked(fs.readFile).mockClear();
      vi.mocked(fs.stat).mockClear();

      // Simulate file change to trigger reload
      vi.mocked(fs.stat).mockResolvedValue({ mtime: new Date('2023-01-01T11:00:00Z') } as any);
      vi.mocked(fs.readFile).mockResolvedValue('default:\n  tenant: concurrent\n  endpoint: concurrent\nchannels: []');

      // Make multiple concurrent calls
      const promises = [manager.getConfig(), manager.getConfig(), manager.getConfig()];

      const results = await Promise.all(promises);

      // Should only load once, but all calls should get the same result
      expect(fs.readFile).toHaveBeenCalledTimes(1);
      results.forEach((config) => {
        expect(config.default.tenant).toBe('concurrent');
      });
    });
  });

  describe('Error Handling', () => {
    beforeEach(async () => {
      // Setup basic working manager
      vi.mocked(fs.readFile).mockResolvedValue('default:\n  tenant: test\n  endpoint: test\nchannels: []');
      vi.mocked(fs.stat).mockResolvedValue(mockStats as any);
      vi.mocked(fsSync.watchFile).mockReturnValue({} as any);
      await manager.initialize();
    });

    it('should handle getRagTenant errors gracefully', async () => {
      // Mock getChannelConfig to throw an error
      vi.spyOn(manager, 'getChannelConfig').mockRejectedValue(new Error('Config error'));

      const result = await manager.getRagTenant('test-channel');

      // Should return undefined when error occurs
      expect(result).toBeUndefined();
    });

    it('should handle getRagEndpoint errors gracefully', async () => {
      // Mock getChannelConfig to throw an error
      vi.spyOn(manager, 'getChannelConfig').mockRejectedValue(new Error('Config error'));

      const result = await manager.getRagEndpoint('test-channel');

      // Should return undefined when error occurs
      expect(result).toBeUndefined();
    });

    it('should validate configuration structure', async () => {
      // Create a new manager that hasn't been initialized yet
      const newManager = new ChannelConfigManager();

      // Test with invalid config - missing default section
      vi.mocked(fs.readFile).mockResolvedValue('channels: []');
      vi.mocked(fs.stat).mockResolvedValue({ mtime: new Date('2023-01-01T11:00:00Z') } as any);

      await expect(newManager.initialize()).rejects.toThrow('Failed to load channel configuration');
    });
  });
});
