import { ApiFunction, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, TokenCollector } from "./helpers";

function isValid(item: ApiItem): item is ApiFunction {
  return item.kind === ApiItemKind.Function;
}

function generate(item: ApiFunction, deprecated?: boolean): GeneratorResult {
  const collector = new TokenCollector();
  if (item.kind !== ApiItemKind.Function) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Function token generator.`,
    );
  }

  // Extract structured properties
  const parameters = item.parameters;
  const typeParameters = item.typeParameters;

  // Add export and function keywords
  collector.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));

  // Check for default export
  const isDefaultExport = item.excerptTokens.some((t) => t.text.includes("export default"));
  if (isDefaultExport) {
    collector.push(createToken(TokenKind.Keyword, "default", { hasSuffixSpace: true, deprecated }));
  }

  collector.push(createToken(TokenKind.Keyword, "function", { hasSuffixSpace: true, deprecated }));
  collector.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));

  // Add type parameters
  if (typeParameters?.length > 0) {
    collector.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
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
        collector.push(
          createToken(TokenKind.Text, tp.constraintExcerpt.text.trim(), { deprecated }),
        );
      }

      if (tp.defaultTypeExcerpt?.text.trim()) {
        collector.push(
          createToken(TokenKind.Punctuation, "=", {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        processExcerptTokens(tp.defaultTypeExcerpt.spannedTokens, collector, deprecated);
      }

      if (index < typeParameters.length - 1) {
        collector.push(
          createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
        );
      }
    });
    collector.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
  }

  // Add parameters
  collector.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
  if (parameters?.length > 0) {
    parameters.forEach((param, index) => {
      collector.push(
        createToken(TokenKind.Text, param.name, {
          hasPrefixSpace: index > 0,
          deprecated,
        }),
      );

      if (param.isOptional) {
        collector.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
      }

      collector.push(
        createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }),
      );
      processExcerptTokens(param.parameterTypeExcerpt.spannedTokens, collector, deprecated);

      if (index < parameters.length - 1) {
        collector.push(
          createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
        );
      }
    });
  }

  // Add return type
  collector.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
  collector.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
  processExcerptTokens(item.returnTypeExcerpt.spannedTokens, collector, deprecated);

  collector.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  return collector.toResult();
}

export const functionTokenGenerator: TokenGenerator<ApiFunction> = {
  isValid,
  generate,
};
