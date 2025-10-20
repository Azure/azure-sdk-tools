import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isStaticItem } from "./utils/typeGuards";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a static item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The static item to process.
 * @param {string} lineIdPrefix - The prefix for hierarchical line IDs.
 */
export function processStatic(item: Item, lineIdPrefix: string = "") {
  if (!isStaticItem(item)) return;

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    Tokens: [],
    Children: [],
  };
  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub static",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_static",
    NavigateToId: item.id.toString(), // Will be updated in post-processing
    HasSuffixSpace: false,
  });

  // Add type and value if available
  if (item.inner.static) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: ":",
    });
    reviewLine.Tokens.push(...typeToReviewTokens(item.inner.static.type));
  }

  // Create content-based LineId from the tokens
  const contentBasedLineId = createContentBasedLineId(reviewLine.Tokens, lineIdPrefix, item.id.toString());
  reviewLine.LineId = contentBasedLineId;

  // Create docs with content-based LineId
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item, contentBasedLineId) : [];

  reviewLines.push(reviewLine);
  return reviewLines;
}
