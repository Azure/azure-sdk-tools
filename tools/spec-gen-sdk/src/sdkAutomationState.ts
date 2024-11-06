import { BlobStorageBlockBlob, BlobStoragePrefix, map } from '@ts-common/azure-js-dev-tools';

/**
 * The generation status strings for an SDK repository.
 */
// tslint:disable-next-line: variable-name
const SDKAutomationStateStrings = {
  /**
   * The generation process has not yet begun.
   */
  pending: `Pending`,
  /**
   * The generation process is in-progress.
   */
  inProgress: `In-Progress`,
  /**
   * The generation process has failed.
   */
  failed: `Failed`,
  /**
   * The generation process has succeeded.
   */
  succeeded: `Succeeded`,
  /**
   * The generation process has warnings.
   */
  warning: `Warning`
};

/**
 * Get the string representation of the provided state.
 * @param state The state to get the string for.
 */
export function getSDKAutomationStateString(state: SDKAutomationState): string {
  return SDKAutomationStateStrings[state];
}

/**
 * The names of the images that associate with the different SDK Automation states.
 */
// tslint:disable-next-line: variable-name
const SDKAutomationStateImageNames = {
  /**
   * The generation process has not yet begun.
   */
  pending: `pending.gif`,
  /**
   * The generation process is in-progress.
   */
  inProgress: `inProgress.gif`,
  /**
   * The generation process has failed.
   */
  failed: `failed.gif`,
  /**
   * The generation process has succeeded.
   */
  succeeded: `succeeded.gif`,
  /**
   * The generation process has warnings.
   */
  warning: `warning.gif`
};

/**
 * Get the image name for the provided state.
 * @param state The state to get the image name for.
 */
export function getSDKAutomationStateImageName(state: SDKAutomationState): string {
  return SDKAutomationStateImageNames[state];
}

/**
 * Get the image prefix relative to the SDK Automation application's working prefix.
 * @param automationWorkingPrefix The working prefix for the SDK Automation application.
 */
export function getSDKAutomationStateImagePrefix(automationWorkingPrefix: BlobStoragePrefix): BlobStoragePrefix {
  return automationWorkingPrefix.getPrefix('images/');
}

/**
 * Get the image blobs relative to the working prefix for the SDK Automation application.
 * @param automationWorkingPrefix The working prefix for the SDK Automation application.
 */
export function getSDKAutomationStateImageBlobs(automationWorkingPrefix: BlobStoragePrefix): BlobStorageBlockBlob[] {
  const imagePrefix: BlobStoragePrefix = getSDKAutomationStateImagePrefix(automationWorkingPrefix);
  return map(Object.values(SDKAutomationStateImageNames), (imageName: string) => imagePrefix.getBlockBlob(imageName));
}

/**
 * Get the image blob associated with the provided state.
 * @param automationWorkingPrefix The working prefix for the SDK Automation application.
 * @param state The state to get the blob for.
 */
export function getSDKAutomationStateImageBlob(
  automationWorkingPrefix: BlobStoragePrefix,
  state: SDKAutomationState
): BlobStorageBlockBlob {
  const imagePrefix: BlobStoragePrefix = getSDKAutomationStateImagePrefix(automationWorkingPrefix);
  const imageName: string = getSDKAutomationStateImageName(state);
  return imagePrefix.getBlockBlob(imageName);
}

/**
 * The generation status for an SDK repository.
 */
export type SDKAutomationState = keyof typeof SDKAutomationStateStrings;

/**
 * Get the possible values for an SDKAutomationState.
 */
export function getSDKAutomationStates(): SDKAutomationState[] {
  return Object.keys(SDKAutomationStateStrings) as SDKAutomationState[];
}
