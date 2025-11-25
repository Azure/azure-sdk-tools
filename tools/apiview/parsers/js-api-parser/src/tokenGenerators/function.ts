import {
  ApiFunction,
  ApiItem,
  ApiItemKind,
  ExcerptTokenKind,
  Parameter,
  TypeParameter,
} from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator } from "./index";

function isValid(item: ApiItem): item is ApiFunction {
  return item.kind === ApiItemKind.Function;
}

function generate(item: ApiFunction, deprecated?: boolean): ReviewToken[] {
  const tokens: ReviewToken[] = [];
  if (item.kind !== ApiItemKind.Function) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Function token generator.`,
    );
  }

  // Add export keyword
  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "export",
    HasSuffixSpace: true,
    IsDeprecated: deprecated,
  });

  // Add function keyword
  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "function",
    HasSuffixSpace: true,
    IsDeprecated: deprecated,
  });

  // Add function name
  tokens.push({ Kind: TokenKind.MemberName, Value: item.displayName, IsDeprecated: deprecated });

  // Try to use structured properties first (parameters, typeParameters)
  const parameters = (item as unknown as { readonly parameters: ReadonlyArray<Parameter> })
    .parameters;
  const typeParameters = (
    item as unknown as { readonly typeParameters: ReadonlyArray<TypeParameter> }
  ).typeParameters;

  // If we have structured parameters/typeParameters, use them
  if (parameters || typeParameters) {
    // Add type parameters if present
    if (typeParameters && typeParameters.length > 0) {
      tokens.push({ Kind: TokenKind.Text, Value: "<", IsDeprecated: deprecated });
      typeParameters.forEach((tp, index) => {
        tokens.push({ Kind: TokenKind.TypeName, Value: tp.name, IsDeprecated: deprecated });
        if (tp.constraintExcerpt && tp.constraintExcerpt.text.trim()) {
          tokens.push({ Kind: TokenKind.Text, Value: " extends ", IsDeprecated: deprecated });
          tokens.push({
            Kind: TokenKind.Text,
            Value: tp.constraintExcerpt.text.trim(),
            IsDeprecated: deprecated,
          });
        }
        if (index < typeParameters.length - 1) {
          tokens.push({ Kind: TokenKind.Text, Value: ", ", IsDeprecated: deprecated });
        }
      });
      tokens.push({ Kind: TokenKind.Text, Value: ">", IsDeprecated: deprecated });
    }

    // Add opening parenthesis
    tokens.push({ Kind: TokenKind.Text, Value: "(", IsDeprecated: deprecated });

    // Add parameters if present
    if (parameters && parameters.length > 0) {
      parameters.forEach((param, index) => {
        // Add parameter name
        tokens.push({ Kind: TokenKind.Text, Value: param.name, IsDeprecated: deprecated });

        // Add optional indicator if present
        if (param.isOptional) {
          tokens.push({ Kind: TokenKind.Text, Value: "?", IsDeprecated: deprecated });
        }

        // Add type annotation
        tokens.push({ Kind: TokenKind.Text, Value: ": ", IsDeprecated: deprecated });

        // Process parameter type from excerpt tokens
        for (const excerpt of param.parameterTypeExcerpt.spannedTokens) {
          if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
            tokens.push({
              Kind: TokenKind.TypeName,
              Value: excerpt.text,
              NavigateToId: excerpt.canonicalReference.toString(),
              IsDeprecated: deprecated,
            });
          } else if (excerpt.text.trim()) {
            tokens.push({ Kind: TokenKind.Text, Value: excerpt.text, IsDeprecated: deprecated });
          }
        }

        // Add comma if not last parameter
        if (index < parameters.length - 1) {
          tokens.push({ Kind: TokenKind.Text, Value: ", ", IsDeprecated: deprecated });
        }
      });
    }

    // Add closing parenthesis and return type
    tokens.push({ Kind: TokenKind.Text, Value: "): ", IsDeprecated: deprecated });

    // Process return type
    for (const excerpt of item.returnTypeExcerpt.spannedTokens) {
      if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
        tokens.push({
          Kind: TokenKind.TypeName,
          Value: excerpt.text,
          NavigateToId: excerpt.canonicalReference.toString(),
          IsDeprecated: deprecated,
        });
      } else if (excerpt.text.trim()) {
        tokens.push({ Kind: TokenKind.Text, Value: excerpt.text, IsDeprecated: deprecated });
      }
    }
  } else {
    // Fallback: Process parameters and return type from excerptTokens
    for (const excerpt of item.excerptTokens) {
      if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
        tokens.push({
          Kind: TokenKind.TypeName,
          Value: excerpt.text,
          NavigateToId: excerpt.canonicalReference.toString(),
          IsDeprecated: deprecated,
        });
      } else {
        // For other tokens (punctuation, parameter names, etc.)
        const text = excerpt.text.trim();
        if (text) {
          tokens.push({
            Kind: TokenKind.Text,
            Value: text,
            IsDeprecated: deprecated,
          });
        }
      }
    }
  }

  return tokens;
}

export const functionTokenGenerator: TokenGenerator<ApiFunction> = {
  isValid,
  generate,
};
