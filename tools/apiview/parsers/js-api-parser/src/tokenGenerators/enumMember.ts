import { ApiEnumMember, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

function isValid(item: ApiItem): item is ApiEnumMember {
  return item.kind === ApiItemKind.EnumMember;
}

function generate(item: ApiEnumMember, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];

  if (item.kind !== ApiItemKind.EnumMember) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for EnumMember token generator.`,
    );
  }

  tokens.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));

  if (item.initializerExcerpt?.spannedTokens?.length) {
    tokens.push(
      createToken(TokenKind.Text, "=", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );
    processExcerptTokens(item.initializerExcerpt.spannedTokens, tokens, deprecated);
  }

  return { tokens };
}

export const enumMemberTokenGenerator: TokenGenerator<ApiEnumMember> = {
  isValid,
  generate,
};