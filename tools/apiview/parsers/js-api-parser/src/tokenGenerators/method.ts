import {
  ApiItem,
  ApiItemKind,
  ApiMethod,
  ApiMethodSignature,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, TokenCollector } from "./helpers";

type MethodLike = ApiMethod | ApiMethodSignature;

function isValid(item: ApiItem): item is MethodLike {
  return item.kind === ApiItemKind.Method || item.kind === ApiItemKind.MethodSignature;
}

function generate(item: MethodLike, deprecated?: boolean): GeneratorResult {
  const collector = new TokenCollector();

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
      collector.push(
        createToken(TokenKind.Keyword, "static", { hasSuffixSpace: true, deprecated }),
      );
    }

    // Add protected modifier if applicable
    if (method.isProtected) {
      collector.push(
        createToken(TokenKind.Keyword, "protected", { hasSuffixSpace: true, deprecated }),
      );
    }

    // Add abstract modifier if applicable
    if (method.isAbstract) {
      collector.push(
        createToken(TokenKind.Keyword, "abstract", { hasSuffixSpace: true, deprecated }),
      );
    }
  }

  // Add method name
  collector.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));

  // Add optional marker if applicable
  if (item.isOptional) {
    collector.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
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

  // Add semicolon at the end
  collector.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  return collector.toResult();
}

export const methodTokenGenerator: TokenGenerator<MethodLike> = {
  isValid,
  generate,
};
