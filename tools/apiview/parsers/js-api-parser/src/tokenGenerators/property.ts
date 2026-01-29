import { ApiItem, ApiItemKind, ApiProperty, ApiPropertyItem } from "@microsoft/api-extractor-model";
import { ReviewLine, ReviewToken, TokenKind } from "../models";
import { TokenGenerator } from "./index";
import { createToken, processExcerptTokens, isComplexType, generateComplexTypeLines } from "./helpers";

function isValid(item: ApiItem): item is ApiPropertyItem {
  return item.kind === ApiItemKind.Property || item.kind === ApiItemKind.PropertySignature;
}

function generate(item: ApiPropertyItem, deprecated?: boolean): ReviewLine {
  const tokens: ReviewToken[] = [];
  
  if (item.kind !== ApiItemKind.Property && item.kind !== ApiItemKind.PropertySignature) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Property token generator.`,
    );
  }

  if (item instanceof ApiProperty && item.isStatic) {
    tokens.push(createToken(TokenKind.Keyword, "static", { hasSuffixSpace: true, deprecated }));
  }

  if (item.isReadonly) {
    tokens.push(createToken(TokenKind.Keyword, "readonly", { hasSuffixSpace: true, deprecated }));
  }

  tokens.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));

  if (item.isOptional) {
    tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
  }

  tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));

  // Check if this is a complex type that needs multi-line formatting
  const typeText = item.propertyTypeExcerpt.spannedTokens?.length
    ? item.propertyTypeExcerpt.spannedTokens.map(t => t.text).join("")
    : item.propertyTypeExcerpt.text || "";

  if (isComplexType(typeText)) {
    // Add opening brace on same line
    tokens.push(createToken(TokenKind.Punctuation, "{", { deprecated }));
    
    // Generate child lines for the complex type content
    const childLines = generateComplexTypeLines(typeText, deprecated);
    
    return {
      Tokens: tokens,
      Children: childLines,
    };
  } else {
    // Simple type - add tokens inline
    if (item.propertyTypeExcerpt.spannedTokens?.length) {
      processExcerptTokens(item.propertyTypeExcerpt.spannedTokens, tokens, deprecated);
    } else if (item.propertyTypeExcerpt.text) {
      tokens.push(createToken(TokenKind.TypeName, item.propertyTypeExcerpt.text.trim(), { deprecated }));
    }
    
    return {
      Tokens: tokens,
    };
  }
}

export const propertyTokenGenerator: TokenGenerator<ApiPropertyItem> = {
  isValid,
  generate,
};
