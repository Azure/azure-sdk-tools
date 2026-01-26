import { parse } from 'yaml';
import { setTimeout } from 'timers/promises';
import { logger } from '../logging/logger.js';
import { BlobClientManager } from './blobClient.js';
import config from './config.js';

export interface TenantItem {
  tenant: string;
  channel_name: string;
  channel_link: string;
}

export interface TenantConfig {
  tenants: TenantItem[];
}

class TenantConfigManager {
  private config: TenantConfig | null = null;
  private lastModified: Date | null = null;
  private isWatching: boolean = false;
  private loadPromise: Promise<void> | null = null;
  private blobClientManager: BlobClientManager;
  private readonly watchInterval: number = 5000; // 5 seconds for blob storage

  constructor() {
    this.blobClientManager = BlobClientManager.getInstance();
  }

  /**
   * Initialize the tenant configuration
   */
  public async initialize(): Promise<void> {
    await this.loadConfig();
    this.startWatching();
  }

  /**
   * Get the current tenant configuration
   */
  public async getConfig(): Promise<TenantConfig> {
    if (!this.config) {
      throw new Error('Tenant configuration is not loaded');
    }
    return this.config;
  }

  /**
   * Get tenant item by tenant ID
   */
  public async getTenant(tenantId: string): Promise<TenantItem | undefined> {
    const config = await this.getConfig();
    return config.tenants.find((t) => t.tenant === tenantId);
  }

  /**
   * Get channel name by tenant ID
   */
  public async getChannelName(tenantId: string): Promise<string | undefined> {
    const tenant = await this.getTenant(tenantId);
    return tenant?.channel_name;
  }

  /**
   * Get channel link by tenant ID
   */
  public async getChannelLink(tenantId: string): Promise<string | undefined> {
    const tenant = await this.getTenant(tenantId);
    return tenant?.channel_link;
  }

  /**
   * Check if blob has been modified and reload if necessary
   */
  private async checkAndReload(): Promise<void> {
    if (this.loadPromise) {
      await this.loadPromise;
      return;
    }

    try {
      const currentModified = await this.blobClientManager.getBlobLastModified(config.tenantConfigBlobName);

      if (!this.lastModified || (currentModified && currentModified > this.lastModified)) {
        logger.info('Tenant config blob changed, reloading...', {
          blob: config.tenantConfigBlobName,
          lastModified: this.lastModified?.toISOString(),
          currentModified: currentModified?.toISOString(),
        });
        await this.loadConfig();
      }
    } catch (error) {
      logger.error('Failed to check tenant config blob modification time', {
        error: error.message,
        blob: config.tenantConfigBlobName,
      });
    }
  }

  /**
   * Load configuration from Azure Blob Storage
   */
  private async loadConfig(): Promise<void> {
    if (this.loadPromise) {
      await this.loadPromise;
      return;
    }

    this.loadPromise = this.loadConfigCore();

    try {
      await this.loadPromise;
    } finally {
      this.loadPromise = null;
    }
  }

  private async loadConfigCore(): Promise<void> {
    try {
      const fileContent = await this.blobClientManager.downloadBlobContent(config.tenantConfigBlobName);

      if (!fileContent) {
        // If we already have a config, keep using it
        if (this.config) {
          logger.warn(`Failed to download tenant config blob, still use old config.`);
          return;
        }
        // First initialization - no config to fall back to
        throw new Error('Failed to download tenant config blob and no existing config available');
      }

      // Parse YAML content using yaml library with type assertion
      const parsedConfig = parse(fileContent, {
        schema: 'core',
        strict: true,
        uniqueKeys: true,
      }) as TenantConfig;

      this.config = parsedConfig;

      // Get blob properties for last modified time
      try {
        this.lastModified = (await this.blobClientManager.getBlobLastModified(config.tenantConfigBlobName)) || new Date();
      } catch (metadataError) {
        logger.warn('Failed to get blob last modified time, using current time', {
          blob: config.tenantConfigBlobName,
          error: metadataError.message,
        });
        this.lastModified = new Date();
      }

      logger.info('Tenant configuration loaded successfully from blob storage', {
        blob: config.tenantConfigBlobName,
        tenantCount: this.config.tenants.length,
      });

      this.ensureConfig();
    } catch (error) {
      logger.error('Failed to load tenant configuration from blob storage', {
        error: error.message,
        blob: config.tenantConfigBlobName,
      });
      throw new Error(`Failed to load tenant configuration: ${error.message}`);
    }
  }

  /**
   * Ensure the loaded configuration is valid
   */
  private ensureConfig(): void {
    if (!this.config) {
      throw new Error('Tenant configuration is null');
    }

    if (!Array.isArray(this.config.tenants)) {
      throw new Error('Tenants configuration must be an array');
    }

    for (const tenant of this.config.tenants) {
      if (!tenant.tenant || !tenant.channel_name || !tenant.channel_link) {
        throw new Error(`Tenant configuration is incomplete: ${JSON.stringify(tenant)}`);
      }
    }

    logger.debug('Tenant configuration validation passed');
  }

  /**
   * Promise-based delay function using async/await
   */
  private async delay(ms: number): Promise<void> {
    await setTimeout(ms);
  }

  /**
   * Start watching the configuration blob for changes
   */
  private startWatching(): void {
    if (this.isWatching) {
      return;
    }

    this.isWatching = true;

    // Start the async watching loop without blocking
    this.startWatchLoop();

    logger.info('Started watching tenant config blob', {
      blob: config.tenantConfigBlobName,
      interval: this.watchInterval,
    });
  }

  /**
   * Start the watch loop asynchronously
   */
  private async startWatchLoop(): Promise<void> {
    try {
      await this.watchLoop();
    } catch (error) {
      logger.error('Tenant watch loop failed', { error: error.message });
    }
  }

  /**
   * Async watching loop using promises
   */
  private async watchLoop(): Promise<void> {
    while (this.isWatching) {
      try {
        await this.checkAndReload();
        await this.delay(this.watchInterval);
      } catch (error) {
        logger.error('Failed to check for tenant config changes', { error: error.message });
        // Continue watching even if there's an error
        await this.delay(this.watchInterval);
      }
    }
  }

  /**
   * Stop watching the configuration blob
   */
  public stopWatching(): void {
    if (this.isWatching) {
      this.isWatching = false;
      logger.info('Stopped watching tenant config blob');
    }
  }
}

export { TenantConfigManager };
