import fs from 'fs/promises';
import fsSync from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { parse } from 'yaml';
import { logger } from '../logging/logger.js';

export interface ChannelConfig {
  default: {
    tenant: string;
    endpoint: string;
  };
  channels: {
    name: string;
    id: string;
    tenant: string;
    endpoint: string;
  }[];
}

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const CHANNEL_CONFIG_PATH = path.resolve(__dirname, '../../config/channel.yaml');

class ChannelConfigManager {
  private config: ChannelConfig | null = null;
  private lastModified: number = 0;
  private isWatching: boolean = false;
  private isLoading: boolean = false;
  private loadPromise: Promise<void> | null = null;
  private readonly watchInterval: number = 5000; // 5 seconds

  constructor() {
    // Constructor is now empty - initialization happens externally
  }

  public async initialize(): Promise<void> {
    await this.loadConfig();
    this.startWatching();
  }

  /**
   * Get the current channel configuration
   * Automatically reloads if file has changed
   */
  public async getConfig(): Promise<ChannelConfig> {
    await this.checkAndReload();
    if (!this.config) {
      throw new Error('Channel configuration is not loaded');
    }
    return this.config;
  }

  /**
   * Get channel configuration by channel ID
   */
  public async getChannelConfig(channelId: string): Promise<{ tenant: string; endpoint: string }> {
    const config = await this.getConfig();

    // Find matching channel
    const channel = config.channels.find((ch) => ch.id === channelId);
    if (channel) {
      return {
        tenant: channel.tenant || config.default.tenant,
        endpoint: channel.endpoint || config.default.endpoint,
      };
    }

    // Return default configuration if no match found
    return {
      tenant: config.default.tenant,
      endpoint: config.default.endpoint,
    };
  }

  /**
   * Get all channel IDs
   */
  public async getAllChannelIds(): Promise<string[]> {
    const config = await this.getConfig();
    return config.channels.map((ch) => ch.id);
  }

  /**
   * Get RAG tenant for a specific channel ID
   */
  public async getRagTenant(channelId: string): Promise<string> {
    try {
      const channelConfig = await this.getChannelConfig(channelId);
      return channelConfig.tenant;
    } catch (error) {
      logger.error('Failed to get RAG tenant for channel', { channelId, error: error.message });
      // Fallback to default configuration
      const config = await this.getConfig();
      return config.default.tenant;
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
      logger.error('Failed to get RAG endpoint for channel', { channelId, error: error.message });
      // Fallback to default configuration
      const config = await this.getConfig();
      return config.default.endpoint;
    }
  }

  /**
   * Check if file has been modified and reload if necessary
   */
  private async checkAndReload(): Promise<void> {
    if (this.isLoading) {
      // If already loading, wait for the current load to complete
      if (this.loadPromise) {
        await this.loadPromise;
      }
      return;
    }

    try {
      const stats = await fs.stat(CHANNEL_CONFIG_PATH);
      const currentModified = stats.mtime.getTime();

      if (currentModified > this.lastModified) {
        logger.info('Channel config file changed, reloading...', {
          file: CHANNEL_CONFIG_PATH,
          lastModified: new Date(this.lastModified).toISOString(),
          currentModified: new Date(currentModified).toISOString(),
        });
        await this.loadConfig();
      }
    } catch (error) {
      logger.error('Failed to check channel config file modification time', {
        error: error.message,
        file: CHANNEL_CONFIG_PATH,
      });
    }
  }

  /**
   * Load configuration from YAML file
   */
  private async loadConfig(): Promise<void> {
    if (this.isLoading) {
      if (this.loadPromise) {
        await this.loadPromise;
      }
      return;
    }

    this.isLoading = true;
    this.loadPromise = this.loadConfigCore();

    try {
      await this.loadPromise;
    } finally {
      this.isLoading = false;
      this.loadPromise = null;
    }
  }

  private async loadConfigCore(): Promise<void> {
    try {
      const fileContent = await fs.readFile(CHANNEL_CONFIG_PATH, 'utf8');

      // Parse YAML content using yaml library with type assertion
      const parsedConfig = parse(fileContent, {
        schema: 'failsafe', // Use failsafe schema for better compatibility
        strict: true, // Strict parsing
        uniqueKeys: true, // Ensure unique keys
      }) as ChannelConfig;

      this.config = parsedConfig;

      const stats = await fs.stat(CHANNEL_CONFIG_PATH);
      this.lastModified = stats.mtime.getTime();

      logger.info('Channel configuration loaded successfully', {
        file: CHANNEL_CONFIG_PATH,
        channelCount: this.config.channels.length,
        defaultTenant: this.config.default.tenant,
      });

      // Validate configuration
      this.validateConfig();
    } catch (error) {
      logger.error('Failed to load channel configuration', {
        error: error.message,
        file: CHANNEL_CONFIG_PATH,
      });
      throw new Error(`Failed to load channel configuration: ${error.message}`);
    }
  }

  /**
   * Validate the loaded configuration
   */
  private validateConfig(): void {
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
   * Start watching the configuration file for changes
   */
  private startWatching(): void {
    if (this.isWatching) {
      return;
    }

    try {
      fsSync.watchFile(CHANNEL_CONFIG_PATH, { interval: this.watchInterval }, (curr, prev) => {
        if (curr.mtime > prev.mtime) {
          logger.info('Channel config file changed (via watch), reloading...', {
            file: CHANNEL_CONFIG_PATH,
          });
          this.loadConfig().catch((error) => {
            logger.error('Failed to reload config after file change', { error: error.message });
          });
        }
      });

      this.isWatching = true;
      logger.info('Started watching channel config file', {
        file: CHANNEL_CONFIG_PATH,
        interval: this.watchInterval,
      });
    } catch (error) {
      logger.warn('Failed to start watching channel config file', {
        error: error.message,
        file: CHANNEL_CONFIG_PATH,
      });
    }
  }

  /**
   * Stop watching the configuration file
   */
  public stopWatching(): void {
    if (this.isWatching) {
      fsSync.unwatchFile(CHANNEL_CONFIG_PATH);
      this.isWatching = false;
      logger.info('Stopped watching channel config file');
    }
  }
}

// Create singleton instance
const channelConfigManager = new ChannelConfigManager();

export { ChannelConfigManager };
