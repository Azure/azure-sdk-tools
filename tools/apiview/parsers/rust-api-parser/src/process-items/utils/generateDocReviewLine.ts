import { ReviewLine, TokenKind } from "../../utils/apiview-models";
import { Item } from "../../utils/rustdoc-json-types/jsonTypes";

/**
 * Creates a ReviewLine object for the documentation of the given item.
 *
 * @param {Item} item - The item to create the documentation ReviewLine for.
 * @returns {ReviewLine} The created ReviewLine object.
 */
export function createDocsReviewLine(item: Item): ReviewLine {
    return {
        Tokens: [{
            Kind: TokenKind.Comment,
            Value: item.docs,
            IsDocumentation: true,
        }],
        RelatedToLine: item.id.toString(),
        LineId: item.id.toString() + "_docs"
    };
}