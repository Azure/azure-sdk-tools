import { ApiConstructor, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

function isValid(item: ApiItem): item is ApiConstructor {
  return item.kind === ApiItemKind.Constructor;
}

function generate(item: ApiConstructor, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];

  if (item.kind !== ApiItemKind.Constructor) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Constructor token generator.`,
    );
  }

  const parameters = item.parameters;

  if (item.isProtected) {
    tokens.push(
      createToken(TokenKind.Keyword, "protected", {
        hasSuffixSpace: true,
        deprecated,
      }),
    );
  }

  tokens.push(createToken(TokenKind.Keyword, "constructor", { deprecated }));
  tokens.push(createToken(TokenKind.Text, "(", { deprecated }));

  if (parameters?.length > 0) {
    parameters.forEach((param, index) => {
      tokens.push(
        createToken(TokenKind.Text, param.name, {
          hasPrefixSpace: index > 0,
          deprecated,
        }),
      );

      if (param.isOptional) {
        tokens.push(createToken(TokenKind.Text, "?", { deprecated }));
      }

      tokens.push(createToken(TokenKind.Text, ":", { hasSuffixSpace: true, deprecated }));
      processExcerptTokens(param.parameterTypeExcerpt.spannedTokens, tokens, deprecated);

      if (index < parameters.length - 1) {
        tokens.push(createToken(TokenKind.Text, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
  }

  tokens.push(createToken(TokenKind.Text, ")", { deprecated }));
  tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  return { tokens };
}

export const constructorTokenGenerator: TokenGenerator<ApiConstructor> = {
  isValid,
  generate,
};