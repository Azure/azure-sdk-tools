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
    if (!this.config) {
      throw new Error('Failed to initialize tenant configuration: config could not be loaded from blob storage');
    }
    this.startWatching();
  }

  /**
   * Get the current tenant configuration.
   */
  public getConfig(): TenantConfig {
    return this.config!;
  }

  /**
   * Get tenant item by tenant ID.
   */
  public getTenant(tenantId: string): TenantItem | undefined {
    const config = this.getConfig();
    return config.tenants.find((t) => t.tenant === tenantId);
  }

  /**
   * Get channel name by tenant ID
   */
  public getChannelName(tenantId: string): string | undefined {
    const tenant = this.getTenant(tenantId);
    return tenant?.channel_name;
  }

  /**
   * Get channel link by tenant ID
   */
  public getChannelLink(tenantId: string): string | undefined {
    const tenant = this.getTenant(tenantId);
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
      const currentModified = await this.blobClientManager.getBlobLastModifiedTime(config.tenantConfigBlobName);

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
        } else {
          logger.error('Failed to download tenant config blob and no existing config available.');
        }
        return;
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
        const lastModified = await this.blobClientManager.getBlobLastModifiedTime(config.tenantConfigBlobName);
        if (lastModified) {
          this.lastModified = lastModified;
        }
      } catch (metadataError) {
        logger.warn('Failed to get blob last modified time, will reload on next check', {
          blob: config.tenantConfigBlobName,
          error: metadataError.message,
        });
      }

      logger.info('Tenant configuration loaded successfully from blob storage', {
        blob: config.tenantConfigBlobName,
        tenantCount: this.config.tenants.length,
      });

    } catch (error) {
      logger.error('Failed to load tenant configuration from blob storage', {
        error: error.message,
        blob: config.tenantConfigBlobName,
      });
    }
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
      } catch (error) {
        logger.error('Failed to check for tenant config changes', { error: error.message });
      }
      await setTimeout(this.watchInterval);
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
