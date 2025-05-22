import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isExternTypeItem } from "./utils/typeGuards";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes an extern type item and returns ReviewLine objects.
 *
 * @param {Item} item - The extern type item to process.
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processExternType(item: Item): ReviewLine[] | null {
  if (!isExternTypeItem(item)) return null;

  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
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

  reviewLines.push(reviewLine);
  lineIdMap.set(item.id.toString(), `extern_type_${item.name}`);
  return reviewLines;
}
