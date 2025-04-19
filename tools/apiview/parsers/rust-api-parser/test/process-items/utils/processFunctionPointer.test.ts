import { describe, it, expect, vi } from 'vitest';
import { processFunctionPointer } from '../../../src/process-items/utils/processFunctionPointer';
import { TokenKind } from '../../../src/models/apiview-models';
import { Abi, FunctionHeader, FunctionPointer, FunctionSignature, Type } from '../../../rustdoc-types/output/rustdoc-types';

// Mock the main.ts module first to prevent the error during import
vi.mock('../../../src/main', () => ({
  getAPIJson: vi.fn()
}));

describe('processFunctionPointer', () => {
  it('should process a basic function pointer', () => {
    // Create a valid FunctionPointer object that matches the interface
    const fnPointer: FunctionPointer = {
      sig: {
        inputs: [
          ['arg1', { primitive: 'i32' }],
          ['arg2', { primitive: 'i32' }]
        ],
        output: { primitive: 'i32' },
        is_c_variadic: false
      },
      generic_params: [],
      header: {
        is_unsafe: false,
        is_const: false,
        is_async: false,
        abi: 'Rust'
      }
    };
    
    const result = processFunctionPointer(fnPointer);
    
    // Should be "fn(i32, i32) -> i32"
    expect(result).toHaveLength(8);
    expect(result[0].Kind).toBe(TokenKind.Keyword);
    expect(result[0].Value).toBe('fn');
    expect(result[1].Value).toBe('(');
    expect(result[2].Value).toBe('i32');
    expect(result[3].Value).toBe(',');
    expect(result[4].Value).toBe('i32');
    expect(result[5].Value).toBe(')');
    expect(result[6].Value).toBe(' -> ');
    expect(result[7].Value).toBe('i32');
  });
  
  it('should handle unsafe function pointers', () => {
    const fnPointer: FunctionPointer = {
      sig: {
        inputs: [
          ['arg', { primitive: 'i32' }]
        ],
        output: { primitive: 'i32' },
        is_c_variadic: false
      },
      generic_params: [],
      header: {
        is_unsafe: true,
        is_const: false,
        is_async: false,
        abi: 'Rust'
      }
    };
    
    const result = processFunctionPointer(fnPointer);
    
    // Should start with "unsafe fn"
    expect(result[0].Value).toBe('unsafe');
    expect(result[1].Value).toBe('fn');
  });
  
  it('should include ABI if specified', () => {
    const fnPointer: FunctionPointer = {
      sig: {
        inputs: [
          ['arg', { primitive: 'i32' }]
        ],
        output: { primitive: 'i32' },
        is_c_variadic: false
      },
      generic_params: [],
      header: {
        is_unsafe: false,
        is_const: false,
        is_async: false,
        abi: {
          C: { unwind: false }
        }
      }
    };
    
    const result = processFunctionPointer(fnPointer);
    
    // Should include "extern "C""
    expect(result[0].Value).toBe('extern');
    expect(result[1].Value).toBe('"C"');
    expect(result[2].Value).toBe('fn');
  });
  
  it('should handle function pointers with no inputs', () => {
    const fnPointer: FunctionPointer = {
      sig: {
        inputs: [],
        output: { primitive: 'i32' },
        is_c_variadic: false
      },
      generic_params: [],
      header: {
        is_unsafe: false,
        is_const: false,
        is_async: false,
        abi: 'Rust'
      }
    };
    
    const result = processFunctionPointer(fnPointer);
    
    // Should be "fn() -> i32"
    expect(result[0].Value).toBe('fn');
    expect(result[1].Value).toBe('(');
    expect(result[2].Value).toBe(')');
    expect(result[3].Value).toBe(' -> ');
  });
  
  it('should handle function pointers with no output', () => {
    const fnPointer: FunctionPointer = {
      sig: {
        inputs: [
          ['arg', { primitive: 'i32' }]
        ],
        is_c_variadic: false
      },
      generic_params: [],
      header: {
        is_unsafe: false,
        is_const: false,
        is_async: false,
        abi: 'Rust'
      }
    };
    
    const result = processFunctionPointer(fnPointer);
    
    // Should be "fn(i32)"
    const lastToken = result[result.length - 1];
    expect(lastToken.Value).toBe(')');
  });
  
  it('should handle complex function pointers', () => {
    const strRef: Type = {
      borrowed_ref: {
        type: { primitive: 'str' },
        is_mutable: false
      }
    };
    
    const option: Type = {
      resolved_path: {
        id: 123,
        name: 'Option',
        args: {
          angle_bracketed: {
            args: [{ type: { primitive: 'u32' } }],
            constraints: []
          }
        }
      }
    };
    
    const returnTuple: Type = {
      tuple: [
        { primitive: 'bool' },
        { primitive: 'u32' }
      ]
    };
    
    const fnPointer: FunctionPointer = {
      sig: {
        inputs: [
          ['str_ref', strRef],
          ['opt', option]
        ],
        output: returnTuple,
        is_c_variadic: false
      },
      generic_params: [],
      header: {
        is_unsafe: true,
        is_const: false,
        is_async: false,
        abi: {
          C: { unwind: false }
        }
      }
    };
    
    const result = processFunctionPointer(fnPointer);
    
    expect(result.length).toBeGreaterThan(5);
    expect(result[0].Value).toBe('unsafe');
    expect(result[1].Value).toBe('extern');
    expect(result[2].Value).toBe('"C"');
  });
});
