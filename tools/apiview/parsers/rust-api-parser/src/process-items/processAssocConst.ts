import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { isAssocConstItem } from "./utils/typeGuards";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes an associated constant item and returns ReviewLine objects.
 *
 * @param {Item} item - The associated constant item to process.
 * @param {string} lineIdPrefix - The prefix from ancestors for hierarchical LineId
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processAssocConst(item: Item, lineIdPrefix: string = ""): ReviewLine[] | null {
  if (!isAssocConstItem(item)) return null;

  // Build tokens first
  const tokens = [
    {
      Kind: TokenKind.Keyword,
      Value: "const",
    },
    {
      Kind: TokenKind.MemberName,
      Value: item.name || "unknown_assoc_const",
      HasSuffixSpace: false,
      NavigateToId: item.id.toString(), // Will be updated in post-processing
    },
    {
      Kind: TokenKind.Punctuation,
      Value: ":",
    },
    ...typeToReviewTokens(item.inner.assoc_const.type),
  ];

  if (item.inner.assoc_const.value) {
    tokens.push(
      {
        Kind: TokenKind.Punctuation,
        Value: " =",
      },
      {
        Kind: TokenKind.Text,
        Value: item.inner.assoc_const.value,
        HasSuffixSpace: false,
      }
    );
  }

  tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

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
