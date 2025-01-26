import { ReviewLine, TokenKind } from "../utils/apiview-models";
import { Crate, Item } from "../utils/rustdoc-json-types/jsonTypes";
import { processImpl, processTraitImpls } from "./processImpl";

export function processEnum(item: Item, apiJson: Crate, reviewLines: ReviewLine[]) {
    if (!(typeof item.inner === 'object' && "enum" in item.inner)) return;

    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };

    processTraitImpls(item.inner.enum.impls, apiJson, reviewLine);

    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub enum'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: item.name || "null",
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: '{'
    });

    // Process enum variants
    if (item.inner.enum.variants) {
        item.inner.enum.variants.forEach((variant: number) => {
            const variantItem = apiJson.index[variant];
            const variantLine: ReviewLine = {
                LineId: variantItem.id.toString(),
                Tokens: [
                    {
                        Kind: TokenKind.TypeName,
                        Value: variantItem.name || "null",
                        NavigateToId: variantItem.id.toString(),
                        NavigationDisplayName: variantItem.name || undefined,
                        HasSuffixSpace: false
                    },
                    {
                        Kind: TokenKind.Punctuation,
                        Value: ',',
                        HasSuffixSpace: false
                    }
                ],
                Children: []
            };
            reviewLine.Children.push(variantLine);
        });
    }

    reviewLines.push(reviewLine);
    reviewLines.push({
        RelatedToLine: item.id.toString(),
        Tokens: [{
            Kind: TokenKind.Punctuation,
            Value: '}'
        }],
    });
}