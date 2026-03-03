import { ApiFunction, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

function isValid(item: ApiItem): item is ApiFunction {
  return item.kind === ApiItemKind.Function;
}

function generate(item: ApiFunction, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];
  if (item.kind !== ApiItemKind.Function) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Function token generator.`,
    );
  }

  // Extract structured properties
  const parameters = item.parameters;
  const typeParameters = item.typeParameters;

  // Add export and function keywords
  tokens.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));

  // Check for default export
  const isDefaultExport = item.excerptTokens.some((t) => t.text.includes("export default"));
  if (isDefaultExport) {
    tokens.push(createToken(TokenKind.Keyword, "default", { hasSuffixSpace: true, deprecated }));
  }

  tokens.push(createToken(TokenKind.Keyword, "function", { hasSuffixSpace: true, deprecated }));
  tokens.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));

  // Add type parameters
  if (typeParameters?.length > 0) {
    tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
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
        tokens.push(createToken(TokenKind.Text, tp.constraintExcerpt.text.trim(), { deprecated }));
      }

      if (tp.defaultTypeExcerpt?.text.trim()) {
        tokens.push(
          createToken(TokenKind.Punctuation, "=", {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        processExcerptTokens(tp.defaultTypeExcerpt.spannedTokens, tokens, deprecated);
      }

      if (index < typeParameters.length - 1) {
        tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
    tokens.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
  }

  // Add parameters
  tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
  if (parameters?.length > 0) {
    parameters.forEach((param, index) => {
      tokens.push(
        createToken(TokenKind.Text, param.name, {
          hasPrefixSpace: index > 0,
          deprecated,
        }),
      );

      if (param.isOptional) {
        tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
      }

      tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      processExcerptTokens(param.parameterTypeExcerpt.spannedTokens, tokens, deprecated);

      if (index < parameters.length - 1) {
        tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
  }

  // Add return type
  tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
  tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
  processExcerptTokens(item.returnTypeExcerpt.spannedTokens, tokens, deprecated);

  tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  return { tokens };
}

export const functionTokenGenerator: TokenGenerator<ApiFunction> = {
  isValid,
  generate,
};
