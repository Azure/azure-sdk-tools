import { parse } from 'yaml';
import { BlobServiceClient } from '@azure/storage-blob';
import { setTimeout } from 'timers/promises';
import { logger } from '../logging/logger.js';
import config from './config.js';
import { getAzureCredential } from '../common/shared.js';

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

// Azure Blob Storage configuration
const CONTAINER_NAME = 'bot-configs';
const BLOB_NAME = 'channel.yaml';
const FALLBACK_RAG_ENDPOINT = 'https://azuresdkbot-dqh7g6btekbfa3hh.eastasia-01.azurewebsites.net';
const FALLBACK_RAG_TENANT = 'azure_sdk_qa_bot';

class ChannelConfigManager {
  private config: ChannelConfig | null = null;
  private lastModified: Date | null = null;
  private isWatching: boolean = false;
  private loadPromise: Promise<void> | null = null;
  private blobServiceClient: BlobServiceClient | null = null;
  private readonly maxRetry = 6;
  private readonly watchInterval: number = 5000; // 5 seconds for blob storage
  private readonly botId: string;

  constructor() {
    this.botId = config.MicrosoftAppId;
  }

  /**
   * Initialize the blob service client
   */
  private async initializeBlobClient(): Promise<void> {
    if (this.blobServiceClient) {
      return;
    }

    try {
      // Extract storage account name from the storage URL
      const storageUrl = config.azureStorageUrl;
      if (!storageUrl) {
        throw new Error('AZURE_STORAGE_URL environment variable is not set');
      }

      // Convert table storage URL to blob storage URL
      // From: https://account.table.core.windows.net/
      // To: https://account.blob.core.windows.net/
      // TODO: update env file in the future
      const blobStorageUrl = storageUrl.replace('.table.core.windows.net', '.blob.core.windows.net');

      const credential = await getAzureCredential(this.botId);

      this.blobServiceClient = new BlobServiceClient(blobStorageUrl, credential);

      logger.info('Blob service client initialized', { storageUrl: blobStorageUrl });
    } catch (error) {
      logger.error('Failed to initialize blob service client', { error: error.message });
      throw error;
    }
  }

  public async initialize(): Promise<void> {
    await this.initializeBlobClient();
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
  public async getChannelConfig(channelId: string): Promise<ChannelItem> {
    const config = await this.getConfig();

    // Find matching channel
    const channel = config.channels.find((ch) => ch.id === channelId);
    if (channel) {
      return {
        name: channel.name,
        id: channel.id,
        tenant: channel.tenant || config.default.tenant,
        endpoint: channel.endpoint || config.default.endpoint,
      };
    }

    // Return default configuration with generated name and id if no match found
    return {
      name: 'default',
      id: channelId, // Use the requested channelId
      tenant: config.default.tenant,
      endpoint: config.default.endpoint,
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
      logger.error(`Failed to get RAG tenant for channel, fallback to ${FALLBACK_RAG_TENANT}`, {
        channelId,
        error: error.message,
      });
      return FALLBACK_RAG_TENANT;
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
      logger.error(`Failed to get RAG endpoint for channel, fallback to ${FALLBACK_RAG_ENDPOINT}`, {
        channelId,
        error: error.message,
      });
      return FALLBACK_RAG_ENDPOINT;
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
      // Blob client should already be initialized during initialize()
      if (!this.blobServiceClient) {
        throw new Error('Blob service client not initialized');
      }

      const containerClient = this.blobServiceClient.getContainerClient(CONTAINER_NAME);
      const blobClient = containerClient.getBlobClient(BLOB_NAME);

      const properties = await blobClient.getProperties();
      const currentModified = properties.lastModified;

      if (!this.lastModified || (currentModified && currentModified > this.lastModified)) {
        logger.info('Channel config blob changed, reloading...', {
          container: CONTAINER_NAME,
          blob: BLOB_NAME,
          lastModified: this.lastModified?.toISOString(),
          currentModified: currentModified?.toISOString(),
        });
        await this.loadConfig();
      }
    } catch (error) {
      logger.error('Failed to check channel config blob modification time', {
        error: error.message,
        container: CONTAINER_NAME,
        blob: BLOB_NAME,
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
      // Blob client should already be initialized during initialize()
      if (!this.blobServiceClient) {
        throw new Error('Blob service client not initialized');
      }

      const containerClient = this.blobServiceClient.getContainerClient(CONTAINER_NAME);
      const blobClient = containerClient.getBlobClient(BLOB_NAME);

      // Download blob content
      let fileContent: string | undefined;
      for (let retry = 0; retry < this.maxRetry; retry++) {
        try {
          const downloadBlockBlobResponse = await blobClient.download();
          fileContent = await this.streamToString(downloadBlockBlobResponse.readableStreamBody!);
          break;
        } catch (error) {
          logger.warn(`Failed to download channel configs in ${retry} times.`, { error: error.message });
          continue;
        }
      }

      if (!fileContent) {
        logger.warn(`Failed to download channel config blob after ${this.maxRetry} attempts, still use old config.`);
        return;
      }

      logger.info('Channel configuration loaded successfully from blob storage', { fileContent });

      // Parse YAML content using yaml library with type assertion
      const parsedConfig = parse(fileContent, {
        schema: 'core',
        strict: true, // Strict parsing
        uniqueKeys: true, // Ensure unique keys
      }) as ChannelConfig;

      this.config = parsedConfig;

      // Get blob properties for last modified time
      const properties = await blobClient.getProperties();
      this.lastModified = properties.lastModified || new Date();

      logger.info('Channel configuration loaded successfully from blob storage', {
        container: CONTAINER_NAME,
        blob: BLOB_NAME,
        channelCount: this.config.channels.length,
        defaultTenant: this.config.default.tenant,
      });

      // Validate configuration
      this.ensureConfig();
    } catch (error) {
      logger.error('Failed to load channel configuration from blob storage', {
        error: error.message,
        container: CONTAINER_NAME,
        blob: BLOB_NAME,
      });
      throw new Error(`Failed to load channel configuration: ${error.message}`);
    }
  }

  /**
   * Convert readable stream to string using async/await
   */
  private async streamToString(readableStream: NodeJS.ReadableStream): Promise<string> {
    const chunks: Buffer[] = [];

    for await (const chunk of readableStream) {
      if (chunk instanceof Buffer) {
        chunks.push(chunk);
      } else if (typeof chunk === 'string') {
        chunks.push(Buffer.from(chunk, 'utf8'));
      } else {
        chunks.push(Buffer.from(chunk));
      }
    }

    return Buffer.concat(chunks).toString('utf8');
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
      container: CONTAINER_NAME,
      blob: BLOB_NAME,
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
