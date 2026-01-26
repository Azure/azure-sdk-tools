import { describe, it, expect, vi, beforeEach, afterEach, Mock } from 'vitest';

// Mock the dependencies before importing the module
vi.mock('../src/config/blobClient.js', () => ({
  BlobClientManager: {
    getInstance: vi.fn(),
  },
}));

vi.mock('../src/config/config.js', () => ({
  default: {
    tenantConfigBlobName: 'tenant.yaml',
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

import { TenantConfigManager } from '../src/config/tenant.js';
import { BlobClientManager } from '../src/config/blobClient.js';

describe('TenantConfigManager', () => {
  let manager: TenantConfigManager;
  let mockBlobClientManager: {
    downloadBlobContent: Mock;
    getBlobLastModified: Mock;
  };

  const validTenantYaml = `
tenants:
  - tenant: tenant-1
    channel_name: Test Channel 1
    channel_link: https://teams.microsoft.com/channel1
  - tenant: tenant-2
    channel_name: Test Channel 2
    channel_link: https://teams.microsoft.com/channel2
`;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();

    mockBlobClientManager = {
      downloadBlobContent: vi.fn(),
      getBlobLastModified: vi.fn(),
    };

    (BlobClientManager.getInstance as Mock).mockReturnValue(mockBlobClientManager);

    manager = new TenantConfigManager();
  });

  afterEach(() => {
    manager.stopWatching();
    vi.useRealTimers();
    vi.resetAllMocks();
  });

  describe('Initialization', () => {
    it('should initialize and load configuration successfully', async () => {
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(validTenantYaml);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      expect(mockBlobClientManager.downloadBlobContent).toHaveBeenCalledWith('tenant.yaml');
    });

    it('should throw error when download fails on first initialization', async () => {
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(undefined);

      await expect(manager.initialize()).rejects.toThrow('Failed to load tenant configuration');
    });

    it('should throw error when download throws', async () => {
      mockBlobClientManager.downloadBlobContent.mockRejectedValue(new Error('Network error'));

      await expect(manager.initialize()).rejects.toThrow('Failed to load tenant configuration');
    });
  });

  describe('Configuration Access', () => {
    beforeEach(async () => {
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(validTenantYaml);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));
      await manager.initialize();
    });

    it('should return full config', async () => {
      const config = await manager.getConfig();

      expect(config.tenants).toHaveLength(2);
      expect(config.tenants[0].tenant).toBe('tenant-1');
      expect(config.tenants[1].tenant).toBe('tenant-2');
    });

    it('should return tenant by ID', async () => {
      const tenant = await manager.getTenant('tenant-1');

      expect(tenant).toBeDefined();
      expect(tenant?.tenant).toBe('tenant-1');
      expect(tenant?.channel_name).toBe('Test Channel 1');
    });

    it('should return undefined for non-existing tenant', async () => {
      const tenant = await manager.getTenant('non-existing');

      expect(tenant).toBeUndefined();
    });

    it('should return channel name by tenant ID', async () => {
      const channelName = await manager.getChannelName('tenant-2');

      expect(channelName).toBe('Test Channel 2');
    });

    it('should return channel link by tenant ID', async () => {
      const channelLink = await manager.getChannelLink('tenant-1');

      expect(channelLink).toBe('https://teams.microsoft.com/channel1');
    });

    it('should return undefined channel name for non-existing tenant', async () => {
      const channelName = await manager.getChannelName('non-existing');

      expect(channelName).toBeUndefined();
    });
  });

  describe('Configuration Reload', () => {
    it('should reload configuration when blob changes', async () => {
      const oldConfig = `
tenants:
  - tenant: old-tenant
    channel_name: Old Channel
    channel_link: https://old.link
`;
      const newConfig = `
tenants:
  - tenant: new-tenant
    channel_name: New Channel
    channel_link: https://new.link
`;

      mockBlobClientManager.downloadBlobContent
        .mockResolvedValueOnce(oldConfig)
        .mockResolvedValueOnce(newConfig);

      mockBlobClientManager.getBlobLastModified
        .mockResolvedValueOnce(new Date('2023-01-01T10:00:00Z'))
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'))
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'));

      await manager.initialize();

      let config = await manager.getConfig();
      expect(config.tenants[0].tenant).toBe('old-tenant');

      // Advance timer to trigger reload check
      await vi.advanceTimersByTimeAsync(5000);

      config = await manager.getConfig();
      expect(config.tenants[0].tenant).toBe('new-tenant');
    });

    it('should keep old config when reload fails', async () => {
      mockBlobClientManager.downloadBlobContent.mockResolvedValueOnce(validTenantYaml);
      mockBlobClientManager.getBlobLastModified
        .mockResolvedValueOnce(new Date('2023-01-01T10:00:00Z'))
        .mockResolvedValueOnce(new Date('2023-01-01T11:00:00Z'));

      await manager.initialize();

      // Now simulate download failure during reload
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(undefined);

      // Advance timer to trigger reload check
      await vi.advanceTimersByTimeAsync(5000);

      const config = await manager.getConfig();
      expect(config.tenants).toHaveLength(2);
      expect(config.tenants[0].tenant).toBe('tenant-1');
    });
  });

  describe('Configuration Validation', () => {
    it('should throw error for invalid tenant config (missing fields)', async () => {
      const invalidYaml = `
tenants:
  - tenant: incomplete-tenant
`;

      mockBlobClientManager.downloadBlobContent.mockResolvedValue(invalidYaml);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date());

      await expect(manager.initialize()).rejects.toThrow('Failed to load tenant configuration');
    });

    it('should throw error when tenants is not an array', async () => {
      const invalidYaml = `
tenants:
  tenant: not-an-array
`;

      mockBlobClientManager.downloadBlobContent.mockResolvedValue(invalidYaml);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date());

      await expect(manager.initialize()).rejects.toThrow('Failed to load tenant configuration');
    });
  });

  describe('Watch Loop', () => {
    it('should stop watching when stopWatching is called', async () => {
      mockBlobClientManager.downloadBlobContent.mockResolvedValue(validTenantYaml);
      mockBlobClientManager.getBlobLastModified.mockResolvedValue(new Date('2023-01-01T10:00:00Z'));

      await manager.initialize();

      // Initial load calls
      const initialCalls = mockBlobClientManager.getBlobLastModified.mock.calls.length;

      manager.stopWatching();

      // Advance timer
      await vi.advanceTimersByTimeAsync(10000);

      // Should not have made more calls after stopping
      expect(mockBlobClientManager.getBlobLastModified.mock.calls.length).toBe(initialCalls);
    });
  });

  describe('Error Handling', () => {
    it('should throw error when getConfig called before initialization', async () => {
      await expect(manager.getConfig()).rejects.toThrow('Tenant configuration is not loaded');
    });

    it('should throw error when getTenant called before initialization', async () => {
      await expect(manager.getTenant('any')).rejects.toThrow('Tenant configuration is not loaded');
    });
  });
});
