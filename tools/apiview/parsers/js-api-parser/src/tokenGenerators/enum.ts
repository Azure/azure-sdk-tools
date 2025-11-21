import { ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { ITokenGenerator as TokenGenerator } from "./interfaces";

function isValid(item: ApiItem): boolean {
    return item.kind === ApiItemKind.Enum;
}

function generate(item: ApiItem, deprecated: boolean): ReviewToken[] {
    const tokens: ReviewToken[] = [];
    if(item.kind !== ApiItemKind.Enum) {
        throw new Error(`Invalid item ${item.displayName} of kind ${item.kind} for Enum token generator.`);
    }
    tokens.push({Kind: TokenKind.Keyword, Value: "export", HasSuffixSpace: true, IsDeprecated: deprecated});

    tokens.push({Kind: TokenKind.Keyword, Value: "enum", HasSuffixSpace: true, IsDeprecated: deprecated});

    tokens.push({ Kind: TokenKind.MemberName, Value: item.displayName });
    tokens.push({Kind: TokenKind.Punctuation, Value: "{", HasPrefixSpace: true, HasSuffixSpace: true, IsDeprecated: deprecated});

    // Enum members
    const enumMembers = item.members;
    for (const member of enumMembers) {
        tokens.push({Kind: TokenKind.MemberName, Value: member.displayName, IsDeprecated: deprecated});
        tokens.push({Kind: TokenKind.Punctuation, Value: ",", HasPrefixSpace: true, HasSuffixSpace: true, IsDeprecated: deprecated});
    }
    // Remove last comma
    if (enumMembers.length > 0) {
        tokens.pop();
    }
    tokens.push({Kind: TokenKind.Punctuation, Value: "}", HasPrefixSpace: true, HasSuffixSpace: true, IsDeprecated: deprecated});

    return tokens;
}

export const enumTokenGenerator: TokenGenerator = {
  isValid,
  generate,
};