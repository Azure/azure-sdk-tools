import { describe, it, expect, vi } from 'vitest';
import { typeToReviewTokens } from '../../../src/process-items/utils/typeToReviewTokens';
import { TokenKind } from '../../../src/models/apiview-models';
import * as externalReexports from '../../../src/process-items/utils/externalReexports';
import { Type } from '../../../rustdoc-types/output/rustdoc-types';

// Mock the addExternalReferencesIfNotExists function
vi.mock('../../../src/process-items/utils/externalReexports', () => ({
  addExternalReferencesIfNotExists: vi.fn(),
}));

describe('typeToReviewTokens', () => {
  it('should handle null or undefined type', () => {
    // @ts-ignore - Testing null explicitly
    const result = typeToReviewTokens(null);
    expect(result).toEqual([{ Kind: TokenKind.Text, Value: "unknown", HasSuffixSpace: false }]);
  });

  it('should handle primitive string type', () => {
    const stringType: Type = { primitive: 'String' };
    const result = typeToReviewTokens(stringType);
    expect(result).toEqual([{ Kind: TokenKind.TypeName, Value: "String", HasSuffixSpace: false }]);
  });

  it('should handle resolved_path type without args', () => {
    const type: Type = {
      resolved_path: {
        id: 123,
        name: 'Vec',
      },
    };
    
    const result = typeToReviewTokens(type);
    
    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({
      Kind: TokenKind.TypeName,
      Value: 'Vec',
      HasSuffixSpace: false,
      NavigateToId: '123',
    });
    expect(externalReexports.addExternalReferencesIfNotExists).toHaveBeenCalledWith(123);
  });

  it('should handle primitive type', () => {
    const type: Type = {
      primitive: 'usize'
    };
    
    const result = typeToReviewTokens(type);
    
    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({
      Kind: TokenKind.TypeName,
      Value: 'usize',
      HasSuffixSpace: false,
    });
  });

  it('should handle generic type', () => {
    const type: Type = {
      generic: 'T'
    };
    
    const result = typeToReviewTokens(type);
    
    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({
      Kind: TokenKind.TypeName,
      Value: 'T',
      HasSuffixSpace: false,
    });
  });

  it('should handle tuple type', () => {
    const type: Type = {
      tuple: [
        { primitive: 'u32' },
        { primitive: 'String' }
      ]
    };
    
    const result = typeToReviewTokens(type);
    
    // Should be (u32, String) with 6 tokens including the empty spacer
    expect(result).toHaveLength(6);
    expect(result[0].Value).toBe('(');
    expect(result[1].Value).toBe('u32');
    expect(result[2].Value).toBe(',');
    expect(result[3].Value).toBe('String');
    expect(result[4].Value).toBe('');
    expect(result[5].Value).toBe(')');
  });

  it('should handle slice type', () => {
    const type: Type = {
      slice: { primitive: 'u8' }
    };
    
    const result = typeToReviewTokens(type);
    
    // Should be [u8]
    expect(result).toHaveLength(3);
    expect(result[0].Value).toBe('[');
    expect(result[1].Value).toBe('u8');
    expect(result[2].Value).toBe(']');
  });

  it('should handle array type', () => {
    const type: Type = {
      array: {
        type: { primitive: 'u8' },
        len: '4' // Fixed: using string instead of number
      }
    };
    
    const result = typeToReviewTokens(type);
    
    // Should be [u8; 4]
    expect(result).toHaveLength(5);
    expect(result[0].Value).toBe('[');
    expect(result[1].Value).toBe('u8');
    expect(result[2].Value).toBe(';');
    expect(result[3].Value).toBe('4');
    expect(result[4].Value).toBe(']');
  });

  it('should handle borrowed reference type', () => {
    const type: Type = {
      borrowed_ref: {
        type: { primitive: 'str' },
        lifetime: 'a',
        is_mutable: true
      }
    };
    
    const result = typeToReviewTokens(type);
    
    // Should be &mut a str
    expect(result).toHaveLength(4);
    expect(result[0].Value).toBe('&');
    expect(result[1].Value).toBe('mut ');
    expect(result[2].Value).toBe('a ');
    expect(result[3].Value).toBe('str');
  });

  it('should handle raw pointer type', () => {
    const type: Type = {
      raw_pointer: {
        type: { primitive: 'u8' },
        is_mutable: false
      }
    };
    
    const result = typeToReviewTokens(type);
    
    // Should be *const u8
    expect(result).toHaveLength(3);
    expect(result[0].Value).toBe('*');
    expect(result[1].Value).toBe('const');
    expect(result[2].Value).toBe('u8');
  });
});
