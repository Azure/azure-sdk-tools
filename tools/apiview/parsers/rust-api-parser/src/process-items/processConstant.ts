import { ReviewLine, TokenKind } from "../utils/apiview-models";
import { Item } from "../utils/rustdoc-json-types/jsonTypes";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { typeToString } from "./utils/typeToString";

export function processConstant(item: Item, reviewLines: ReviewLine[]) {
    if (!(typeof item.inner === 'object' && "constant" in item.inner)) return;

    reviewLines.push(createDocsReviewLine(item));

    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };

    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub const'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.Text,
        Value: item.name || "null",
        HasSuffixSpace: false,
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: ':'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: `${typeToString(item.inner.constant.type)}`,
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined,
        HasSuffixSpace: false,
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: ';'
    });

    reviewLines.push(reviewLine);
}