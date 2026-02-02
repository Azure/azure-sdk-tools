import { ApiItem, ApiItemKind, ApiProperty, ApiPropertyItem } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, parseTypeText } from "./helpers";

function isValid(item: ApiItem): item is ApiPropertyItem {
  return item.kind === ApiItemKind.Property || item.kind === ApiItemKind.PropertySignature;
}

/**
 * Checks if the property is actually a getter accessor
 */
function isGetter(item: ApiPropertyItem): boolean {
  if ("excerptTokens" in item) {
    const excerptTokens = (item as any).excerptTokens;
    if (excerptTokens?.length > 0) {
      const firstTokenText = excerptTokens[0]?.text;
      if (firstTokenText) {
        const trimmed = firstTokenText.trimStart();
        // The getter keyword appears at the start of the excerpt, e.g., "get keyID(): "
        if (trimmed.startsWith("get ")) {
          return true;
        }
      }
    }
  }
  return false;
}

function generate(item: ApiPropertyItem, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];

  if (item.kind !== ApiItemKind.Property && item.kind !== ApiItemKind.PropertySignature) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Property token generator.`,
    );
  }

  // Check if this is a getter
  if (isGetter(item)) {
    tokens.push(createToken(TokenKind.Keyword, "get", { hasSuffixSpace: true, deprecated }));
    tokens.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));
    tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
    tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
    tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));

    const typeText = item.propertyTypeExcerpt.text;
    let children;

    if (typeText) {
      children = parseTypeText(typeText, tokens, deprecated);
      if (!children) {
        tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      }
    } else {
      tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    }

    return { tokens, children };
  }

  // Regular property handling
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

  const typeText = item.propertyTypeExcerpt.text;
  let children;

  if (typeText) {
    children = parseTypeText(typeText, tokens, deprecated);
    if (!children) {
      tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    }
  } else {
    tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
  }

  return { tokens, children };
}

export const propertyTokenGenerator: TokenGenerator<ApiPropertyItem> = {
  isValid,
  generate,
};
