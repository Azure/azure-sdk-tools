import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { processImpl } from "./processImpl";
import { processStructField } from "./processStructField";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";

/**
 * Processes a union item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The union item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processUnion(item: Item, apiJson: Crate) {
    if (!(typeof item.inner === "object" && "union" in item.inner)) return;
    const reviewLines: ReviewLine[] = [];
    if (item.docs) reviewLines.push(createDocsReviewLine(item));

    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: [],
    };

    // Process derives
    if (item.inner.union && item.inner.union.impls) {
        processImpl({ ...item, inner: { union: item.inner.union } }, apiJson, reviewLine);
    }

    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: "pub union",
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: item.name || "null",
        RenderClasses: ["union"],
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined,
    });

    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: "{",
    });
    // fields
    item.inner.union.fields.forEach((fieldId: number) => {
        const fieldItem = apiJson.index[fieldId];
        if (fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner) {
            if (!reviewLine.Children) {
                reviewLine.Children = [];
            }
            reviewLine.Children.push({
                LineId: fieldItem.id.toString(),
                Tokens: [
                    {
                        Kind: TokenKind.Keyword,
                        Value: "pub",
                    },
                    {
                        Kind: TokenKind.MemberName,
                        Value: fieldItem.name || "null",
                        HasSuffixSpace: false,
                    },
                    {
                        Kind: TokenKind.Punctuation,
                        Value: ":",
                    },
                    processStructField(fieldItem.inner.struct_field),
                ],
            });
        }
    });

    reviewLines.push(reviewLine);
    reviewLines.push({
        RelatedToLine: item.id.toString(),
        Tokens: [
            {
                Kind: TokenKind.Punctuation,
                Value: "}",
            },
        ],
    });
    return reviewLines;
}
