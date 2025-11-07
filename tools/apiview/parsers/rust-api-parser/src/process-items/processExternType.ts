import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isExternTypeItem } from "./utils/typeGuards";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes an extern type item and returns ReviewLine objects.
 *
 * @param {Item} item - The extern type item to process.
 * @param {string} lineIdPrefix - The prefix for hierarchical line IDs.
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processExternType(item: Item, lineIdPrefix: string = ""): ReviewLine[] | null {
  if (!isExternTypeItem(item)) return null;

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    Tokens: [],
    Children: [],
  };

  // Add extern type declaration
  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "extern type",
  });

  // Add name
  reviewLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_extern_type",
  });

  // Add semicolon
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

  // Create content-based LineId from the tokens
  const contentBasedLineId = createContentBasedLineId(reviewLine.Tokens, lineIdPrefix, item.id.toString());
  reviewLine.LineId = contentBasedLineId;

  // Create docs with content-based LineId
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item, contentBasedLineId) : [];

  reviewLines.push(reviewLine);
  return reviewLines;
}
