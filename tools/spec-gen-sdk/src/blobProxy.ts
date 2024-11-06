import { BlobStorageBlob, BlobStoragePrefix } from '@ts-common/azure-js-dev-tools';

/**
 * An interface that can create blob proxy URLs from blob URLs and that can resolve blob proxy URLs
 * back to their original blob URLs.
 */
export interface BlobProxy {
  /**
   * Get the blob proxy URL for the provided blob.
   * @param blob The blob to get a proxy URL for.
   * @returns The blob proxy URL for the provided blob.
   */
  getProxyURL(workingPrefix: BlobStoragePrefix, blob: BlobStorageBlob): string;
  /**
   * Get the blob URL for the provided blob proxy URL.
   * @param blobProxyURL The proxy URL to get the blob URL for.
   * @param The blob URL for the provided blob proxy URL.
   */
  resolveProxyURL(workingPrefix: BlobStoragePrefix, blobProxyURL: string): Promise<string | undefined>;
}
