import {
  ApiCallSignature,
  ApiConstructSignature,
  ApiItem,
  ApiItemKind,
} from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

type SignatureLike = ApiCallSignature | ApiConstructSignature;

function isValid(item: ApiItem): item is SignatureLike {
  return item.kind === ApiItemKind.CallSignature || item.kind === ApiItemKind.ConstructSignature;
}

function generate(item: SignatureLike, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];

  if (item.kind !== ApiItemKind.CallSignature && item.kind !== ApiItemKind.ConstructSignature) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Signature token generator.`,
    );
  }

  const parameters = item.parameters;
  const typeParameters = item.typeParameters;

  // Add new keyword for construct signatures
  if (item.kind === ApiItemKind.ConstructSignature) {
    tokens.push(createToken(TokenKind.Keyword, "new", { deprecated }));
  }

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
        processExcerptTokens(tp.constraintExcerpt.spannedTokens, tokens, deprecated);
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

export const callableSignatureTokenGenerator: TokenGenerator<SignatureLike> = {
  isValid,
  generate,
};
