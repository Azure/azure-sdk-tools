import { describe, it, expect } from 'vitest';
import { getSuppressionLines } from '../../src/utils/handleSuppressionLines';
import { SdkSuppressionsYml } from '../../src/types/sdkSuppressions';
import { WorkflowContext } from '../../src/types/Workflow';

describe('getSuppressionLines', () => {
  const mockContext: WorkflowContext = {
    config: {
      sdkName: 'azure-sdk-for-go',
    },
  } as WorkflowContext;

  it('should handle null suppression content list', () => {
    const mockSuppressionContentList = null;
    const mockName = 'testPackage';
    const mockBreakingChangeItems = ['breaking change 1', 'breaking change 2'];
    const result = getSuppressionLines(mockSuppressionContentList, mockName, mockBreakingChangeItems, mockContext);

    expect(result.presentSuppressionLines).toEqual(['No suppression file added.']);
    expect(result.absentSuppressionLines).toEqual(['+\tbreaking change 1', '+\tbreaking change 2']);
  });

  it('should handle missing sdk name in suppressions', () => {
    const mockSuppressionContentList: SdkSuppressionsYml = {
      suppressions: {
        'azure-sdk-for-python': [
          {
            package: '@azure/arm-appservice',
            'breaking-changes': ['breaking change 1', 'breaking change 2'],
          },
          {
            package: 'sdk/resourcemanager/appservice/armappservice',
            'breaking-changes': ['breaking change 3', 'breaking change 4'],
          },
        ],
        'azure-sdk-for-js': [
          {
            package: 'azure-mgmt-containerservice',
            'breaking-changes': ['breaking change 5', 'breaking change 6'],
          },
          {
            package: '@azure/arm-containerservice',
            'breaking-changes': ['breaking change 7', 'breaking change 8'],
          },
        ],
      },
    };
    const mockName = '@azure/arm-appservice';
    const mockBreakingChangeItems = ['breaking change 11', 'breaking change 22'];

    const result = getSuppressionLines(mockSuppressionContentList, mockName, mockBreakingChangeItems, mockContext);

    expect(result.presentSuppressionLines).toEqual(['This package has no defined suppressions.']);
    expect(result.absentSuppressionLines).toEqual(['+\tbreaking change 11', '+\tbreaking change 22']);
  });

  it('should handle matching sdk with missing package', () => {
    const mockSuppressionContentList: SdkSuppressionsYml = {
      suppressions: {
        'azure-sdk-for-go': [
          {
            package: '@azure/arm-appservice',
            'breaking-changes': ['breaking change 1', 'breaking change 2'],
          },
          {
            package: 'sdk/resourcemanager/appservice/armappservice',
            'breaking-changes': ['breaking change 3', 'breaking change 4'],
          },
        ],
        'azure-sdk-for-js': [
          {
            package: 'azure-mgmt-containerservice',
            'breaking-changes': ['breaking change 5', 'breaking change 6'],
          },
          {
            package: '@azure/arm-containerservice',
            'breaking-changes': ['breaking change 7', 'breaking change 8'],
          },
        ],
      },
    };
    const mockName = 'azure-mgmt-netapp';
    const mockBreakingChangeItems = ['breaking change 11', 'breaking change 22'];

    const result = getSuppressionLines(mockSuppressionContentList, mockName, mockBreakingChangeItems, mockContext);

    expect(result.presentSuppressionLines).toEqual(['This package has no defined suppressions.']);
    expect(result.absentSuppressionLines).toEqual(['+\tbreaking change 11', '+\tbreaking change 22']);
  });

  it('should handle matching sdk with matching package with no breaking changes defined', () => {
    const mockSuppressionContentList: SdkSuppressionsYml = {
      suppressions: {
        'azure-sdk-for-go': [
          {
            package: '@azure/arm-appservice',
            'breaking-changes': [],
          },
          {
            package: 'sdk/resourcemanager/appservice/armappservice',
            'breaking-changes': ['breaking change 3', 'breaking change 4'],
          },
        ],
        'azure-sdk-for-js': [
          {
            package: 'azure-mgmt-containerservice',
            'breaking-changes': ['breaking change 5', 'breaking change 6'],
          },
          {
            package: '@azure/arm-containerservice',
            'breaking-changes': ['breaking change 7', 'breaking change 8'],
          },
        ],
      },
    };
    const mockName = '@azure/arm-appservice';
    const mockBreakingChangeItems = ['breaking change 11', 'breaking change 22'];

    const result = getSuppressionLines(mockSuppressionContentList, mockName, mockBreakingChangeItems, mockContext);

    expect(result.presentSuppressionLines).toEqual(['This package has no defined suppressions.']);
    expect(result.absentSuppressionLines).toEqual(['+\tbreaking change 11', '+\tbreaking change 22']);
  });

  it('should handle matching sdk with matching package', () => {
    const mockSuppressionContentList: SdkSuppressionsYml = {
      suppressions: {
        'azure-sdk-for-go': [
          {
            package: '@azure/arm-appservice',
            'breaking-changes': ['breaking change 1', 'breaking change 2'],
          },
          {
            package: 'sdk/resourcemanager/appservice/armappservice',
            'breaking-changes': ['breaking change 3', 'breaking change 4'],
          },
        ],
        'azure-sdk-for-js': [
          {
            package: 'azure-mgmt-containerservice',
            'breaking-changes': ['breaking change 5', 'breaking change 6'],
          },
          {
            package: '@azure/arm-containerservice',
            'breaking-changes': ['breaking change 7', 'breaking change 8'],
          },
        ],
      },
    };
    const mockName = '@azure/arm-appservice';
    const mockBreakingChangeItems = ['breaking change 11', 'breaking change 22'];

    const result = getSuppressionLines(mockSuppressionContentList, mockName, mockBreakingChangeItems, mockContext);

    expect(result.presentSuppressionLines).toEqual(['breaking change 1', 'breaking change 2']);
    expect(result.absentSuppressionLines).toEqual(['+\tbreaking change 11', '+\tbreaking change 22']);
  });
});

