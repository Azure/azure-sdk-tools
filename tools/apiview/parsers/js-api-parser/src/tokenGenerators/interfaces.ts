import { ApiInterface, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

function isValid(item: ApiItem): item is ApiInterface {
  return item.kind === ApiItemKind.Interface;
}

function generate(item: ApiInterface, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];
  if (item.kind !== ApiItemKind.Interface) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Interface token generator.`,
    );
  }

  // Extract structured properties
  const typeParameters = item.typeParameters;

  // Add export and interface keywords
  tokens.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));

  // Check for default export
  const isDefaultExport = item.excerptTokens.some((t) => t.text.includes("export default"));
  if (isDefaultExport) {
    tokens.push(createToken(TokenKind.Keyword, "default", { hasSuffixSpace: true, deprecated }));
  }

  tokens.push(createToken(TokenKind.Keyword, "interface", { hasSuffixSpace: true, deprecated }));

  // Create interface name token with proper metadata (matching splitAndBuild behavior)
  const nameToken = createToken(TokenKind.TypeName, item.displayName, { deprecated });
  nameToken.NavigateToId = item.canonicalReference.toString();
  nameToken.NavigationDisplayName = item.displayName;
  nameToken.RenderClasses = ["interface"];
  tokens.push(nameToken);

  // Add type parameters
  if (typeParameters?.length > 0) {
    tokens.push(createToken(TokenKind.Text, "<", { deprecated }));
    typeParameters.forEach((tp, index) => {
      tokens.push(createToken(TokenKind.TypeName, tp.name, { deprecated }));

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

  // Add extends clause if interface extends other interfaces
  if (item.extendsTypes && item.extendsTypes.length > 0) {
    tokens.push(
      createToken(TokenKind.Keyword, "extends", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    item.extendsTypes.forEach((extendsType, index) => {
      processExcerptTokens(extendsType.excerpt.spannedTokens, tokens, deprecated);

      if (index < item.extendsTypes.length - 1) {
        tokens.push(createToken(TokenKind.Text, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
  }

  return { tokens };
}

export const interfaceTokenGenerator: TokenGenerator<ApiInterface> = {
  isValid,
  generate,
};
