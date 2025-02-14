import { ReviewLine, TokenKind } from "../../models/apiview-models";
import { Item } from "../../models/rustdoc-json-types";

/**
 * Creates a ReviewLine object for the documentation of the given item.
 *
 * @param {Item} item - The item to create the documentation ReviewLine for.
 * @returns {ReviewLine} The created ReviewLine object.
 */
export function createDocsReviewLine(item: Item): ReviewLine {
  return {
    Tokens: [
      {
        Kind: TokenKind.Comment,
        Value: `/// ${item.docs}`,
        IsDocumentation: true,
      },
    ],
    RelatedToLine: item.id.toString(),
    LineId: item.id.toString() + "_docs", // Add _docs to the id to make it unique
  };
}
