
import { diffStringArrays } from './utils';
import { SdkSuppressionsYml, SdkPackageSuppressionsEntry } from '@azure/swagger-validation-common';
import { WorkflowContext } from '../automation/workflow';

export type SDKSuppressionContentList = Map<string, {content: SdkSuppressionsYml| null, sdkSuppressionFilePath: string | undefined, errors: string[]}>
/**
 * get special suppression by sdkName and packageName info and
 * diff breaking changes info to display in pull request comment
 * @param suppressionContentList
 * @param name
 * @param breakingChangeItems
 * @param context
 * @returns suppressionLines
 * 
 */
export function getSuppressionLines(
  suppressionContentList: SdkSuppressionsYml | null,
  name: string,
  breakingChangeItems: string[],
  context: WorkflowContext
): {
  presentSuppressionLines: string[],
  absentSuppressionLines: string[]
} {
  const suppressionLines: {
    presentSuppressionLines: string[],
    absentSuppressionLines: string[]
  } = {
    presentSuppressionLines: [],
    absentSuppressionLines: []
  };

  if(!suppressionContentList) {
    const diff = diffStringArrays([], breakingChangeItems);
    suppressionLines.presentSuppressionLines = [`No suppression file added.`];
    suppressionLines.absentSuppressionLines = diff.diffResult.filter(inertChange => inertChange.startsWith('+\t'));
    return suppressionLines;
  }

  // 1. The suppression file does not have an sdkName that matches.
  if (!Object.keys(suppressionContentList.suppressions).includes(context.config.sdkName)) {
    const diff = diffStringArrays([], breakingChangeItems);
    suppressionLines.presentSuppressionLines = [`This package has no defined suppressions.`];
    suppressionLines.absentSuppressionLines = diff.diffResult.filter(inertChange => inertChange.startsWith('+\t'));
    return suppressionLines;
  }
  // 2. The suppression file contains a match for sdkName
  const suppressionSDKList: SdkPackageSuppressionsEntry[] = suppressionContentList.suppressions[context.config.sdkName];
  // Verify that the package name matches the one in the suppression file.
  if (!suppressionSDKList.map(item => item.package).includes(name || '')) {
    const diff = diffStringArrays([], breakingChangeItems);
    suppressionLines.presentSuppressionLines = [`This package has no defined suppressions.`];
    suppressionLines.absentSuppressionLines = diff.diffResult.filter(inertChange => inertChange.startsWith('+\t'));
  } else {
    for (const supSDK of suppressionSDKList) {
      const genPackageName = name;
      const supPackageName = supSDK.package;
      if (genPackageName === supPackageName) {
        // The variable 'left' to indicate that we will add suppression lines to avoid breaking changes.
        const left = supSDK['breaking-changes'] as string[];
        // The variable 'right' means that the SDK produces breaking changes
        const right = breakingChangeItems;
        const diff = diffStringArrays(left, right);
        if (left.length === 0) {
          suppressionLines.presentSuppressionLines = [`This package has no defined suppressions.`];
        } else {
          suppressionLines.presentSuppressionLines = left;
        }
        suppressionLines.absentSuppressionLines = diff.diffResult.filter(inertChange => inertChange.startsWith('+\t'));
        break;
      }
    }
  }

  return suppressionLines;
}
