import {
  ApiItem,
  ApiItemKind,
  ApiMethod,
  ApiMethodSignature,
} from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

type MethodLike = ApiMethod | ApiMethodSignature;

function isValid(item: ApiItem): item is MethodLike {
  return item.kind === ApiItemKind.Method || item.kind === ApiItemKind.MethodSignature;
}

function generate(item: MethodLike, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];

  if (item.kind !== ApiItemKind.Method && item.kind !== ApiItemKind.MethodSignature) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Method token generator.`,
    );
  }

  // Extract structured properties
  const parameters = item.parameters;
  const typeParameters = item.typeParameters;

  // Handle modifiers for class methods (ApiMethod)
  if (item.kind === ApiItemKind.Method) {
    const method = item as ApiMethod;

    // Add static modifier if applicable
    if (method.isStatic) {
      tokens.push(createToken(TokenKind.Keyword, "static", { hasSuffixSpace: true, deprecated }));
    }

    // Add protected modifier if applicable
    if (method.isProtected) {
      tokens.push(
        createToken(TokenKind.Keyword, "protected", { hasSuffixSpace: true, deprecated }),
      );
    }

    // Add abstract modifier if applicable
    if (method.isAbstract) {
      tokens.push(createToken(TokenKind.Keyword, "abstract", { hasSuffixSpace: true, deprecated }));
    }
  }

  // Add method name
  tokens.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));

  // Add optional marker if applicable
  if (item.isOptional) {
    tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
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

  // Add semicolon at the end
  tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  return { tokens };
}

export const methodTokenGenerator: TokenGenerator<MethodLike> = {
  isValid,
  generate,
};
