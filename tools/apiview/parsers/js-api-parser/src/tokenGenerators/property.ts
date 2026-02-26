import {
  ApiDeclaredItem,
  ApiItem,
  ApiItemKind,
  ApiProperty,
  ApiPropertyItem,
  ExcerptToken,
} from "@microsoft/api-extractor-model";
import { ReviewLine, ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, parseTypeText, buildReferenceMap } from "./helpers";

function isValid(item: ApiItem): item is ApiPropertyItem {
  return item.kind === ApiItemKind.Property || item.kind === ApiItemKind.PropertySignature;
}

/**
 * Processes the property type using parseTypeText with a reference map for navigation.
 * This handles both simple types (with navigation links) and inline type literals (with children structure).
 */
function processPropertyType(
  typeText: string,
  spannedTokens: readonly ExcerptToken[] | undefined,
  tokens: ReviewToken[],
  deprecated?: boolean,
): ReviewLine[] | undefined {
  // Build a reference map from spanned tokens for navigation
  const referenceMap = spannedTokens ? buildReferenceMap(spannedTokens) : undefined;
  // Use parseTypeText which now handles both type structure and navigation via the reference map
  return parseTypeText(typeText, tokens, deprecated, 0, referenceMap);
}

/**
 * Checks if the property is actually a getter accessor
 */
function isGetter(item: ApiPropertyItem): boolean {
  if ("excerptTokens" in item) {
    const excerptTokens = (item as ApiDeclaredItem).excerptTokens;
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

/**
 * Checks if the property is actually a setter accessor
 */
function isSetter(item: ApiPropertyItem): boolean {
  if ("excerptTokens" in item) {
    const excerptTokens = (item as ApiDeclaredItem).excerptTokens;
    if (excerptTokens?.length > 0) {
      const firstTokenText = excerptTokens[0]?.text;
      if (firstTokenText) {
        const trimmed = firstTokenText.trimStart();
        // The setter keyword appears at the start of the excerpt, e.g., "set keyID(value: "
        if (trimmed.startsWith("set ")) {
          return true;
        }
      }
    }
  }
  return false;
}

/**
 * Extracts setter parameter info from excerpt tokens
 * Returns the parameter name and type text
 */
function getSetterParameterInfo(item: ApiPropertyItem): { paramName: string; paramType: string } {
  if ("excerptTokens" in item) {
    const excerptTokens = (item as ApiDeclaredItem).excerptTokens;
    // Join all excerpt token texts to get the full signature
    const fullText = excerptTokens.map((t) => t.text).join("");
    // Pattern: set name(paramName: ParamType)
    const match = fullText.match(/set\s+\w+\s*\(\s*(\w+)\s*:\s*(.+?)\s*\)/);
    if (match) {
      return { paramName: match[1], paramType: match[2].trim() };
    }
  }
  return { paramName: "value", paramType: item.propertyTypeExcerpt.text || "unknown" };
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
      children = processPropertyType(
        typeText,
        item.propertyTypeExcerpt.spannedTokens,
        tokens,
        deprecated,
      );
      if (!children) {
        tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      }
    } else {
      tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    }

    return { tokens, children };
  }

  // Check if this is a setter
  if (isSetter(item)) {
    const { paramName, paramType } = getSetterParameterInfo(item);

    tokens.push(createToken(TokenKind.Keyword, "set", { hasSuffixSpace: true, deprecated }));
    tokens.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));
    tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
    tokens.push(createToken(TokenKind.MemberName, paramName, { deprecated }));
    tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));

    let children;
    if (paramType) {
      // For setters, use the spannedTokens from propertyTypeExcerpt for navigation
      children = processPropertyType(
        paramType,
        item.propertyTypeExcerpt.spannedTokens,
        tokens,
        deprecated,
      );
      if (!children) {
        tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
        tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      } else {
        // If there are children (inline type), we need to close the parenthesis on the end line
        // For now, add closing paren after the type
        tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
      }
    } else {
      tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
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
    children = processPropertyType(
      typeText,
      item.propertyTypeExcerpt.spannedTokens,
      tokens,
      deprecated,
    );
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
