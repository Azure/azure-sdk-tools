import { ReviewLine, TokenKind } from "../apiview-models";
import { Crate, Item } from "../rustdoc-json-types/jsonTypes";

/**
 * Processes a struct item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The struct item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processStruct(apiJson: Crate, item: Item, reviewLines: ReviewLine[]) {
    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };
    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub struct'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: item.name || "null",
        RenderClasses: [
            "struct"
        ],
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined
    });

    // fields
    if (typeof item.inner === 'object' && 'struct' in item.inner && item.inner.struct && typeof item.inner.struct.kind === 'object' && 'plain' in item.inner.struct.kind && item.inner.struct.kind.plain.fields) {
        item.inner.struct.kind.plain.fields.forEach((fieldId: number) => {
            const fieldItem = apiJson.index[fieldId];
            if (fieldItem && typeof fieldItem.inner === 'object' && 'struct_field' in fieldItem.inner) {
                if (!reviewLine.Children) {
                    reviewLine.Children = [];
                }
                reviewLine.Children.push({
                    LineId: fieldItem.id.toString(),
                    Tokens: [
                        {
                            Kind: TokenKind.Keyword,
                            Value: 'pub'
                        },
                        {
                            Kind: TokenKind.MemberName,
                            Value: fieldItem.name || "null"
                        },
                        {
                            Kind: TokenKind.Punctuation,
                            Value: ':'
                        },
                        {
                            Kind: TokenKind.TypeName,
                            Value: "unknown"
                        }
                    ]
                });
            }
        });
    }

    reviewLines.push(reviewLine);
}
