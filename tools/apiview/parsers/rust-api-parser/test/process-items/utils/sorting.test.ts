import { describe, it, expect, vi, beforeEach } from 'vitest';
import { getSortedChildIds, sortExternalItems, itemKindOrder } from '../../../src/process-items/utils/sorting';
import { Id, Item, ItemKind } from '../../../rustdoc-types/output/rustdoc-types';
import { ReviewLine, TokenKind } from '../../../src/models/apiview-models';

// Setup mock data
const mockApiJson = {
  index: {
    1: { id: 1, name: "Alpha", inner: { constant: {} } },
    2: { id: 2, name: "Bravo", inner: { function: {} } },
    3: { id: 3, name: "Charlie", inner: { module: {} } },
    4: { id: 4, name: "Delta", inner: { struct: {} } },
    5: { id: 5, name: "Echo", inner: { use: { source: "Foxtrot" } } },
    6: { id: 6, name: "Golf", inner: { enum: {} } },
    7: { id: 7, name: "Hotel", inner: { trait: {} } },
    8: { id: 8, name: "India", inner: { type_alias: {} } },
    9: { id: 9, name: "Juliet", inner: { use: { source: "Alpha" } } },
  },
  paths: {
    100: { path: ["external", "Lima"], kind: ItemKind.Struct },
    101: { path: ["external", "Mike"], kind: ItemKind.Module },
    102: { path: ["external", "November"], kind: ItemKind.Enum },
  }
};

// Mock the main module
vi.mock('../../../src/main', () => ({
  getAPIJson: vi.fn(() => mockApiJson)
}));

describe('Sorting Utils', () => {
  describe('getSortedChildIds', () => {
    it('should separate module and non-module items', () => {
      const childIds = [1, 2, 3, 4, 5, 101];
      const result = getSortedChildIds(childIds);
      
      // Check module items
      expect(result.module).toContain(3);   // Charlie (module)
      expect(result.module).toContain(101); // Mike (module)
      expect(result.module.length).toBe(2);
      
      // Check non-module items
      expect(result.nonModule).toContain(1); // Alpha (constant)
      expect(result.nonModule).toContain(2); // Bravo (function)
      expect(result.nonModule).toContain(4); // Delta (struct)
      expect(result.nonModule).toContain(5); // Echo (use)
      expect(result.nonModule.length).toBe(4);
    });
    
    it('should order items according to itemKindOrder', () => {
      const childIds = [1, 2, 4, 5, 6, 7, 8];
      const result = getSortedChildIds(childIds);
      
      // Check ordering in nonModule array
      const useIndex = result.nonModule.indexOf(5);     // Use item
      const structIndex = result.nonModule.indexOf(4);  // Struct item
      const enumIndex = result.nonModule.indexOf(6);    // Enum item
      const traitIndex = result.nonModule.indexOf(7);   // Trait item
      const functionIndex = result.nonModule.indexOf(2); // Function item
      const typeAliasIndex = result.nonModule.indexOf(8); // TypeAlias item
      const constantIndex = result.nonModule.indexOf(1);  // Constant item
      
      // Check order follows itemKindOrder
      expect(useIndex).toBeLessThan(structIndex);
      expect(structIndex).toBeLessThan(enumIndex);
      expect(enumIndex).toBeLessThan(traitIndex);
      expect(traitIndex).toBeLessThan(functionIndex);
      expect(functionIndex).toBeLessThan(typeAliasIndex);
      expect(typeAliasIndex).toBeLessThan(constantIndex);
    });
    
    it('should sort items of the same kind by name', () => {
      const childIds = [5, 9]; // Two use items: Echo (source: Foxtrot) and Juliet (source: Alpha)
      const result = getSortedChildIds(childIds);
      
      // Juliet's source (Alpha) should come before Echo's source (Foxtrot)
      expect(result.nonModule[0]).toBe(9); // Juliet (source: Alpha)
      expect(result.nonModule[1]).toBe(5); // Echo (source: Foxtrot)
    });
    
    it('should handle items from paths', () => {
      const childIds = [100, 101, 102]; // All from paths
      const result = getSortedChildIds(childIds);
      
      // Check module items
      expect(result.module).toContain(101); // Mike (module)
      expect(result.module.length).toBe(1);
      
      // Check non-module items
      expect(result.nonModule).toContain(100); // Lima (struct)
      expect(result.nonModule).toContain(102); // November (enum)
      expect(result.nonModule.length).toBe(2);
      
      // Struct should come before enum (per itemKindOrder)
      expect(result.nonModule.indexOf(100)).toBeLessThan(result.nonModule.indexOf(102));
    });
    
    it('should handle empty input', () => {
      const result = getSortedChildIds([]);
      
      expect(result.module).toEqual([]);
      expect(result.nonModule).toEqual([]);
    });
    
    it('should handle invalid item IDs', () => {
      const result = getSortedChildIds([999, 998]); // IDs that don't exist
      
      expect(result.module).toEqual([]);
      expect(result.nonModule).toEqual([]);
    });
    
    it('should handle mixed valid and invalid IDs', () => {
      const result = getSortedChildIds([1, 999, 3]);
      
      expect(result.module).toEqual([3]);
      expect(result.nonModule).toEqual([1]);
    });
  });
  
  describe('sortExternalItems', () => {
    it('should sort items by kind according to itemKindOrder', () => {
      const items: ReviewLine[] = [
        createReviewLine('1', ItemKind.Constant, 'MyConstant'),
        createReviewLine('2', ItemKind.Struct, 'MyStruct'),
        createReviewLine('3', ItemKind.Enum, 'MyEnum'),
        createReviewLine('4', ItemKind.Use, 'MyUse')
      ];
      
      const sorted = sortExternalItems([...items]); // Clone array to avoid modifying original
      
      // The expected order: Use, Struct, Enum, Constant
      expect(sorted[0].Tokens[1].Value).toBe(ItemKind.Use);
      expect(sorted[1].Tokens[1].Value).toBe(ItemKind.Struct);
      expect(sorted[2].Tokens[1].Value).toBe(ItemKind.Enum);
      expect(sorted[3].Tokens[1].Value).toBe(ItemKind.Constant);
    });
    
    it('should sort items of the same kind by name', () => {
      const items: ReviewLine[] = [
        createReviewLine('1', ItemKind.Struct, 'StructC'),
        createReviewLine('2', ItemKind.Struct, 'StructA'),
        createReviewLine('3', ItemKind.Struct, 'StructB')
      ];
      
      const sorted = sortExternalItems([...items]);
      
      // Should be sorted as: StructA, StructB, StructC
      expect(sorted[0].Tokens[2].Value).toBe('StructA');
      expect(sorted[1].Tokens[2].Value).toBe('StructB');
      expect(sorted[2].Tokens[2].Value).toBe('StructC');
    });
    
    it('should handle case-insensitive sorting within same kind', () => {
      const items: ReviewLine[] = [
        createReviewLine('1', ItemKind.Struct, 'structC'),
        createReviewLine('2', ItemKind.Struct, 'STRUCTa'),
        createReviewLine('3', ItemKind.Struct, 'StructB')
      ];
      
      const sorted = sortExternalItems([...items]);
      
      // Should sort alphabetically regardless of case
      expect(sorted[0].Tokens[2].Value.toLowerCase()).toBe('structa');
      expect(sorted[1].Tokens[2].Value.toLowerCase()).toBe('structb');
      expect(sorted[2].Tokens[2].Value.toLowerCase()).toBe('structc');
    });
    
    it('should handle empty input', () => {
      const result = sortExternalItems([]);
      expect(result).toEqual([]);
    });
    
    it('should maintain stable order for same kind and name', () => {
      const items: ReviewLine[] = [
        createReviewLine('1', ItemKind.Struct, 'SameStruct'),
        createReviewLine('2', ItemKind.Struct, 'SameStruct'),
        createReviewLine('3', ItemKind.Struct, 'SameStruct')
      ];
      
      const sorted = sortExternalItems([...items]);
      
      // Order should be maintained for identical items
      expect(sorted[0].LineId).toBe('1');
      expect(sorted[1].LineId).toBe('2');
      expect(sorted[2].LineId).toBe('3');
    });
  });
});

/**
 * Helper function to create a ReviewLine for testing
 */
function createReviewLine(id: string, kind: ItemKind, name: string): ReviewLine {
  return {
    LineId: id,
    Tokens: [
      { Kind: TokenKind.Keyword, Value: 'pub' },
      { Kind: TokenKind.Keyword, Value: kind },
      { Kind: TokenKind.TypeName, Value: name }
    ],
    Children: []
  };
}
