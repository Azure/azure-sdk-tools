import { describe, it, expect, vi, beforeEach } from 'vitest';
import { processModule } from '../../src/process-items/processModule';
import { Crate, Id, Item, ItemKind, StructKind, Use } from '../../rustdoc-types/output/rustdoc-types';
import { TokenKind } from '../../src/models/apiview-models';

// Move ALL mock definitions to the top before any dependent code
vi.mock('../../src/main', () => ({
  getAPIJson: vi.fn()
}));

vi.mock('../../src/process-items/processItem', () => ({
  processItem: vi.fn().mockImplementation((item) => {
    // Return a simplified representation of processed items for testing
    return [{
      LineId: `processed_${item.id}`,
      Tokens: [
        { Kind: TokenKind.Keyword, Value: 'pub' },
        { Kind: TokenKind.TypeName, Value: item.name || 'unnamed' }
      ]
    }];
  })
}));

vi.mock('../../src/process-items/utils/sorting', () => ({
  getSortedChildIds: vi.fn().mockImplementation((childIds) => {
    // Return a simple split - items with odd IDs are modules, even IDs are non-modules
    return {
      module: childIds.filter(id => Number(id) % 2 === 1),
      nonModule: childIds.filter(id => Number(id) % 2 === 0)
    };
  }),
  itemKindOrder: [ItemKind.Function, ItemKind.Struct, ItemKind.Module]
}));

vi.mock('../../src/process-items/utils/externalReexports', () => ({
  getModuleChildIdsByPath: vi.fn().mockReturnValue([]),
  processModuleReexport: vi.fn().mockImplementation((itemId, itemSummary) => {
    return [{
      LineId: `reexport_${itemId}`,
      Tokens: [
        { Kind: TokenKind.Keyword, Value: 'pub mod' },
        { Kind: TokenKind.TypeName, Value: 'reexported_module' }
      ]
    }];
  })
}));

describe('processModule', () => {
  // Create a base item with required properties
  const createBaseItem = (id: number, name: string): Omit<Item, 'inner'> => ({
    id: id,
    crate_id: 0,
    name: name,
    visibility: 'public',
    links: {},
    attrs: []
  });

  // Setup API JSON mock
  let mockApiJson: Partial<Crate>;
  
  beforeEach(() => {
    vi.clearAllMocks();
    
    // Initialize mockApiJson here in beforeEach
    mockApiJson = {
      index: {
        // Simple module without children
        1: {
          ...createBaseItem(1, 'empty_module'),
          docs: 'Empty module documentation',
          inner: { module: { items: [], is_crate: false, is_stripped: false } }
        },
        // Module with direct children
        2: {
          ...createBaseItem(2, 'parent_module'),
          inner: { module: { items: [4, 6], is_crate: false, is_stripped: false } }
        },
        // Module with re-exported module
        3: {
          ...createBaseItem(3, 'reexport_module'),
          inner: { module: { items: [5], is_crate: false, is_stripped: false } }
        },
        // Regular function
        4: {
          ...createBaseItem(4, 'some_function'),
          inner: { function: { 
            sig: { inputs: [], is_c_variadic: false },
            generics: { params: [], where_predicates: [] },
            header: { is_unsafe: false, is_const: false, is_async: false, abi: 'Rust' },
            has_body: true
          } }
        },
        // Use item pointing to external module
        5: {
          ...createBaseItem(5, 'use_external'),
          inner: { use: { 
            id: 101,
            is_glob: false,
            source: 'external::module',
            name: 'module'
          } }
        },
        // Struct item
        6: {
          ...createBaseItem(6, 'some_struct'),
          inner: { struct: { 
            kind: { plain: { fields: [], has_stripped_fields: false } },
            generics: { params: [], where_predicates: [] },
            impls: []
          } }
        },
        // Use item pointing to internal module
        7: {
          ...createBaseItem(7, 'use_internal'),
          inner: { use: { 
            id: 1,
            is_glob: false,
            source: 'empty_module',
            name: 'empty_module'
          } }
        },
        // Module with use item that does glob import
        8: {
          ...createBaseItem(8, 'glob_module'),
          inner: { module: { items: [9], is_crate: false, is_stripped: false } }
        },
        // Use item with glob import
        9: {
          ...createBaseItem(9, 'glob_import'),
          inner: { use: { 
            id: 102,
            is_glob: true,
            source: 'external::glob_module::*',
            name: '*'
          } }
        }
      },
      paths: {
        101: {
          path: ['external', 'module'],
          kind: ItemKind.Module,
          crate_id: 0
        },
        102: {
          path: ['external', 'glob_module'],
          kind: ItemKind.Module,
          crate_id: 0
        }
      }
    };
    
    // Set up the mock return value
    const mainModule = require('../../src/main');
    mainModule.getAPIJson.mockReturnValue(mockApiJson);
  });

  it('should process an empty module', () => {
    const item = mockApiJson.index?.[1] as Item;
    const result = processModule(item);

    // Should have two lines: one for documentation and one for the module
    expect(result).toHaveLength(2);
    
    // Check documentation line
    expect(result[0].LineId).toBe('1_docs_0');
    expect(result[0].Tokens[0].Kind).toBe(TokenKind.Comment);
    expect(result[0].Tokens[0].Value).toBe('/// Empty module documentation');
    
    // Check module line
    expect(result[1].LineId).toBe('1');
    expect(result[1].Tokens).toHaveLength(3); // pub mod name {
    expect(result[1].Tokens[0].Value).toBe('pub mod');
    expect(result[1].Tokens[1].Value).toBe('empty_module');
    expect(result[1].Tokens[2].Value).toBe('{');
    
    // Should have closing brace on the same line for empty modules
    expect(result[1].Tokens[3].Value).toBe('}');
  });

  it('should process a module with children', () => {
    const item = mockApiJson.index?.[2] as Item;
    const result = processModule(item);

    // Should have at least two lines: module declaration and closing brace
    expect(result.length).toBeGreaterThanOrEqual(2);
    
    // Check module line
    expect(result[0].LineId).toBe('2');
    expect(result[0].Tokens[0].Value).toBe('pub mod');
    expect(result[0].Tokens[1].Value).toBe('parent_module');
    
    // Check for children in the module
    expect(result[0].Children).toBeDefined();
    expect(result[0].Children?.length).toBeGreaterThan(0);
    
    // Check that there's a closing brace line
    const lastLineTokens = result[1].Tokens;
    expect(lastLineTokens[0].Kind).toBe(TokenKind.Punctuation);
    expect(lastLineTokens[0].Value).toBe('}');
  });

  it('should handle module with a parent module', () => {
    const item = mockApiJson.index?.[1] as Item; // empty_module
    const parentModule = { prefix: 'parent', id: 100 };
    const result = processModule(item, parentModule);

    // Check module line has parent reference
    expect(result[1].LineId).toBe('1');
    expect(result[1].Tokens).toHaveLength(5); // pub mod parent::empty_module {
    expect(result[1].Tokens[0].Value).toBe('pub mod');
    expect(result[1].Tokens[1].Value).toBe('parent');
    expect(result[1].Tokens[2].Value).toBe('::');
    expect(result[1].Tokens[3].Value).toBe('empty_module');
    
    // Check for NavigationDisplayName with full path
    expect(result[1].Tokens[3].NavigationDisplayName).toBe('parent::empty_module');
  });

  it('should process a module with re-exported modules', () => {
    const item = mockApiJson.index?.[3] as Item; // reexport_module
    const result = processModule(item);

    // Check for the presence of re-exported module in the result
    const reexportFound = result.some(line => line.LineId === 'reexport_101');
    expect(reexportFound).toBe(true);
  });

  it('should skip non-module items', () => {
    // Try to process a non-module item
    const item = mockApiJson.index?.[4] as Item; // some_function
    const result = processModule(item);
    
    // Should return empty array for non-module items
    expect(result).toEqual([]);
  });

  it('should handle module with glob imports', () => {
    const item = mockApiJson.index?.[8] as Item; // glob_module
    const result = processModule(item);
    
    // Check that the module was processed
    expect(result[0].LineId).toBe('8');
    expect(result[0].Tokens[0].Value).toBe('pub mod');
    expect(result[0].Tokens[1].Value).toBe('glob_module');
    
    // In the mock setup, the getModuleChildIdsByPath should have been called 
    // for the globbed module, though we just return an empty array in our mock
    const externalReexports = require('../../src/process-items/utils/externalReexports');
    expect(externalReexports.getModuleChildIdsByPath).toHaveBeenCalled();
  });

  it('should handle modules without documentation', () => {
    // Module without docs
    const item = { ...mockApiJson.index?.[2] } as Item;
    delete item.docs;
    
    const result = processModule(item);
    
    // Should only have module-related lines, no documentation lines
    expect(result[0].LineId).toBe('2');
    expect(result[0].Tokens[0].Value).toBe('pub mod');
  });
});
