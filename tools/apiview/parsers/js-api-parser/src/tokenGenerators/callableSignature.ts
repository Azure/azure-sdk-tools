import {
  ApiCallSignature,
  ApiConstructSignature,
  ApiItem,
  ApiItemKind,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, TokenCollector } from "./helpers";

type SignatureLike = ApiCallSignature | ApiConstructSignature;

function isValid(item: ApiItem): item is SignatureLike {
  return item.kind === ApiItemKind.CallSignature || item.kind === ApiItemKind.ConstructSignature;
}

function generate(item: SignatureLike, deprecated?: boolean): GeneratorResult {
  const collector = new TokenCollector();

  if (item.kind !== ApiItemKind.CallSignature && item.kind !== ApiItemKind.ConstructSignature) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Signature token generator.`,
    );
  }

  const parameters = item.parameters;
  const typeParameters = item.typeParameters;

  // Add new keyword for construct signatures
  if (item.kind === ApiItemKind.ConstructSignature) {
    collector.push(createToken(TokenKind.Keyword, "new", { deprecated }));
  }

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
        processExcerptTokens(tp.constraintExcerpt.spannedTokens, collector, deprecated);
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

  // Filter out destructured parameters (names starting with "{") - API Extractor reports
  // destructured parameters in raw form followed by a synthetic normalized parameter with
  // the actual type info. We skip the raw destructuring pattern and only emit the synthetic one.
  const filteredParameters = parameters?.filter((param) => !param.name.startsWith("{")) ?? [];

  // Add parameters
  collector.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
  if (filteredParameters.length > 0) {
    filteredParameters.forEach((param, index) => {
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

      if (index < filteredParameters.length - 1) {
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

export const callableSignatureTokenGenerator: TokenGenerator<SignatureLike> = {
  isValid,
  generate,
};
