import { describe, it, expect, vi } from 'vitest';
import { processConstant } from '../../src/process-items/processConstant';
import { loadTestAsset } from '../utils/testUtils';
import { Constant, Item, Type } from '../../rustdoc-types/output/rustdoc-types';
import { isConstantItem } from '../../src/process-items/utils/typeGuards';

// Mock the main.ts module first to prevent the error during import
vi.mock('../../src/main', () => ({
  getAPIJson: vi.fn()
}));

// Mock the isConstantItem function to simplify testing
vi.mock('../../src/process-items/utils/typeGuards', () => ({
  isConstantItem: vi.fn()
}));

describe('processConstant', () => {
  it('should process a constant item correctly', () => {
    // Mock implementation for this test
    vi.mocked(isConstantItem).mockReturnValue(true);
    
    const constantItem = loadTestAsset<Item>('constants.rust.json');
    const result = processConstant(constantItem);
    const expected = loadTestAsset('constants.json');
    
    expect(result).toEqual(expected);
  });

  it('should handle constants without documentation', () => {
    // Mock implementation for this test
    vi.mocked(isConstantItem).mockReturnValue(true);
    
    const constantItem = loadTestAsset<Item>('constants.rust.json');
    // Remove docs to test the no-docs scenario
    const noDocs = { ...constantItem, docs: undefined };
    const result = processConstant(noDocs);
    
    // Expect result to not include doc line
    expect(result?.length).toBe(1);
    expect(result?.[0].LineId).toBe('const_example');
  });

  it('should handle constants without value', () => {
    // Mock implementation for this test
    vi.mocked(isConstantItem).mockReturnValue(true);
    
    // Modified to use safe property access with proper casting
    const constantItem = loadTestAsset<Item>('constants.rust.json');
    
    // Instead of accessing inner.constant directly, create a modified version 
    // of the test item with a modified inner.constant that has no value
    const mockType: Type = { primitive: 'u32' };
    const mockConst: Constant = { expr: 'const VALUE', is_literal: true };
    
    const noValue = { 
      ...constantItem, 
      inner: { 
        constant: { 
          type: mockType,
          const: {
            ...mockConst,
            value: undefined 
          } 
        } 
      } 
    };
    
    const result = processConstant(noValue);
    
    // Verify that there's no comment token for value
    const lastTokens = result?.[result.length - 1].Tokens.slice(-2);
    expect(lastTokens?.[0].Value).toBe(';');
    expect(lastTokens?.length).toBe(1);
  });

  it('should return undefined for non-constant items', () => {
    // Mock implementation to return false for this test
    vi.mocked(isConstantItem).mockReturnValue(false);
    
    const nonConstantItem = { 
      id: 123, 
      crate_id: 0, 
      name: 'NotConstant',
      visibility: 'public',
      links: {},
      attrs: [],
      inner: { function: {} }
    } as unknown as Item;
    
    const result = processConstant(nonConstantItem);
    expect(result).toBeUndefined();
  });
});
