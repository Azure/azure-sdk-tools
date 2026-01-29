import { ExcerptToken, ExcerptTokenKind } from "@microsoft/api-extractor-model";
import { ReviewLine, ReviewToken, TokenKind } from "../models";

/** Helper to create a token with common properties */
export function createToken(
  kind: TokenKind,
  value: string,
  options?: {
    hasSuffixSpace?: boolean;
    hasPrefixSpace?: boolean;
    navigateToId?: string;
    deprecated?: boolean;
  },
): ReviewToken {
  return {
    Kind: kind,
    Value: value,
    HasSuffixSpace: options?.hasSuffixSpace ?? false,
    HasPrefixSpace: options?.hasPrefixSpace ?? false,
    NavigateToId: options?.navigateToId,
    IsDeprecated: options?.deprecated,
  };
}

/**
 * Determines if a token needs a leading space based on its value
 * @param value The token value
 * @returns true if the token needs a leading space
 */
export function needsLeadingSpace(value: string): boolean {
  return value === "|" || value === "&" || value === "is" || value === "extends";
}

/**
 * Determines if a token needs a trailing space based on its value
 * @param value The token value
 * @returns true if the token needs a trailing space
 */
export function needsTrailingSpace(value: string): boolean {
  return value === "|" || value === "&" || value === "is" || value === "extends" || value === ":" || value === ";";
}

/**
 * Determines if a token should have a space before it
 * @param value The token value
 * @returns true if the token needs a space before
 */
export function needsSpaceBefore(value: string): boolean {
  return value === "{" || value === "|" || value === "&" || value === "is" || value === "extends";
}

/**
 * Check if a type is complex (contains { }) and should be formatted on multiple lines
 */
export function isComplexType(text: string): boolean {
  return text.includes("{") && text.includes("}");
}

/**
 * Generate child ReviewLines for a complex type's content
 * @param typeText The full type text including braces
 * @param deprecated Whether the type is deprecated
 * @returns Array of ReviewLine objects for the type's content
 */
export function generateComplexTypeLines(typeText: string, deprecated?: boolean): ReviewLine[] {
  const lines: ReviewLine[] = [];
  
  // Extract content between outer braces
  const firstBrace = typeText.indexOf("{");
  const lastBrace = typeText.lastIndexOf("}");
  
  if (firstBrace === -1 || lastBrace === -1) {
    return lines;
  }
  
  const content = typeText.substring(firstBrace + 1, lastBrace).trim();
  
  // Split by semicolons to get individual members
  const members = content.split(";").filter(m => m.trim());
  
  for (const member of members) {
    const trimmedMember = member.trim();
    if (!trimmedMember) continue;
    
    const tokens: ReviewToken[] = [];
    
    // Tokenize the member
    const parts = tokenizeText(trimmedMember + ";");
    for (const part of parts) {
      if (!part.trim()) continue;
      
      const hasPrefixSpace = needsLeadingSpace(part) || needsSpaceBefore(part);
      const hasSuffixSpace = needsTrailingSpace(part);
      
      tokens.push(
        createToken(TokenKind.Text, part, {
          hasPrefixSpace,
          hasSuffixSpace,
          deprecated,
        }),
      );
    }
    
    lines.push({ Tokens: tokens });
  }
  
  // Add closing brace line
  lines.push({
    Tokens: [createToken(TokenKind.Punctuation, "}", { deprecated })],
    IsContextEndLine: true,
  });
  
  return lines;
}

/** Process excerpt tokens and add them to the tokens array */
export function processExcerptTokens(
  excerptTokens: readonly ExcerptToken[],
  tokens: ReviewToken[],
  deprecated?: boolean,
): void {
  for (const excerpt of excerptTokens) {
    const text = excerpt.text;
    if (!text || !text.trim()) continue;

    // Normalize whitespace - replace newlines and multiple spaces with single space
    const normalizedText = text.replace(/\s+/g, ' ').trim();
    if (!normalizedText) continue;

    // Split into individual tokens for proper spacing
    const parts = tokenizeText(normalizedText);
    
    for (const part of parts) {
      if (!part.trim()) continue;
      
      const hasPrefixSpace = needsLeadingSpace(part) || needsSpaceBefore(part);
      const hasSuffixSpace = needsTrailingSpace(part);

      if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
        tokens.push(
          createToken(TokenKind.TypeName, part, {
            navigateToId: excerpt.canonicalReference.toString(),
            hasPrefixSpace,
            hasSuffixSpace,
            deprecated,
          }),
        );
      } else {
        tokens.push(
          createToken(TokenKind.Text, part, {
            hasPrefixSpace,
            hasSuffixSpace,
            deprecated,
          }),
        );
      }
    }
  }
}

/**
 * Tokenize text into individual parts, preserving operators and punctuation
 */
export function tokenizeText(text: string): string[] {
  const result: string[] = [];
  let current = "";
  
  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    
    if (char === "{" || char === "}" || char === ";" || char === ":") {
      if (current.trim()) {
        result.push(current.trim());
      }
      result.push(char);
      current = "";
    } else if (char === " ") {
      if (current.trim()) {
        result.push(current.trim());
      }
      current = "";
    } else {
      current += char;
    }
  }
  
  if (current.trim()) {
    result.push(current.trim());
  }
  
  return result;
}
