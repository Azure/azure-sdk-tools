export abstract class AzureSDKError extends Error {
  protected namePrefix = 'AzureSDK';
  constructor(message: string) {
    super(message);
    this.name = this.namePrefix + 'Error';
  }
}

export abstract class AzureSDKInputError extends AzureSDKError {
  protected namePrefix = 'AzureSDKInput';
  constructor(message: string) {
    super(message);
    this.name = this.namePrefix + 'Error';
  }
}
