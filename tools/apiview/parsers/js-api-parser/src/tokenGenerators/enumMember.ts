import { ApiEnumMember, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens, TokenCollector } from "./helpers";

function isValid(item: ApiItem): item is ApiEnumMember {
  return item.kind === ApiItemKind.EnumMember;
}

function generate(item: ApiEnumMember, deprecated?: boolean): GeneratorResult {
  const collector = new TokenCollector();

  if (item.kind !== ApiItemKind.EnumMember) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for EnumMember token generator.`,
    );
  }

  collector.push(createToken(TokenKind.MemberName, item.displayName, { deprecated }));

  if (item.initializerExcerpt?.spannedTokens?.length) {
    collector.push(
      createToken(TokenKind.Text, "=", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );
    processExcerptTokens(item.initializerExcerpt.spannedTokens, collector, deprecated);
  }

  return collector.toResult();
}

export const enumMemberTokenGenerator: TokenGenerator<ApiEnumMember> = {
  isValid,
  generate,
};