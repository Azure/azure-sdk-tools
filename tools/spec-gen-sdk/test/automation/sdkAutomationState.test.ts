import { describe, it, expect } from 'vitest';
import { 
  getSDKAutomationStateString, 
  getSDKAutomationStateImageName, 
  getSDKAutomationStates,
  SDKAutomationState
} from '../../src/automation/sdkAutomationState';

describe('SDKAutomationState', () => {
  describe('getSDKAutomationStateString', () => {
    it('should return correct string representation for each state', () => {
      const testCases: [SDKAutomationState, string][] = [
        ['pending', 'Pending'],
        ['inProgress', 'In-Progress'],
        ['failed', 'Failed'],
        ['succeeded', 'Succeeded'],
        ['warning', 'Warning'],
        ['notEnabled', 'NotEnabled']
      ];

      testCases.forEach(([state, expected]) => {
        expect(getSDKAutomationStateString(state)).toBe(expected);
      });
    });
  });

  describe('getSDKAutomationStateImageName', () => {
    it('should return correct image name for each state', () => {
      const testCases: [SDKAutomationState, string][] = [
        ['pending', 'pending.gif'],
        ['inProgress', 'inProgress.gif'],
        ['failed', 'failed.gif'],
        ['succeeded', 'succeeded.gif'],
        ['warning', 'warning.gif']
      ];

      testCases.forEach(([state, expected]) => {
        expect(getSDKAutomationStateImageName(state)).toBe(expected);
      });
    });
  });

  describe('getSDKAutomationStates', () => {
    it('should return all possible states', () => {
      const states = getSDKAutomationStates();
      const expectedStates: SDKAutomationState[] = [
        'pending',
        'inProgress',
        'failed',
        'succeeded',
        'warning',
        'notEnabled'
      ];

      expect(states).toHaveLength(expectedStates.length);
      expectedStates.forEach(state => {
        expect(states).toContain(state);
      });
    });

    it('should return an array with no duplicates', () => {
      const states = getSDKAutomationStates();
      const uniqueStates = new Set(states);
      expect(states.length).toBe(uniqueStates.size);
    });
  });
});