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

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: item.inner.macro,
  });

  reviewLines.push(reviewLine);
  return reviewLines;
}
