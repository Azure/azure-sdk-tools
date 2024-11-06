import { BlobStorageBlob, BlobStoragePrefix, InMemoryBlobStorage } from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { FakeBlobProxy } from '../lib/fakeBlobProxy';

describe('fakeBlobProxy.ts', function() {
  describe('FakeBlobProxy', function() {
    describe('getProxyURL()', function() {
      it('with blob with SAS URL', function() {
        const blobStorage = new InMemoryBlobStorage('https://fake.storage.com/?sig=fake-signature&sv=fake-version');
        const blobPrefix: BlobStoragePrefix = blobStorage.getPrefix('container/');
        const blob: BlobStorageBlob = blobPrefix.getBlob('path');
        const blobProxy = new FakeBlobProxy();
        const proxyUrl: string = blobProxy.getProxyURL(blobPrefix, blob);
        assert.strictEqual(proxyUrl, 'https://fake.storage.com/container/path');
      });

      it('with blob with non-SAS URL', function() {
        const blobStorage = new InMemoryBlobStorage('https://fake.storage.com/');
        const blobPrefix: BlobStoragePrefix = blobStorage.getPrefix('container/');
        const blob: BlobStorageBlob = blobPrefix.getBlob('path');
        const blobProxy = new FakeBlobProxy();
        const proxyUrl: string = blobProxy.getProxyURL(blobPrefix, blob);
        assert.strictEqual(proxyUrl, 'https://fake.storage.com/container/path');
      });
    });

    describe('resolveProxyURL()', function() {
      it('with undefined', async function() {
        const blobStorage = new InMemoryBlobStorage('https://fake.storage.com/');
        const blobPrefix: BlobStoragePrefix = blobStorage.getPrefix('container/');
        const blobProxy = new FakeBlobProxy();
        const blobUrl: string | undefined = await blobProxy.resolveProxyURL(blobPrefix, undefined as any);
        assert.strictEqual(blobUrl, undefined);
      });

      it(`with "blah"`, async function() {
        const blobStorage = new InMemoryBlobStorage('https://fake.storage.com/');
        const blobPrefix: BlobStoragePrefix = blobStorage.getPrefix('container/');
        const blobProxy = new FakeBlobProxy();
        const blobUrl: string | undefined = await blobProxy.resolveProxyURL(blobPrefix, 'blah');
        assert.strictEqual(blobUrl, 'blah');
      });
    });
  });
});
