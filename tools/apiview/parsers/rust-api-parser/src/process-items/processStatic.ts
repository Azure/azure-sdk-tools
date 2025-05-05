import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isStaticItem } from "./utils/typeGuards";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes a static item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The static item to process.
 */
export function processStatic(item: Item) {
  if (!isStaticItem(item)) return;

  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  lineIdMap.set(item.id.toString(), `static_${item.name}`);
  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
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
    RenderClasses: ["interface"],
    NavigateToId: reviewLine.LineId,
    NavigationDisplayName: item.name || undefined,
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

  reviewLines.push(reviewLine);
  return reviewLines;
}
