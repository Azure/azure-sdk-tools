import { ApiIndexSignature, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

function isValid(item: ApiItem): item is ApiIndexSignature {
  return item.kind === ApiItemKind.IndexSignature;
}

function generate(item: ApiIndexSignature, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];

  if (item.kind !== ApiItemKind.IndexSignature) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for IndexSignature token generator.`,
    );
  }

  const parameters = item.parameters;

  if (item.isReadonly) {
    tokens.push(createToken(TokenKind.Keyword, "readonly", { hasSuffixSpace: true, deprecated }));
  }

  tokens.push(createToken(TokenKind.Punctuation, "[", { deprecated }));

  if (parameters?.length > 0) {
    parameters.forEach((param, index) => {
      tokens.push(createToken(TokenKind.Text, param.name, { deprecated }));
      tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      processExcerptTokens(param.parameterTypeExcerpt.spannedTokens, tokens, deprecated);

      if (index < parameters.length - 1) {
        tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
  }

  tokens.push(createToken(TokenKind.Punctuation, "]", { deprecated }));
  tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));

  processExcerptTokens(item.returnTypeExcerpt.spannedTokens, tokens, deprecated);

  tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  return { tokens };
}

export const indexSignatureTokenGenerator: TokenGenerator<ApiIndexSignature> = {
  isValid,
  generate,
};
