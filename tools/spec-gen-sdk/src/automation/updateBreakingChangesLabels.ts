import { addPullRequestLabelOctokit, getPullRequestLabelsOctokit, removePullRequestLabelOctokit } from '../utils/githubUtils';
import { sdkLabels } from '@azure/swagger-validation-common';
import { WorkflowContext } from './workflow';

/**
 * Update "breaking changes" labels on a PR, removing deprecated labels and adding new ones, as appropriate.
 *
 * 1. For an old Pull Request which has deprecatedBreakingChange label, eg: CI-BreakingChange-Go,
 *  a. if it has both deprecated label and deprecated approved label, keep them all.
 *  b. if it only has deprecated label, replace it by new 'breaking-change' label.
 *
 * 2. For a new Pull Request, after run sdk-automation. It will add new breakingChange label when it can meet all case below
 *  a. has breaking changes 
 *  b. not beta version 
 *  c. empty suppression file or uncorrect suppresion file
 * 
 * 3. Remove breaking change label 
 *  a. has correct suppression file 
 *  b. The breaking changes has been resolved.
 * 
 */
export async function updateBreakingChangesLabel(
  context: WorkflowContext,
  hasBreakingChange: boolean,
  isDataPlane: boolean,
  isBetaMgmtSdk: boolean,
  hasSuppressions: boolean,
  hasAbsentSuppressions: boolean,
): Promise<void> {
  const sdkName = context.config.sdkName;
  const sdkBreakingChangesLabelsConfig: {
    breakingChange: string | undefined;
    breakingChangeApproved: string | undefined;
    breakingChangeSuppression: string | undefined;
    breakingChangeSuppressionApproved: string | undefined;
    deprecatedBreakingChange: string | undefined;
    deprecatedBreakingChangeApproved: string | undefined;
  } = sdkLabels[sdkName];

  // Using Github config setting. To control which language's breaking change label should be processed.
  const sdkBreakingChangesLabel =
    context.swaggerToSdkConfig.packageOptions.breakingChangesLabel ??
    context.legacyLangConfig?.breakingChangesLabel?.name;
    
  // Support language: azure-sdk-for-go azure-sdk-for-js azure-sdk-for-python
  const sdkNameList = ['azure-sdk-for-go', 'azure-sdk-for-js', 'azure-sdk-for-python'];

  // If this sdkName has a corresponding breaking change label and it is not in the sdkNameList, it will return directly.
  // If this sdkName does not has a corresponding breaking change label and it is in the sdkNameList, it will log error then return.
  if (!sdkBreakingChangesLabel || !sdkNameList.includes(sdkName)) {
    if(sdkNameList.includes(sdkName)) {
      context.logger.error(`ConfigError: the 'breakingChangesLabel' configuration is missing or is incorrect from the 'swagger_to_sdk_config.json file. Please correct the value or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
    }
    return;
  };

  const presentLabels = await getPullRequestLabelsOctokit(context, context.config.specRepo, context.config.pullNumber);

  const deprecatedBreakingChangeLabel = sdkBreakingChangesLabelsConfig.deprecatedBreakingChange || '';
  const deprecatedBreakingChangeApproved = sdkBreakingChangesLabelsConfig.deprecatedBreakingChangeApproved || '';

  if (presentLabels.includes(deprecatedBreakingChangeLabel) && !presentLabels.includes(deprecatedBreakingChangeApproved) && (isBetaMgmtSdk || !hasBreakingChange)) {
    context.logger.log('github', `Remove deprecated breakingChange Label: ${deprecatedBreakingChangeLabel}`);
    await removePullRequestLabelOctokit(
      context,
      context.config.specRepo,
      context.config.pullNumber,
      deprecatedBreakingChangeLabel
    );
  } else if (presentLabels.includes(deprecatedBreakingChangeLabel) && !presentLabels.includes(deprecatedBreakingChangeApproved)) {
    context.logger.log('github', `Replace deprecated breakingChange Label: ${deprecatedBreakingChangeLabel} to ${sdkBreakingChangesLabel}`);
    await removePullRequestLabelOctokit(
      context,
      context.config.specRepo,
      context.config.pullNumber,
      deprecatedBreakingChangeLabel
    );
    if (!isDataPlane) {
      context.logger.log('github', `It's not a data-plane service and add breakingChange label: ${sdkBreakingChangesLabel}`);
      await addPullRequestLabelOctokit(
       context, context.config.specRepo,
       context.config.pullNumber,
       [sdkBreakingChangesLabel]
     );
    }
  } else if (presentLabels.includes(deprecatedBreakingChangeLabel) && presentLabels.includes(deprecatedBreakingChangeApproved)) {
    context.logger.log('github', `Keep deprecated breakingChange labels: ${deprecatedBreakingChangeLabel} and ${deprecatedBreakingChangeApproved}`);
  } else if (hasBreakingChange && !isBetaMgmtSdk && !isDataPlane && (!hasSuppressions || hasAbsentSuppressions) && !presentLabels.includes(sdkBreakingChangesLabel)) {
    context.logger.log('github', `Add breakingChange label: ${sdkBreakingChangesLabel}`);
    await addPullRequestLabelOctokit(
      context,
      context.config.specRepo,
      context.config.pullNumber,
      [sdkBreakingChangesLabel]
    );
  } else if (hasBreakingChange && !isBetaMgmtSdk && hasSuppressions && !hasAbsentSuppressions && presentLabels.includes(sdkBreakingChangesLabel)) {
    context.logger.log('github', `Remove breakingChange label: ${sdkBreakingChangesLabel} while correct suppression file has been added.`);
    await removePullRequestLabelOctokit(
      context,
      context.config.specRepo,
      context.config.pullNumber,
      sdkBreakingChangesLabel
    );
  } else if (!hasBreakingChange && presentLabels.includes(sdkBreakingChangesLabel)) {
    context.logger.log('github', `Remove breakingChange label: ${sdkBreakingChangesLabel}. The PR was updated without introducing any breaking changes.`);
    await removePullRequestLabelOctokit(
      context,
      context.config.specRepo,
      context.config.pullNumber,
      sdkBreakingChangesLabel
    );
  } else {
    if (presentLabels.includes(deprecatedBreakingChangeApproved)) {
      context.logger.log('info', 'Breakingchange label has been added');
    }
    if (!hasBreakingChange) {
      context.logger.log('info', 'There is no breaking changes.');
    }
    if (isBetaMgmtSdk) {
      context.logger.log('info', 'It is a beta management SDK.');
    }
    if (!(!hasSuppressions || hasAbsentSuppressions)) {
      context.logger.log('info', 'All breaking changes are suppressed hence we should not add any label hence we do nothing.');
    }
  }
}
