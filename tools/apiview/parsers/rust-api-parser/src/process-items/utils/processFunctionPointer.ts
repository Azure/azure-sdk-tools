import { FunctionPointer } from "../../../rustdoc-types/output/rustdoc-types";
import { ReviewToken, TokenKind } from "../../models/apiview-models";
import { typeToReviewTokens } from "./typeToReviewTokens";

/**
 * Processes a function pointer type and converts it to ReviewTokens
 * 
 * @param functionPointer The function pointer type to process
 * @returns Array of ReviewTokens representing the function pointer
 */
export function processFunctionPointer(functionPointer: FunctionPointer): ReviewToken[] {
  const tokens: ReviewToken[] = [];
  
  // Add generic parameters (for<'a> etc)
  if (functionPointer.generic_params.length > 0) {
    tokens.push({ Kind: TokenKind.Keyword, Value: "for", HasSuffixSpace: false });
    tokens.push({ Kind: TokenKind.Punctuation, Value: "<", HasSuffixSpace: false });
    
    functionPointer.generic_params.forEach((param, index) => {
      tokens.push({ Kind: TokenKind.Text, Value: param.name, HasSuffixSpace: false });
      
      if (index < functionPointer.generic_params.length - 1) {
        tokens.push({ Kind: TokenKind.Punctuation, Value: ", ", HasSuffixSpace: false });
      }
    });
    
    tokens.push({ Kind: TokenKind.Punctuation, Value: "> ", HasSuffixSpace: false });
  }
  
  // Add function modifiers
  const header = functionPointer.header;
  
  // Handle unsafe
  if (header.is_unsafe) {
    tokens.push({ Kind: TokenKind.Keyword, Value: "unsafe", HasSuffixSpace: true });
  }
  
  // Handle extern ABI
  if (header.abi !== "Rust") {
    tokens.push({ Kind: TokenKind.Keyword, Value: "extern", HasSuffixSpace: true });
    
    let abiString = "";
    if (typeof header.abi === "string") {
      // For simple ABI strings
      abiString = header.abi;
    } else {
      // For complex ABI objects
      const abiKey = Object.keys(header.abi)[0];
      if (abiKey) {
        abiString = abiKey;
      }
    }
    
    if (abiString && abiString !== "Rust") {
      tokens.push({ Kind: TokenKind.Text, Value: `"${abiString}"`, HasSuffixSpace: true });
    }
  }
  
  // Handle const
  if (header.is_const) {
    tokens.push({ Kind: TokenKind.Keyword, Value: "const", HasSuffixSpace: true });
  }
  
  // Handle async
  if (header.is_async) {
    tokens.push({ Kind: TokenKind.Keyword, Value: "async", HasSuffixSpace: true });
  }
  
  // Add fn keyword
  tokens.push({ Kind: TokenKind.Keyword, Value: "fn", HasSuffixSpace: false });
  
  // Add parameters
  tokens.push({ Kind: TokenKind.Punctuation, Value: "(", HasSuffixSpace: false });
  
  const signature = functionPointer.sig;
  signature.inputs.forEach(([paramName, paramType], index) => {
    // Add parameter name if present
    if (paramName) {
      tokens.push({ Kind: TokenKind.Text, Value: paramName, HasSuffixSpace: false });
      tokens.push({ Kind: TokenKind.Punctuation, Value: ": ", HasSuffixSpace: false });
    }
    
    // Add parameter type
    tokens.push(...typeToReviewTokens(paramType));
    
    // Add comma if not the last parameter
    if (index < signature.inputs.length - 1) {
      tokens.push({ Kind: TokenKind.Punctuation, Value: ", ", HasSuffixSpace: false });
    }
  });
  
  // Add C-variadic "..." if needed
  if (signature.is_c_variadic) {
    if (signature.inputs.length > 0) {
      tokens.push({ Kind: TokenKind.Punctuation, Value: ", ", HasSuffixSpace: false });
    }
    tokens.push({ Kind: TokenKind.Punctuation, Value: "...", HasSuffixSpace: false });
  }
  
  tokens.push({ Kind: TokenKind.Punctuation, Value: ")", HasSuffixSpace: false });
  
  // Add return type if present
  if (signature.output) {
    tokens.push({ Kind: TokenKind.Punctuation, Value: " -> ", HasSuffixSpace: false });
    tokens.push(...typeToReviewTokens(signature.output));
  }
  
  return tokens;
}
