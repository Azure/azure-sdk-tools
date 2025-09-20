import { ReviewLine, TokenKind } from "../../models/apiview-models";
import { Item } from "../../../rustdoc-types/output/rustdoc-types";

/**
 * Creates ReviewLine objects for the documentation of the given item.
 *
 * @param {Item} item - The item to create the documentation ReviewLines for.
 * @param {string} contentBasedLineId - The content-based LineId to relate the docs to.
 * @returns {ReviewLine[]} The created ReviewLine objects.
 */
export function createDocsReviewLines(item: Item, contentBasedLineId: string): ReviewLine[] {
  if (!item.docs) {
    return [];
  }

  // Split the docs by newline character
  const docLines = item.docs.split("\n");

  // Create a ReviewLine for each doc line
  const reviewLines: ReviewLine[] = docLines.map((line, index) => ({
    Tokens: [
      {
        Kind: TokenKind.Comment,
        Value: `/// ${line}`,
        IsDocumentation: true,
      },
    ],
    RelatedToLine: contentBasedLineId,
  }));

  return reviewLines;
}
