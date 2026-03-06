import { ApiTypeAlias, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, ReviewLine, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, parseTypeText, buildReferenceMap } from "./helpers";

function isValid(item: ApiItem): item is ApiTypeAlias {
  return item.kind === ApiItemKind.TypeAlias;
}

function generate(item: ApiTypeAlias, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];
  const children: ReviewLine[] = [];

  if (item.kind !== ApiItemKind.TypeAlias) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for TypeAlias token generator.`,
    );
  }

  // Extract structured properties
  const typeParameters = item.typeParameters;

  // Add export keyword
  tokens.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));

  // Check for default export
  const isDefaultExport = item.excerptTokens.some((t) => t.text.includes("export default"));
  if (isDefaultExport) {
    tokens.push(createToken(TokenKind.Keyword, "default", { hasSuffixSpace: true, deprecated }));
  }

  // Add type keyword
  tokens.push(createToken(TokenKind.Keyword, "type", { hasSuffixSpace: true, deprecated }));

  // Add type name with navigation metadata
  const nameToken = createToken(TokenKind.TypeName, item.displayName, { deprecated });
  nameToken.NavigateToId = item.canonicalReference.toString();
  nameToken.NavigationDisplayName = item.displayName;
  nameToken.RenderClasses = ["type"];
  tokens.push(nameToken);

  // Add type parameters (e.g., <T, U extends SomeType>)
  if (typeParameters?.length > 0) {
    tokens.push(createToken(TokenKind.Text, "<", { deprecated }));
    typeParameters.forEach((tp, index) => {
      tokens.push(createToken(TokenKind.TypeName, tp.name, { deprecated }));

      // Handle constraint (extends clause)
      if (tp.constraintExcerpt?.text.trim()) {
        tokens.push(
          createToken(TokenKind.Keyword, "extends", {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        processExcerptTokens(tp.constraintExcerpt.spannedTokens, tokens, deprecated);
      }

      // Handle default type
      if (tp.defaultTypeExcerpt?.text.trim()) {
        tokens.push(
          createToken(TokenKind.Text, "=", {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        processExcerptTokens(tp.defaultTypeExcerpt.spannedTokens, tokens, deprecated);
      }

      if (index < typeParameters.length - 1) {
        tokens.push(createToken(TokenKind.Text, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
    tokens.push(createToken(TokenKind.Text, ">", { deprecated }));
  }

  // Add equals sign (for type alias assignment - remains Punctuation)
  tokens.push(
    createToken(TokenKind.Punctuation, "=", {
      hasPrefixSpace: true,
      hasSuffixSpace: true,
      deprecated,
    }),
  );

  // Process the type definition
  // Use parseTypeText with a reference map for both type structure and navigation
  const typeText = item.typeExcerpt?.text?.trim();
  if (typeText) {
    // Build a reference map from spanned tokens for navigation
    const referenceMap = item.typeExcerpt.spannedTokens
      ? buildReferenceMap(item.typeExcerpt.spannedTokens)
      : undefined;
    const typeChildren = parseTypeText(typeText, tokens, deprecated, 0, referenceMap);
    if (typeChildren?.length) {
      children.push(...typeChildren);
    } else {
      tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    }
  } else {
    // Fallback: process excerpt tokens directly
    processExcerptTokens(item.typeExcerpt.spannedTokens, tokens, deprecated);
    tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
  }

  return { tokens, children: children.length > 0 ? children : undefined };
}

export const typeAliasTokenGenerator: TokenGenerator<ApiTypeAlias> = {
  isValid,
  generate,
};
