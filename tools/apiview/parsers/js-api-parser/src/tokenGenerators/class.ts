import { ApiClass, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, TokenCollector } from "./helpers";

function isValid(item: ApiItem): item is ApiClass {
  return item.kind === ApiItemKind.Class;
}

function generate(item: ApiClass, deprecated?: boolean): GeneratorResult {
  const collector = new TokenCollector();
  if (item.kind !== ApiItemKind.Class) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Class token generator.`,
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

  // Add abstract modifier if applicable
  if (item.isAbstract) {
    collector.push(
      createToken(TokenKind.Keyword, "abstract", { hasSuffixSpace: true, deprecated }),
    );
  }

  collector.push(createToken(TokenKind.Keyword, "class", { hasSuffixSpace: true, deprecated }));

  // Create class name token with proper metadata
  const nameToken = createToken(TokenKind.TypeName, item.displayName, { deprecated });
  nameToken.NavigateToId = item.canonicalReference.toString();
  nameToken.NavigationDisplayName = item.displayName;
  nameToken.RenderClasses = ["class"];
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

  // Add extends clause if class extends another class
  if (item.extendsType) {
    collector.push(
      createToken(TokenKind.Keyword, "extends", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );
    processExcerptTokens(item.extendsType.excerpt.spannedTokens, collector, deprecated);
  }

  // Add implements clause if class implements interfaces
  if (item.implementsTypes && item.implementsTypes.length > 0) {
    collector.push(
      createToken(TokenKind.Keyword, "implements", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    item.implementsTypes.forEach((implementsType, index) => {
      processExcerptTokens(implementsType.excerpt.spannedTokens, collector, deprecated);

      if (index < item.implementsTypes.length - 1) {
        collector.push(createToken(TokenKind.Text, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
  }

  return collector.toResult();
}

export const classTokenGenerator: TokenGenerator<ApiClass> = {
  isValid,
  generate,
};
