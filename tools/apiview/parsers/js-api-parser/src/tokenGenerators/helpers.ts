import { ExcerptToken, ExcerptTokenKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";

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
  return value === "|" || value === "&" || value === "is" || value === "extends";
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

    // Split by newlines to preserve line structure
    const lines = text.split(/(\r?\n)/);

    for (const segment of lines) {
      // Handle newline - add as separate token or skip if empty
      if (segment === '\n' || segment === '\r\n') {
        continue; // Newlines are handled by the rendering layer
      }

      const trimmedText = segment.trim();
      if (!trimmedText) continue;

      const hasPrefixSpace = segment.startsWith(" ") || segment.startsWith("\t") || needsLeadingSpace(trimmedText);
      const hasSuffixSpace = segment.endsWith(" ") || segment.endsWith("\t") || needsTrailingSpace(trimmedText);

      if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
        tokens.push(
          createToken(TokenKind.TypeName, trimmedText, {
            navigateToId: excerpt.canonicalReference.toString(),
            hasPrefixSpace,
            hasSuffixSpace,
            deprecated,
          }),
        );
      } else {
        tokens.push(
          createToken(TokenKind.Text, trimmedText, {
            hasPrefixSpace,
            hasSuffixSpace,
            deprecated,
          }),
        );
      }
    }
  }
}
