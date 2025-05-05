import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes a struct field item and returns its ReviewLine.
 *
 * @param {Item} fieldItem - The struct field item to process.
 * @returns {ReviewLine} The ReviewLine object for the struct field.
 */
export function processStructField(fieldItem: Item): ReviewLine {
  if (!(fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner))
    return null;

  lineIdMap.set(fieldItem.id.toString(), `fieldItem_${fieldItem.name}`);
  return {
    LineId: fieldItem.id.toString(),
    Tokens: [
      {
        Kind: TokenKind.Keyword,
        Value: "pub",
      },
      {
        Kind: TokenKind.MemberName,
        Value: fieldItem.name || "unknown_field_item",
        HasSuffixSpace: false,
        RenderClasses: ["interface"],
        NavigateToId: fieldItem.id.toString(),
        NavigationDisplayName: fieldItem.name,
      },
      {
        Kind: TokenKind.Punctuation,
        Value: ":",
      },
      ...typeToReviewTokens(fieldItem.inner.struct_field),
    ],
  };
}
