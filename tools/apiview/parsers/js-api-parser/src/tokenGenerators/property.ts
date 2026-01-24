import { ApiItem, ApiItemKind, ApiProperty, ApiPropertyItem } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator } from "./index";

function isValid(item: ApiItem): item is ApiPropertyItem {
    return item.kind === ApiItemKind.Property || item.kind === ApiItemKind.PropertySignature;
}

function generate(item: ApiPropertyItem, deprecated?: boolean): ReviewToken[] {
    const tokens: ReviewToken[] = [];
    if (item.kind !== ApiItemKind.Property && item.kind !== ApiItemKind.PropertySignature) {
        throw new Error(`Invalid item ${item.displayName} of kind ${item.kind} for Property token generator.`);
    }

    if (item instanceof ApiProperty && item.isStatic) {
        tokens.push({ Kind: TokenKind.Keyword, Value: "static", HasSuffixSpace: true, IsDeprecated: deprecated });
    }

    if (item.isReadonly) {
        tokens.push({ Kind: TokenKind.Keyword, Value: "readonly", HasSuffixSpace: true, IsDeprecated: deprecated });
    }

    tokens.push({ Kind: TokenKind.MemberName, Value: item.displayName, IsDeprecated: deprecated, HasSuffixSpace: false });

    if (item.isOptional) {
        tokens.push({ Kind: TokenKind.Punctuation, Value: "?", HasSuffixSpace: false, HasPrefixSpace: false, IsDeprecated: deprecated });
    }

    tokens.push({ Kind: TokenKind.Punctuation, Value: ":", HasSuffixSpace: true, HasPrefixSpace: false, IsDeprecated: deprecated });

    tokens.push({ Kind: TokenKind.TypeName, Value: item.propertyTypeExcerpt.text.trim(), IsDeprecated: deprecated });

    return tokens;
}

export const propertyTokenGenerator: TokenGenerator<ApiPropertyItem> = {
    isValid,
    generate,
};
