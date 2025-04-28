import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isMacroItem } from "./utils/typeGuards";

/**
 * Processes a macro item and returns ReviewLine objects.
 *
 * @param {Item} item - The macro item to process.
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processMacro(item: Item): ReviewLine[] | null {
  if (!isMacroItem(item)) return null;

  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  // Split the macro value by newlines
  const macroLines = item.inner.macro.split("\n");

  // Create ReviewLines for each macro line
  macroLines.forEach((line, index) => {
    const reviewLine: ReviewLine = {
      LineId: index === 0 ? item.id.toString() : `${item.id.toString()}_macro_${index}`,
      Tokens: [
        {
          Kind: TokenKind.Text,
          Value: line,
        },
      ],
      // Only additional lines are related to the first line
      RelatedToLine: index === 0 ? undefined : item.id.toString(),
    };
    reviewLines.push(reviewLine);
  });

  return reviewLines;
}
