import { ApiItem, ApiItemKind, ApiProperty, ApiPropertyItem } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

function isValid(item: ApiItem): item is ApiPropertyItem {
  return item.kind === ApiItemKind.Property || item.kind === ApiItemKind.PropertySignature;
}

function generate(item: ApiPropertyItem, deprecated?: boolean): ReviewToken[] {
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

  // Use spannedTokens if available, otherwise fall back to text
  if (item.propertyTypeExcerpt.spannedTokens?.length) {
    processExcerptTokens(item.propertyTypeExcerpt.spannedTokens, tokens, deprecated);
    formatObjectTypeTokens(tokens);
  } else if (item.propertyTypeExcerpt.text) {
    tokens.push(createToken(TokenKind.TypeName, item.propertyTypeExcerpt.text.trim(), { deprecated }));
  }

  return tokens;
}

function formatObjectTypeTokens(tokens: ReviewToken[]): void {
  const indent = "    "; // 4 spaces
  let indentLevel = 0;

  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i];
    const value = token.Value?.trim();

    if (value === "{") {
      indentLevel++;
      token.HasSuffixSpace = false;
      // Add newline after { by inserting indent text token
      if (i + 1 < tokens.length) {
        const wsToken = createToken(TokenKind.Text, "\n" + indent.repeat(indentLevel));
        tokens.splice(i + 1, 0, wsToken);
        i += 1;
      }
    } else if (value === ";") {
      token.HasSuffixSpace = false;
      // Check if next meaningful token is }
      let nextIndex = i + 1;
      while (nextIndex < tokens.length && tokens[nextIndex].Value?.trim() === "") {
        nextIndex++;
      }
      const nextIndent = (nextIndex < tokens.length && tokens[nextIndex].Value?.trim() === "}") 
        ? indentLevel - 1 
        : indentLevel;
      
      if (indentLevel > 0) {
        const wsToken = createToken(TokenKind.Text, "\n" + indent.repeat(Math.max(0, nextIndent)));
        tokens.splice(i + 1, 0, wsToken);
        i += 1;
      }
    } else if (value === "}") {
      indentLevel = Math.max(0, indentLevel - 1);
    }
  }
}

export const propertyTokenGenerator: TokenGenerator<ApiPropertyItem> = {
  isValid,
  generate,
};
