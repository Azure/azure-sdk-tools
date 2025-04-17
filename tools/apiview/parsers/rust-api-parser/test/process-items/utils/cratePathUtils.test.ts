import { describe, it, expect, vi } from 'vitest';
import { replaceCratePath } from '../../../src/process-items/utils/cratePathUtils';

// Mock the main module to control PACKAGE_NAME
vi.mock('../../../src/main', () => ({
  PACKAGE_NAME: 'test_crate'
}));

describe('cratePathUtils', () => {
  describe('replaceCratePath', () => {
    it('should replace crate:: with package name', () => {
      const result = replaceCratePath('crate::module::function');
      expect(result).toBe('test_crate::module::function');
    });
    
    it('should not modify paths that don\'t start with crate::', () => {
      const paths = [
        'std::vec::Vec',
        'other_crate::module',
        'just_a_name'
      ];
      
      paths.forEach(path => {
        const result = replaceCratePath(path);
        expect(result).toBe(path);
      });
    });
    
    it('should handle the case where the path is exactly crate::', () => {
      const result = replaceCratePath('crate::');
      expect(result).toBe('test_crate::');
    });
  });
});
