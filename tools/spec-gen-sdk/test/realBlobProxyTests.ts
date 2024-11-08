import {
  BlobStorageBlob,
  BlobStoragePrefix,
  InMemoryBlobStorage,
  BlobStorageContainer,
  assertEx,
  URLBuilder
} from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { RealBlobProxy } from '../lib/realBlobProxy';
import { SharedKeyCredential } from '@azure/storage-blob';

describe('realBlobProxy.ts', function() {
  describe('RealBlobProxy', function() {
    describe('getProxyURL()', function() {
      it('with blob with SAS URL', function() {
        const blobStorage = new InMemoryBlobStorage('https://fake.storage.com/?sig=fake-signature&sv=fake-version');
        const blobPrefix: BlobStoragePrefix = blobStorage.getPrefix('container/');
        const blob: BlobStorageBlob = blobPrefix.getBlob('path');
        const blobProxy = new RealBlobProxy('foo://kebab.com/prefix/');
        const proxyUrl: string = blobProxy.getProxyURL(blobPrefix, blob);
        assert.strictEqual(proxyUrl, 'foo://kebab.com/prefix/path');
      });

      it('with blob with non-SAS URL', function() {
        const blobStorage = new InMemoryBlobStorage('https://fake.storage.com/');
        const blobPrefix: BlobStoragePrefix = blobStorage.getPrefix('container/');
        const blob: BlobStorageBlob = blobPrefix.getBlob('path');
        const blobProxy = new RealBlobProxy('foo://kebab.com/prefix/');
        const proxyUrl: string = blobProxy.getProxyURL(blobPrefix, blob);
        assert.strictEqual(proxyUrl, 'foo://kebab.com/prefix/path');
      });
    });

    describe('resolveProxyURL()', function() {
      it('with undefined', async function() {
        const blobStorage = new InMemoryBlobStorage(
          'https://fake.storage.com/',
          new SharedKeyCredential('fake', 'fake-account-key')
        );
        const blobContainer: BlobStorageContainer = blobStorage.getContainer('container');
        await blobContainer.create();
        const blobProxy = new RealBlobProxy('foo://kebab.com/prefix/');
        const blobUrl: string | undefined = await blobProxy.resolveProxyURL(blobContainer, undefined as any);
        assert.strictEqual(blobUrl, undefined);
      });

      it(`with "blah"`, async function() {
        const blobStorage = new InMemoryBlobStorage(
          'https://fake.storage.com/',
          new SharedKeyCredential('fake', 'fake-account-key')
        );
        const blobContainer: BlobStorageContainer = blobStorage.getContainer('container');
        await blobContainer.create();
        const blobProxy = new RealBlobProxy('foo://kebab.com/prefix/');
        const blobUrl: string | undefined = await blobProxy.resolveProxyURL(blobContainer, 'blah');
        assert.strictEqual(blobUrl, undefined);
      });

      it(`with "foo://kebab.com/prefix/container/blob"`, async function() {
        const blobStorage = new InMemoryBlobStorage(
          'https://fake.storage.com/',
          new SharedKeyCredential('fake', 'fake-account-key')
        );
        const blobContainer: BlobStorageContainer = blobStorage.getContainer('container');
        await blobContainer.create();
        const blobProxy = new RealBlobProxy('foo://kebab.com/prefix/');
        const blobUrl: string = (await blobProxy.resolveProxyURL(blobContainer, 'foo://kebab.com/prefix/blob'))!;
        assertEx.definedAndNotEmpty(blobUrl, 'blobUrl');
        const blobUrlBuilder: URLBuilder = URLBuilder.parse(blobUrl);
        assert.strictEqual(blobUrlBuilder.getScheme(), 'https');
        assert.strictEqual(blobUrlBuilder.getHost(), 'fake.storage.com');
        assert.strictEqual(blobUrlBuilder.getPort(), undefined);
        assert.strictEqual(blobUrlBuilder.getPath(), '/container/blob');
        assertEx.definedAndNotEmpty(blobUrlBuilder.getQuery());
      });
    });
  });
});
