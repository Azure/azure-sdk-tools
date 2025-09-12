import { AzureSDKInputError } from './commonErrors.js';

export class URLNotSupportedError extends AzureSDKInputError {
  constructor(url: URL) {
    super(`URL not supported: ${url.href}. Only GitHub pull request URLs are supported.`);
    this.name = this.namePrefix + 'URLNotSupportedError';
  }
}
