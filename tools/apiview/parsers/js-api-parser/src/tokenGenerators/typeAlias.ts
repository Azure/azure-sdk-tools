import { ApiTypeAlias, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewLine, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, parseTypeText, buildReferenceMap, TokenCollector } from "./helpers";

function isValid(item: ApiItem): item is ApiTypeAlias {
  return item.kind === ApiItemKind.TypeAlias;
}

function generate(item: ApiTypeAlias, deprecated?: boolean): GeneratorResult {
  const collector = new TokenCollector();

  if (item.kind !== ApiItemKind.TypeAlias) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for TypeAlias token generator.`,
    );
  }

  // Extract structured properties
  const typeParameters = item.typeParameters;

  // Add export keyword
  collector.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));

  // Check for default export
  const isDefaultExport = item.excerptTokens.some((t) => t.text.includes("export default"));
  if (isDefaultExport) {
    collector.push(createToken(TokenKind.Keyword, "default", { hasSuffixSpace: true, deprecated }));
  }

  // Add type keyword
  collector.push(createToken(TokenKind.Keyword, "type", { hasSuffixSpace: true, deprecated }));

  // Add type name with navigation metadata
  const nameToken = createToken(TokenKind.TypeName, item.displayName, { deprecated });
  nameToken.NavigateToId = item.canonicalReference.toString();
  nameToken.NavigationDisplayName = item.displayName;
  nameToken.RenderClasses = ["type"];
  collector.push(nameToken);

  // Add type parameters (e.g., <T, U extends SomeType>)
  if (typeParameters?.length > 0) {
    collector.push(createToken(TokenKind.Text, "<", { deprecated }));
    typeParameters.forEach((tp, index) => {
      collector.push(createToken(TokenKind.TypeName, tp.name, { deprecated }));

      // Handle constraint (extends clause)
      if (tp.constraintExcerpt?.text.trim()) {
        collector.push(
          createToken(TokenKind.Keyword, "extends", {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        processExcerptTokens(tp.constraintExcerpt.spannedTokens, collector, deprecated);
      }

      // Handle default type
      if (tp.defaultTypeExcerpt?.text.trim()) {
        collector.push(
          createToken(TokenKind.Text, "=", {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        processExcerptTokens(tp.defaultTypeExcerpt.spannedTokens, collector, deprecated);
      }

      if (index < typeParameters.length - 1) {
        collector.push(createToken(TokenKind.Text, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
    collector.push(createToken(TokenKind.Text, ">", { deprecated }));
  }

  // Add equals sign (for type alias assignment - remains Punctuation)
  collector.push(
    createToken(TokenKind.Punctuation, "=", {
      hasPrefixSpace: true,
      hasSuffixSpace: true,
      deprecated,
    }),
  );

  // Process the type definition
  // Use parseTypeText with a reference map for both type structure and navigation
  const typeText = item.typeExcerpt?.text?.trim();
  let typeChildren: ReviewLine[] | undefined;
  if (typeText) {
    // Build a reference map from spanned tokens for navigation
    const referenceMap = item.typeExcerpt.spannedTokens
      ? buildReferenceMap(item.typeExcerpt.spannedTokens)
      : undefined;
    typeChildren = parseTypeText(typeText, collector.currentTarget, deprecated, 0, referenceMap);
    if (!typeChildren?.length) {
      collector.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    }
  } else {
    // Fallback: process excerpt tokens directly
    processExcerptTokens(item.typeExcerpt.spannedTokens, collector, deprecated);
    collector.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
  }

  return collector.toResult(typeChildren);
}

export const typeAliasTokenGenerator: TokenGenerator<ApiTypeAlias> = {
  isValid,
  generate,
};
