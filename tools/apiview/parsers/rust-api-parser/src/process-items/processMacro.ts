import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isMacroItem } from "./utils/typeGuards";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a macro item and returns ReviewLine objects.
 *
 * @param {Item} item - The macro item to process.
 * @param {string} lineIdPrefix - The prefix from ancestors for hierarchical LineId
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processMacro(item: Item, lineIdPrefix: string = ""): ReviewLine[] | null {
  if (!isMacroItem(item)) return null;

  // Split the macro value by newlines
  const macroLines = item.inner.macro.split("\n");

  // Create tokens for the first line to generate content-based LineId
  const firstLineTokens = [
    {
      Kind: TokenKind.Text,
      Value: macroLines[0],
    },
  ];

  // Create content-based LineId from first line tokens
  const contentBasedLineId = createContentBasedLineId(firstLineTokens, lineIdPrefix, item.id.toString());

  // Create docs with content-based LineId
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item, contentBasedLineId) : [];

  // Create ReviewLines for each macro line
  macroLines.forEach((line, index) => {
    const reviewLine: ReviewLine = {
      LineId: index === 0 ? contentBasedLineId : undefined,
      Tokens: [
        {
          Kind: TokenKind.Text,
          Value: line,
        },
      ],
      // Only additional lines are related to the first line
      RelatedToLine: index === 0 ? undefined : contentBasedLineId,
    };
    reviewLines.push(reviewLine);
  });

  return reviewLines;
}
