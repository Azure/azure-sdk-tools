import { ReviewLine, TokenKind } from "../apiview-models";
import { Crate, Item } from "../rustdoc-json-types/jsonTypes";
import { processItem } from "./processItem";

/**
 * Processes a module item and adds its documentation to the ReviewLine.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The module item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processModule(apiJson: Crate, item: Item, reviewLines: ReviewLine[]) {
    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };

    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub mod'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: item.name || "null",
        RenderClasses: [
            "module"
        ],
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined
    });

    if (typeof item.inner === 'object' && 'module' in item.inner && item.inner.module.items) {
        item.inner.module.items.forEach((childId: number) => {
            const childItem = apiJson.index[childId];
            const childReviewLines = processItem(apiJson, childItem);
            if (childReviewLines) {
                if (!reviewLine.Children) {
                    reviewLine.Children = [];
                }
                reviewLine.Children.push(...childReviewLines);
            }
        });
    }

    reviewLines.push(reviewLine);
}