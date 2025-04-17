import { describe, it, expect, vi, beforeEach } from 'vitest';
import { addExternalReferencesIfNotExists, externalReferencesLines, processModuleReexport, getModuleChildIdsByPath } from '../../../src/process-items/utils/externalReexports';
import { TokenKind } from '../../../src/models/apiview-models';
import { Crate, ItemKind, ItemSummary } from '../../../rustdoc-types/output/rustdoc-types';

// Mock the main module
const mockGetAPIJson = vi.fn();
vi.mock('../../../src/main', () => ({
  getAPIJson: () => mockGetAPIJson()
}));

describe('externalReexports', () => {
  beforeEach(() => {
    // Clear the externalReferencesLines array before each test
    externalReferencesLines.length = 0;
    
    // Reset the mock
    mockGetAPIJson.mockReset();
  });
  
  describe('addExternalReferencesIfNotExists', () => {
    it('should add external reference line if it does not exist', () => {
      // Setup mock API JSON with proper type structure
      mockGetAPIJson.mockReturnValue({
        index: {},
        paths: {
          123: {
            path: ['std', 'vec', 'Vec'],
            kind: ItemKind.Struct,
            crate_id: 0
          }
        }
      } as unknown as Crate); // Cast to Crate to satisfy TypeScript
      
      addExternalReferencesIfNotExists(123);
      
      expect(externalReferencesLines).toHaveLength(1);
      expect(externalReferencesLines[0].LineId).toBe('123');
      expect(externalReferencesLines[0].Tokens[0].Value).toBe('pub');
      expect(externalReferencesLines[0].Tokens[1].Value).toBe('struct');
      expect(externalReferencesLines[0].Tokens[2].Value).toBe('std::vec::Vec');
    });
    
    it('should not add reference if item exists in index', () => {
      mockGetAPIJson.mockReturnValue({
        index: {
          123: { 
            id: 123, 
            name: 'test',
            crate_id: 0,
            visibility: 'public',
            links: {},
            attrs: [],
            inner: { module: { items: [] } }
          }
        },
        paths: {}
      } as unknown as Crate);
      
      addExternalReferencesIfNotExists(123);
      
      expect(externalReferencesLines).toHaveLength(0);
    });
    
    it('should not add reference if it does not exist in paths', () => {
      mockGetAPIJson.mockReturnValue({
        index: {},
        paths: {}
      } as unknown as Crate);
      
      addExternalReferencesIfNotExists(123);
      
      expect(externalReferencesLines).toHaveLength(0);
    });
    
    it('should not add duplicate references', () => {
      mockGetAPIJson.mockReturnValue({
        index: {},
        paths: {
          123: {
            path: ['std', 'vec', 'Vec'],
            kind: ItemKind.Struct,
            crate_id: 0
          }
        }
      } as unknown as Crate);
      
      // Add the same reference twice
      addExternalReferencesIfNotExists(123);
      addExternalReferencesIfNotExists(123);
      
      // Should only add it once
      expect(externalReferencesLines).toHaveLength(1);
    });
  });
  
  describe('processModuleReexport', () => {
    it('should process an empty module', () => {
      const apiJson = {
        paths: {}
      };
      
      const itemSummary: ItemSummary = {
        path: ['std', 'collections'],
        kind: ItemKind.Module,
        crate_id: 0
      };
      
      const parentModule = {
        prefix: 'std',
        id: 456
      };
      
      const result = processModuleReexport(123, itemSummary, apiJson as unknown as Crate, parentModule);
      
      expect(result).toHaveLength(1);
      
      // Check the header line
      const headerLine = result[0];
      expect(headerLine.LineId).toBe('123');
      
      // Check that the closing brace is on the same line for empty modules
      const lastToken = headerLine.Tokens[headerLine.Tokens.length - 1];
      expect(lastToken.Kind).toBe(TokenKind.Punctuation);
      expect(lastToken.Value).toBe('}');
    });
    
    it('should process a module with children', () => {
      const apiJson = {
        paths: {
          789: {
            path: ['std', 'collections', 'HashMap'],
            kind: ItemKind.Struct,
            crate_id: 0
          }
        }
      };
      
      const itemSummary: ItemSummary = {
        path: ['std', 'collections'],
        kind: ItemKind.Module,
        crate_id: 0
      };
      
      const parentModule = {
        prefix: 'std',
        id: 456
      };
      
      const result = processModuleReexport(123, itemSummary, apiJson as unknown as Crate, parentModule);
      
      expect(result).toHaveLength(2);
      
      // Check the header line
      const headerLine = result[0];
      expect(headerLine.LineId).toBe('123');
      expect(headerLine.Children).toHaveLength(1);
      
      // Check the closing brace line
      const closingLine = result[1];
      expect(closingLine.RelatedToLine).toBe('123');
      expect(closingLine.Tokens[0].Value).toBe('}');
    });
  });
  
  describe('getModuleChildIdsByPath', () => {
    it('should find child items by path', () => {
      const apiJson = {
        paths: {
          123: {
            path: ['std', 'collections', 'HashMap'],
            kind: ItemKind.Struct,
            crate_id: 0
          },
          456: {
            path: ['std', 'collections', 'HashSet'],
            kind: ItemKind.Struct,
            crate_id: 0
          },
          789: {
            path: ['std', 'vec', 'Vec'],
            kind: ItemKind.Struct,
            crate_id: 0
          }
        }
      };
      
      const result = getModuleChildIdsByPath('std::collections', apiJson as unknown as Crate);
      
      expect(result).toContain(123);
      expect(result).toContain(456);
      expect(result).not.toContain(789);
    });
    
    it('should not include the module itself', () => {
      const apiJson = {
        paths: {
          123: {
            path: ['std', 'collections'],
            kind: ItemKind.Module,
            crate_id: 0
          },
          456: {
            path: ['std', 'collections', 'HashMap'],
            kind: ItemKind.Struct,
            crate_id: 0
          }
        }
      };
      
      const result = getModuleChildIdsByPath('std::collections', apiJson as unknown as Crate);
      
      expect(result).not.toContain(123);
      expect(result).toContain(456);
    });
    
    it('should handle items without valid paths', () => {
      const apiJson = {
        paths: {
          123: {
            // @ts-ignore - Testing with null path
            path: null,
            kind: ItemKind.Struct,
            crate_id: 0
          },
          456: {
            path: ['std', 'collections', 'HashMap'],
            kind: ItemKind.Struct,
            crate_id: 0
          }
        }
      };
      
      const result = getModuleChildIdsByPath('std::collections', apiJson as unknown as Crate);
      
      expect(result).not.toContain(123);
      expect(result).toContain(456);
    });
  });
});
