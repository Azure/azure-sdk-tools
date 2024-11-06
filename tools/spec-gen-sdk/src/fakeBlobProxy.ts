import { BlobStorageBlob, BlobStoragePrefix } from '@ts-common/azure-js-dev-tools';
import { BlobProxy } from './blobProxy';

/**
 * A BlobProxy implementation that doesn't proxy blob URLs.
 */
export class FakeBlobProxy implements BlobProxy {
  public getProxyURL(_workingPrefix: BlobStoragePrefix, blob: BlobStorageBlob): string {
    return blob.getURL();
  }

  public resolveProxyURL(_workingPrefix: BlobStoragePrefix, blobProxyURL: string): Promise<string | undefined> {
    return Promise.resolve(blobProxyURL);
  }
}
