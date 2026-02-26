import { ApiInterface, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, TokenCollector } from "./helpers";

function isValid(item: ApiItem): item is ApiInterface {
  return item.kind === ApiItemKind.Interface;
}

function generate(item: ApiInterface, deprecated?: boolean): GeneratorResult {
  const collector = new TokenCollector();
  if (item.kind !== ApiItemKind.Interface) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Interface token generator.`,
    );
  }

  // Extract structured properties
  const typeParameters = item.typeParameters;

  // Add export and interface keywords
  collector.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));

  // Check for default export
  const isDefaultExport = item.excerptTokens.some((t) => t.text.includes("export default"));
  if (isDefaultExport) {
    collector.push(createToken(TokenKind.Keyword, "default", { hasSuffixSpace: true, deprecated }));
  }

  collector.push(
    createToken(TokenKind.Keyword, "interface", { hasSuffixSpace: true, deprecated }),
  );

  // Create interface name token with proper metadata (matching splitAndBuild behavior)
  const nameToken = createToken(TokenKind.TypeName, item.displayName, { deprecated });
  nameToken.NavigateToId = item.canonicalReference.toString();
  nameToken.NavigationDisplayName = item.displayName;
  nameToken.RenderClasses = ["interface"];
  collector.push(nameToken);

  // Add type parameters
  if (typeParameters?.length > 0) {
    collector.push(createToken(TokenKind.Text, "<", { deprecated }));
    typeParameters.forEach((tp, index) => {
      collector.push(createToken(TokenKind.TypeName, tp.name, { deprecated }));

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

  // Add extends clause if interface extends other interfaces
  if (item.extendsTypes && item.extendsTypes.length > 0) {
    collector.push(
      createToken(TokenKind.Keyword, "extends", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    item.extendsTypes.forEach((extendsType, index) => {
      processExcerptTokens(extendsType.excerpt.spannedTokens, collector, deprecated);

      if (index < item.extendsTypes.length - 1) {
        collector.push(createToken(TokenKind.Text, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
  }

  return collector.toResult();
}

export const interfaceTokenGenerator: TokenGenerator<ApiInterface> = {
  isValid,
  generate,
};
