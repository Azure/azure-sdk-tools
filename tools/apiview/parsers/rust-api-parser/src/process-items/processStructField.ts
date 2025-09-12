import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a struct field item and returns its ReviewLine.
 *
 * @param {Item} fieldItem - The struct field item to process.
 * @param {string} lineIdPrefix - The prefix from ancestors for hierarchical LineId
 * @returns {ReviewLine} The ReviewLine object for the struct field.
 */
export function processStructField(fieldItem: Item, lineIdPrefix: string = ""): ReviewLine {
  if (!(fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner))
    return null;

  const tokens = [
    {
      Kind: TokenKind.Keyword,
      Value: "pub",
    },
    {
      Kind: TokenKind.MemberName,
      Value: fieldItem.name || "unknown_field_item",
      HasSuffixSpace: false,
      NavigateToId: fieldItem.id.toString(), // Will be updated in post-processing
    },
    {
      Kind: TokenKind.Punctuation,
      Value: ":",
    },
    ...typeToReviewTokens(fieldItem.inner.struct_field),
  ];

  const contentBasedLineId = createContentBasedLineId(tokens, lineIdPrefix, fieldItem.id.toString());
  
  return {
    LineId: contentBasedLineId,
    Tokens: tokens,
  };
}
