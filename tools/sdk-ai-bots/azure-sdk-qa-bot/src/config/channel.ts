import { parse } from 'yaml';
import { setTimeout } from 'timers/promises';
import { logger } from '../logging/logger.js';
import { BlobClientManager } from './blobClient.js';
import config from './config.js';

export interface ChannelItem {
  name: string;
  id: string;
  tenant: string;
  endpoint: string;
}

export interface ChannelConfig {
  default: {
    tenant: string;
    endpoint: string;
  };
  channels: ChannelItem[];
}

class ChannelConfigManager {
  private config: ChannelConfig | null = null;
  private lastModified: Date | null = null;
  private isWatching: boolean = false;
  private loadPromise: Promise<void> | null = null;
  private readonly blobClientManager: BlobClientManager;
  private readonly watchInterval: number = 5000; // 5 seconds for blob storage

  constructor() {
    this.blobClientManager = BlobClientManager.getInstance();
  }

  public async initialize(): Promise<void> {
    await this.loadConfig();
    this.startWatching();
  }

  /**
   * Get the current channel configuration
   */
  public async getConfig(): Promise<ChannelConfig> {
    if (!this.config) {
      throw new Error('Channel configuration is not loaded');
    }
    return this.config;
  }

  /**
   * Get channel configuration by channel ID
   */
  public async getChannelConfig(channelId: string): Promise<ChannelItem> {
    // In local environment, return local settings directly
    if (config.isLocal) {
      return {
        name: 'local',
        id: channelId,
        tenant: config.localRagTenant ?? config.fallbackRagTenant,
        endpoint: config.localBackendEndpoint ?? config.fallbackRagEndpoint,
      };
    }

    const channelConfig = await this.getConfig();

    // Find matching channel
    const channel = channelConfig.channels.find((ch) => ch.id === channelId);
    if (channel) {
      return {
        name: channel.name,
        id: channel.id,
        tenant: channel.tenant,
        endpoint: channel.endpoint,
      };
    }

    // Return default configuration with generated name and id if no match found
    return {
      name: 'default',
      id: channelId, // Use the requested channelId
      tenant: channelConfig.default.tenant,
      endpoint: channelConfig.default.endpoint,
    };
  }

  /**
   * Get RAG tenant for a specific channel ID
   */
  public async getRagTenant(channelId: string): Promise<string> {
    try {
      const channelConfig = await this.getChannelConfig(channelId);
      return channelConfig.tenant;
    } catch (error) {
      logger.error(`Failed to get RAG tenant for channel, fallback to ${config.fallbackRagTenant}`, {
        channelId,
        error: error.message,
      });
      return config.fallbackRagTenant;
    }
  }

  /**
   * Get RAG endpoint for a specific channel ID
   */
  public async getRagEndpoint(channelId: string): Promise<string> {
    try {
      const channelConfig = await this.getChannelConfig(channelId);
      return channelConfig.endpoint;
    } catch (error) {
      logger.error(`Failed to get RAG endpoint for channel, fallback to ${config.fallbackRagEndpoint}`, {
        channelId,
        error: error.message,
      });
      return config.fallbackRagEndpoint;
    }
  }

  /**
   * Check if blob has been modified and reload if necessary
   */
  private async checkAndReload(): Promise<void> {
    if (this.loadPromise) {
      // If already loading, wait for the current load to complete
      await this.loadPromise;
      return;
    }

    try {
      const currentModified = await this.blobClientManager.getBlobLastModified(config.channelConfigBlobName);

      if (!this.lastModified || (currentModified && currentModified > this.lastModified)) {
        logger.info('Channel config blob changed, reloading...', {
          blob: config.channelConfigBlobName,
          lastModified: this.lastModified?.toISOString(),
          currentModified: currentModified?.toISOString(),
        });
        await this.loadConfig();
      }
    } catch (error) {
      logger.error('Failed to check channel config blob modification time', {
        error: error.message,
        blob: config.channelConfigBlobName,
      });
    }
  }

  /**
   * Load configuration from Azure Blob Storage
   */
  private async loadConfig(): Promise<void> {
    if (this.loadPromise) {
      // If already loading, wait for the current load to complete
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
      // Download blob content using shared blob client
      const fileContent = await this.blobClientManager.downloadBlobContent(config.channelConfigBlobName);

      if (!fileContent) {
        // If we already have a config, keep using it
        if (this.config) {
          logger.warn('Failed to download channel config blob, still use old config.');
          return;
        }
        // First initialization - no config to fall back to
        throw new Error('Failed to download channel config blob and no existing config available');
      }

      // Parse YAML content using yaml library with type assertion
      const parsedConfig = parse(fileContent, {
        schema: 'core',
        strict: true, // Strict parsing
        uniqueKeys: true, // Ensure unique keys
      }) as ChannelConfig;

      this.config = parsedConfig;

      // Get blob properties for last modified time
      try {
        const lastModified = await this.blobClientManager.getBlobLastModified(config.channelConfigBlobName);
        this.lastModified = lastModified || new Date();
      } catch (metadataError) {
        logger.warn('Failed to get blob last modified time, using current time', {
          blob: config.channelConfigBlobName,
          error: metadataError.message,
        });
        this.lastModified = new Date();
      }

      logger.info('Channel configuration loaded successfully from blob storage', {
        blob: config.channelConfigBlobName,
        channelCount: this.config.channels.length,
        defaultTenant: this.config.default.tenant,
      });

      // Validate configuration
      this.ensureConfig();
    } catch (error) {
      logger.error('Failed to load channel configuration from blob storage', {
        error: error.message,
        blob: config.channelConfigBlobName,
      });
      throw new Error(`Failed to load channel configuration: ${error.message}`);
    }
  }

  /**
   * Ensure the loaded configuration is valid
   */
  private ensureConfig(): void {
    if (!this.config) {
      throw new Error('Configuration is null');
    }

    if (!this.config.default || !this.config.default.tenant || !this.config.default.endpoint) {
      throw new Error('Default configuration is missing or incomplete');
    }

    if (!Array.isArray(this.config.channels)) {
      throw new Error('Channels configuration must be an array');
    }

    for (const channel of this.config.channels) {
      if (!channel.id || !channel.name || !channel.tenant || !channel.endpoint) {
        throw new Error(`Channel configuration is incomplete: ${JSON.stringify(channel)}`);
      }
    }

    logger.debug('Channel configuration validation passed');
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

    logger.info('Started watching channel config blob', {
      blob: config.channelConfigBlobName,
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
      logger.error('Watch loop failed', { error: error.message });
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
        logger.error('Failed to check for config changes', { error: error.message });
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
      logger.info('Stopped watching channel config blob');
    }
  }
}

export { ChannelConfigManager };
