import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../models/rustdoc-json-types";
import { processImpl } from "./processImpl";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";

/**
 * Processes a struct item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The struct item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processUse(item: Item, reviewLines: ReviewLine[]) {
    if (!(typeof item.inner === 'object' && 'use' in item.inner)) return;

    if (item.docs) reviewLines.push(createDocsReviewLine(item));

    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };

    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub use'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: item.inner.use.source || "null",
        RenderClasses: [
            "use"
        ],
    });

    reviewLines.push(reviewLine);
}
