import { BlobServiceClient, ContainerClient } from '@azure/storage-blob';
import { logger } from '../logging/logger.js';
import config from './config.js';
import { getAzureCredential } from '../common/shared.js';

const DEFAULT_MAX_RETRY = 6;

/**
 * Shared blob client for accessing Azure Blob Storage
 */
class BlobClientManager {
  private static instance: BlobClientManager | null = null;
  private blobServiceClient: BlobServiceClient | null = null;
  private containerClient: ContainerClient | null = null;
  private readonly botId: string;
  private readonly maxRetry: number;

  private constructor(maxRetry: number = DEFAULT_MAX_RETRY) {
    this.botId = config.MicrosoftAppId;
    this.maxRetry = maxRetry;
  }

  /**
   * Get the singleton instance of BlobClientManager
   */
  public static getInstance(): BlobClientManager {
    if (!BlobClientManager.instance) {
      BlobClientManager.instance = new BlobClientManager();
    }
    return BlobClientManager.instance;
  }

  /**
   * Initialize the blob service client
   */
  public async initialize(): Promise<void> {
    if (this.blobServiceClient && this.containerClient) {
      return;
    }

    try {
      const credential = await getAzureCredential(this.botId);
      this.blobServiceClient = new BlobServiceClient(config.azureBlobStorageUrl, credential);
      this.containerClient = this.blobServiceClient.getContainerClient(config.blobContainerName);

      logger.info('Blob service client initialized', { storageUrl: config.azureBlobStorageUrl, container: config.blobContainerName });
    } catch (error) {
      logger.error('Failed to initialize blob service client', { error: error.message });
      throw error;
    }
  }

  /**
   * Get the container client
   */
  public getContainerClient(): ContainerClient {
    if (!this.containerClient) {
      throw new Error('Blob client not initialized. Call initialize() first.');
    }
    return this.containerClient;
  }

  /**
   * Download blob content with retry logic
   */
  public async downloadBlobContent(blobName: string): Promise<string | undefined> {
    const containerClient = this.getContainerClient();
    const blobClient = containerClient.getBlobClient(blobName);

    for (let retry = 0; retry < this.maxRetry; retry++) {
      try {
        const downloadBlockBlobResponse = await blobClient.download();
        return await this.streamToString(downloadBlockBlobResponse.readableStreamBody!);
      } catch (error) {
        logger.warn(`Failed to download ${blobName} in ${retry + 1} attempt(s).`, { error: error.message });
        continue;
      }
    }

    return undefined;
  }

  /**
   * Get blob last modified time
   */
  public async getBlobLastModifiedTime(blobName: string): Promise<Date | undefined> {
    const containerClient = this.getContainerClient();
    const blobClient = containerClient.getBlobClient(blobName);
    const properties = await blobClient.getProperties();
    return properties?.lastModified;
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
}

export { BlobClientManager };
