import { BlobProxy } from './blobProxy';
import { BlobStoragePrefix, BlobStorageBlob, Duration, URLBuilder } from '@ts-common/azure-js-dev-tools';

export class RealBlobProxy implements BlobProxy {
  private readonly blobProxyUrlBuilderPrefix: URLBuilder;

  constructor(private readonly blobProxyUrlPrefix: string) {
    this.blobProxyUrlBuilderPrefix = URLBuilder.parse(blobProxyUrlPrefix);
  }

  public getProxyURL(workingPrefix: BlobStoragePrefix, blob: BlobStorageBlob): string {
    const workingPrefixBlobName: string = workingPrefix.path.blobName;
    const blobPathRelativeToWorkingPrefix: string = workingPrefixBlobName
      ? blob.path.blobName.substring(workingPrefixBlobName.length)
      : blob.path.blobName;
    return `${this.blobProxyUrlPrefix}${blobPathRelativeToWorkingPrefix}`;
  }

  public resolveProxyURL(workingPrefix: BlobStoragePrefix, blobProxyURL: string): Promise<string | undefined> {
    let result: string | undefined;
    if (blobProxyURL && blobProxyURL.startsWith(this.blobProxyUrlPrefix)) {
      const proxyBlobUrlBuilder: URLBuilder = URLBuilder.parse(blobProxyURL);
      const proxyBlobUrlPath: string | undefined = proxyBlobUrlBuilder.getPath();
      if (proxyBlobUrlPath) {
        const prefixPath: string | undefined = this.blobProxyUrlBuilderPrefix.getPath();
        const proxyBlobUrlRelativePath: string = prefixPath
          ? proxyBlobUrlPath.substring(prefixPath.length)
          : proxyBlobUrlPath;
        const blob: BlobStorageBlob = workingPrefix.getBlob(proxyBlobUrlRelativePath);
        result = blob.getURL({
          sasToken: {
            startTime: Duration.minutes(-5).fromNow(),
            endTime: Duration.minutes(30).fromNow()
          }
        });
      }
    }
    return Promise.resolve(result);
  }
}
