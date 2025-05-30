import { describe, it, expect } from 'vitest';
import { MessagePrefix, formatMessage, toolError, externalError, configError, toolWarning, externalWarning, configWarning, hasPrefix } from '../../src/utils/messageUtils';

describe('messageUtils', () => {
  describe('formatMessage', () => {
    it('should format message with prefix only', () => {
      const result = formatMessage(MessagePrefix.ToolError, 'test message');
      expect(result).toBe('[SGS-ERR] test message');
    });

    it('should format message with prefix and details', () => {
      const result = formatMessage(MessagePrefix.ToolError, 'test message', 'extra details');
      expect(result).toBe('[SGS-ERR] test message. extra details');
    });
  });

  describe('helper functions', () => {
    it('should format tool error', () => {
      expect(toolError('error')).toBe('[SGS-ERR] error');
      expect(toolError('error', 'details')).toBe('[SGS-ERR] error. details');
    });

    it('should format external error', () => {
      expect(externalError('error')).toBe('[EXT-ERR] error');
      expect(externalError('error', 'details')).toBe('[EXT-ERR] error. details');
    });

    it('should format config error', () => {
      expect(configError('error')).toBe('[CONFIG-ERR] error');
      expect(configError('error', 'details')).toBe('[CONFIG-ERR] error. details');
    });

    it('should format tool warning', () => {
      expect(toolWarning('warning')).toBe('[SGS-WARN] warning');
      expect(toolWarning('warning', 'details')).toBe('[SGS-WARN] warning. details');
    });

    it('should format external warning', () => {
      expect(externalWarning('warning')).toBe('[EXT-WARN] warning');
      expect(externalWarning('warning', 'details')).toBe('[EXT-WARN] warning. details');
    });

    it('should format config warning', () => {
      expect(configWarning('warning')).toBe('[CONFIG-WARN] warning');
      expect(configWarning('warning', 'details')).toBe('[CONFIG-WARN] warning. details');
    });
  });

  describe('hasPrefix', () => {
    it('should return true for messages with valid prefixes', () => {
      expect(hasPrefix('[SGS-ERR] message')).toBe(true);
      expect(hasPrefix('[EXT-WARN] message')).toBe(true);
      expect(hasPrefix('[CONFIG-ERR] message')).toBe(true);
    });

    it('should return false for messages without valid prefixes', () => {
      expect(hasPrefix('message')).toBe(false);
      expect(hasPrefix('[INVALID] message')).toBe(false);
      expect(hasPrefix('[SGS-INVALID] message')).toBe(false);
    });
  });
});
