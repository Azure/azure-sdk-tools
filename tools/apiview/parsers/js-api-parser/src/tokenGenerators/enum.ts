import { ApiEnum, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { GeneratorResult, TokenGenerator } from "./index";

function isValid(item: ApiItem): item is ApiEnum {
  return item.kind === ApiItemKind.Enum;
}

function generate(item: ApiEnum, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];
  if (item.kind !== ApiItemKind.Enum) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Enum token generator.`,
    );
  }
  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "export",
    HasSuffixSpace: true,
    IsDeprecated: deprecated,
  });

  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "enum",
    HasSuffixSpace: true,
    IsDeprecated: deprecated,
  });

  tokens.push({ Kind: TokenKind.MemberName, Value: item.displayName, IsDeprecated: deprecated });

  return { tokens: tokens };
}

export const enumTokenGenerator: TokenGenerator<ApiEnum> = {
  isValid,
  generate,
};
