import { ReviewLine, TokenKind } from "../utils/apiview-models";
import { Crate, Item } from "../utils/rustdoc-json-types/jsonTypes";
import { processStructField } from "./processStructField";

/**
 * Processes a static item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The static item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processStatic(item: Item, reviewLines: ReviewLine[]) {
    if (!(typeof item.inner === 'object' && 'static' in item.inner)) return;

    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };
    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub static'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.MemberName,
        Value: item.name || "null",
        RenderClasses: [
            "static"
        ],
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined,
        HasSuffixSpace: false
    });

    // Add type and value if available
    if (item.inner.static) {
        reviewLine.Tokens.push({
            Kind: TokenKind.Punctuation,
            Value: ':'
        });
        reviewLine.Tokens.push(processStructField(item.inner.static.type)); // TODO: make sure to encode other attributes too
    }

    reviewLines.push(reviewLine);
}