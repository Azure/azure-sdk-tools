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
  warning: `Warning`,
  /**
   * The generation process exited due to missing language configuration in tspconfig.yaml or reamd.md .
   */
  notEnabled: `NotEnabled`
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
 * The generation status for an SDK repository.
 */
export type SDKAutomationState = keyof typeof SDKAutomationStateStrings;

/**
 * Get the possible values for an SDKAutomationState.
 */
export function getSDKAutomationStates(): SDKAutomationState[] {
  return Object.keys(SDKAutomationStateStrings) as SDKAutomationState[];
}
