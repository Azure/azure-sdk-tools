import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { processStructField } from "./utils/processStructField";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isStaticItem } from "./utils/typeGuards";

/**
 * Processes a static item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The static item to process.
 */
export function processStatic(item: Item) {
  if (!isStaticItem(item)) return;

  const reviewLines: ReviewLine[] = [];
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

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
    Value: item.name || "null",
    RenderClasses: ["static"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });

  // Add type and value if available
  if (item.inner.static) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: ":",
    });
    reviewLine.Tokens.push(processStructField(item.inner.static.type)); // TODO: make sure to encode other attributes too
  }

  reviewLines.push(reviewLine);
  return reviewLines;
}
