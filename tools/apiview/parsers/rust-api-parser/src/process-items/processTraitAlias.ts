import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isTraitAliasItem } from "./utils/typeGuards";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a trait alias item and returns ReviewLine objects.
 *
 * @param {Item} item - The trait alias item to process.
 * @param {string} lineIdPrefix - The prefix from ancestors for hierarchical LineId
 * @returns {ReviewLine[]} The ReviewLine objects if processing fails.
 */
export function processTraitAlias(item: Item, lineIdPrefix: string = ""): ReviewLine[] {
  if (!isTraitAliasItem(item)) return [];

  // Build tokens first
  const tokens = [
    {
      Kind: TokenKind.Keyword,
      Value: "pub trait",
    },
    {
      Kind: TokenKind.MemberName,
      Value: item.name || "unknown",
      HasSuffixSpace: false,
      RenderClasses: ["type"],
      NavigateToId: item.id.toString(), // Will be updated in post-processing
      NavigationDisplayName: item.name || undefined,
    },
    {
      Kind: TokenKind.Punctuation,
      Value: "=",
    },
    {
      Kind: TokenKind.Text,
      Value: "/* trait alias definition */",
    },
    {
      Kind: TokenKind.Punctuation,
      Value: ";",
    },
  ];

  // Create content-based LineId from tokens
  const contentBasedLineId = createContentBasedLineId(tokens, lineIdPrefix, item.id.toString());

  // Create docs with content-based LineId
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item, contentBasedLineId) : [];

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: contentBasedLineId,
    Tokens: tokens,
    Children: [],
  };

  reviewLines.push(reviewLine);
  return reviewLines;
}
