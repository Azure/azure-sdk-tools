import { describe, it, expect, vi } from 'vitest';
import { processGenerics, processGenericArgs, createGenericBoundTokens } from '../../../src/process-items/utils/processGenerics';
import { TokenKind } from '../../../src/models/apiview-models';
import { GenericBound, GenericParamDef, Path, TraitBoundModifier } from '../../../rustdoc-types/output/rustdoc-types';

// Mock the main.ts module first to prevent the error during import
vi.mock('../../../src/main', () => ({
  getAPIJson: vi.fn()
}));

describe('processGenerics', () => {
  it('should process generics with no params or where predicates', () => {
    const generics = {
      params: [],
      where_predicates: []
    };
    
    const result = processGenerics(generics);
    
    expect(result.params).toEqual([]);
    expect(result.wherePredicates).toEqual([]);
  });
  
  it('should process generics with type params', () => {
    const generics = {
      params: [
        {
          name: 'T',
          kind: {
            type: {
              bounds: [],
              is_synthetic: false // Added required property
            }
          }
        }
      ],
      where_predicates: []
    };
    
    const result = processGenerics(generics);
    
    expect(result.params).toHaveLength(2);
    expect(result.params[0].Value).toBe('<');
    expect(result.params[1].Value).toBe('T');
  });
  
  it('should process generics with type params and bounds', () => {
    const trait: Path = {
      id: 123,
      name: 'Debug'
    };
    
    const generics = {
      params: [
        {
          name: 'T',
          kind: {
            type: {
              bounds: [
                {
                  trait_bound: {
                    trait: trait,
                    generic_params: [], // Added required property
                    modifier: TraitBoundModifier.None // Added required property
                  }
                }
              ],
              is_synthetic: false // Added required property
            }
          }
        }
      ],
      where_predicates: []
    };
    
    const result = processGenerics(generics);
    
    // Should produce <T: Debug>
    expect(result.params.length).toBeGreaterThan(3);
    expect(result.params[0].Value).toBe('<');
    expect(result.params[1].Value).toBe('T');
    expect(result.params[2].Value).toBe(':');
    expect(result.params[3].Value).toBe('Debug');
  });
  
  it('should process generics with where predicates', () => {
    const trait: Path = {
      id: 123,
      name: 'Clone'
    };
    
    const generics = {
      params: [
        {
          name: 'T',
          kind: {
            type: {
              bounds: [],
              is_synthetic: false // Added required property
            }
          }
        }
      ],
      where_predicates: [
        {
          bound_predicate: {
            type: { generic: 'T' },
            bounds: [
              {
                trait_bound: {
                  trait: trait,
                  generic_params: [], // Added required property
                  modifier: TraitBoundModifier.None // Added required property
                }
              }
            ],
            generic_params: [] // Added required property
          }
        }
      ]
    };
    
    const result = processGenerics(generics);
    
    // Should include where clause
    expect(result.wherePredicates.length).toBeGreaterThan(0);
    expect(result.wherePredicates[0].Value).toBe('where');
    expect(result.wherePredicates[0].Kind).toBe(TokenKind.Keyword);
  });
});

describe('processGenericArgs', () => {
  it('should handle null args', () => {
    const result = processGenericArgs(null);
    expect(result).toEqual([]);
  });
  
  it('should process angle bracketed args', () => {
    const args = {
      angle_bracketed: {
        args: [
          { type: { primitive: 'u32' } },
          { type: { generic: 'T' } }
        ],
        constraints: []
      }
    };
    
    const result = processGenericArgs(args);
    
    // Should produce <u32, T>
    expect(result).toHaveLength(5);
    expect(result[0].Value).toBe('<');
    expect(result[1].Value).toBe('u32');
    expect(result[2].Value).toBe(',');
    expect(result[3].Value).toBe('T');
    expect(result[4].Value).toBe('>');
  });
  
  it('should process parenthesized args', () => {
    const args = {
      parenthesized: {
        inputs: [
          { primitive: 'u32' },
          { primitive: 'String' }
        ],
        output: { primitive: 'bool' }
      }
    };
    
    const result = processGenericArgs(args);
    
    // Should produce (u32, String) -> bool
    expect(result.length).toBeGreaterThan(5);
    expect(result[0].Value).toBe('(');
    expect(result[1].Value).toBe('u32');
    expect(result[2].Value).toBe(',');
    expect(result[3].Value).toBe('String');
    expect(result[4].Value).toBe(')');
    expect(result[5].Value).toBe(' -> ');
    expect(result[6].Value).toBe('bool');
  });
});

describe('createGenericBoundTokens', () => {
  it('should process trait bounds', () => {
    const trait: Path = {
      id: 123,
      name: 'Clone'
    };
    
    const bounds: GenericBound[] = [
      {
        trait_bound: {
          trait: trait,
          generic_params: [], // Added required property
          modifier: TraitBoundModifier.None // Added required property
        }
      }
    ];
    
    const result = createGenericBoundTokens(bounds);
    
    expect(result).toHaveLength(1);
    expect(result[0].Value).toBe('Clone');
    expect(result[0].Kind).toBe(TokenKind.TypeName);
    expect(result[0].NavigateToId).toBe('123');
  });
  
  it('should process multiple bounds with + separator', () => {
    const traits: Path[] = [
      { id: 123, name: 'Clone' },
      { id: 456, name: 'Debug' }
    ];
    
    const bounds: GenericBound[] = [
      {
        trait_bound: {
          trait: traits[0],
          generic_params: [], // Added required property
          modifier: TraitBoundModifier.None // Added required property
        }
      },
      {
        trait_bound: {
          trait: traits[1],
          generic_params: [], // Added required property
          modifier: TraitBoundModifier.None // Added required property
        }
      }
    ];
    
    const result = createGenericBoundTokens(bounds);
    
    // Should produce Clone + Debug
    expect(result).toHaveLength(3);
    expect(result[0].Value).toBe('Clone');
    expect(result[1].Value).toBe(' + ');
    expect(result[2].Value).toBe('Debug');
  });
  
  it('should process lifetime bounds', () => {
    const bounds: GenericBound[] = [
      {
        outlives: "'a"
      }
    ];
    
    const result = createGenericBoundTokens(bounds);
    
    expect(result).toHaveLength(1);
    expect(result[0].Value).toBe("'a");
    expect(result[0].Kind).toBe(TokenKind.TypeName);
  });
});
