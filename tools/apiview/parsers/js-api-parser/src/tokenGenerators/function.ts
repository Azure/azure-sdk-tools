import {
  ApiFunction,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
  Parameter,
  TypeParameter,
} from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator } from "./index";

function isValid(item: ApiItem): item is ApiFunction {
  return item.kind === ApiItemKind.Function;
}

/** Helper to create a token with common properties */
function createToken(
  kind: TokenKind,
  value: string,
  options?: {
    hasSuffixSpace?: boolean;
    hasPrefixSpace?: boolean;
    navigateToId?: string;
    deprecated?: boolean;
  },
): ReviewToken {
  return {
    Kind: kind,
    Value: value,
    HasSuffixSpace: options?.hasSuffixSpace ?? false,
    HasPrefixSpace: options?.hasPrefixSpace ?? false,
    NavigateToId: options?.navigateToId,
    IsDeprecated: options?.deprecated,
  };
}

/**
 * Determines if a token needs a leading space based on its value
 * @param value The token value
 * @returns true if the token needs a leading space
 */
function needsLeadingSpace(value: string): boolean {
  return value === "|" || value === "&" || value === "is";
}

/**
 * Determines if a token needs a trailing space based on its value
 * @param value The token value
 * @returns true if the token needs a trailing space
 */
function needsTrailingSpace(value: string): boolean {
  return value === "|" || value === "&" || value === "is";
}

/** Process excerpt tokens and add them to the tokens array */
function processExcerptTokens(
  excerptTokens: readonly ExcerptToken[],
  tokens: ReviewToken[],
  deprecated?: boolean,
): void {
  for (const excerpt of excerptTokens) {
    const trimmedText = excerpt.text.trim();
    if (!trimmedText) continue;

    const hasPrefixSpace = needsLeadingSpace(trimmedText);
    const hasSuffixSpace = needsTrailingSpace(trimmedText);

    if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
      tokens.push(
        createToken(TokenKind.TypeName, trimmedText, {
          navigateToId: excerpt.canonicalReference.toString(),
          hasPrefixSpace,
          hasSuffixSpace,
          deprecated,
        }),
      );
    } else {
      tokens.push(
        createToken(TokenKind.Text, trimmedText, {
          hasPrefixSpace,
          hasSuffixSpace,
          deprecated,
        }),
      );
    }
  }
}

function generate(item: ApiFunction, deprecated?: boolean): ReviewToken[] {
  const tokens: ReviewToken[] = [];
  if (item.kind !== ApiItemKind.Function) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Function token generator.`,
    );
  }

  // Extract structured properties
  const parameters = (item as unknown as { readonly parameters: ReadonlyArray<Parameter> })
    .parameters;
  const typeParameters = (
    item as unknown as { readonly typeParameters: ReadonlyArray<TypeParameter> }
  ).typeParameters;

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
    tokens.push(createToken(TokenKind.Text, "<", { deprecated }));
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

      if (index < typeParameters.length - 1) {
        tokens.push(createToken(TokenKind.Text, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
    tokens.push(createToken(TokenKind.Text, ">", { deprecated }));
  }

  // Add parameters
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

  // Add return type
  tokens.push(createToken(TokenKind.Text, "):", { hasSuffixSpace: true, deprecated }));
  processExcerptTokens(item.returnTypeExcerpt.spannedTokens, tokens, deprecated);

  return tokens;
}

export const functionTokenGenerator: TokenGenerator<ApiFunction> = {
  isValid,
  generate,
};
