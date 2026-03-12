import { ApiIndexSignature, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, TokenCollector } from "./helpers";

function isValid(item: ApiItem): item is ApiIndexSignature {
  return item.kind === ApiItemKind.IndexSignature;
}

function generate(item: ApiIndexSignature, deprecated?: boolean): GeneratorResult {
  const collector = new TokenCollector();

  if (item.kind !== ApiItemKind.IndexSignature) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for IndexSignature token generator.`,
    );
  }

  const parameters = item.parameters;

  if (item.isReadonly) {
    collector.push(
      createToken(TokenKind.Keyword, "readonly", { hasSuffixSpace: true, deprecated }),
    );
  }

  collector.push(createToken(TokenKind.Punctuation, "[", { deprecated }));

  if (parameters?.length > 0) {
    parameters.forEach((param, index) => {
      collector.push(createToken(TokenKind.Text, param.name, { deprecated }));
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

  collector.push(createToken(TokenKind.Punctuation, "]", { deprecated }));
  collector.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));

  processExcerptTokens(item.returnTypeExcerpt.spannedTokens, collector, deprecated);

  collector.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  return collector.toResult();
}

export const indexSignatureTokenGenerator: TokenGenerator<ApiIndexSignature> = {
  isValid,
  generate,
};
