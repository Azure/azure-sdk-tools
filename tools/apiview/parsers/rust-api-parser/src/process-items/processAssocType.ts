import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { isAssocTypeItem } from "./utils/typeGuards";
import { createGenericBoundTokens, processGenerics } from "./utils/processGenerics";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes an associated type item and returns ReviewLine objects.
 *
 * @param {Item} item - The associated type item to process.
 * @param {string} lineIdPrefix - The prefix for hierarchical line IDs.
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processAssocType(item: Item, lineIdPrefix: string = ""): ReviewLine[] | null {
  if (!isAssocTypeItem(item)) return null;

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    Tokens: [],
    Children: [],
  };

  // Add type keyword
  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "type",
  });

  // Add name
  reviewLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown",
    NavigateToId: item.id.toString(), // Will be updated in post-processing
    NavigationDisplayName: item.name || undefined,
  });

  // Handle generics
  const generics = processGenerics(item.inner.assoc_type.generics);
  reviewLine.Tokens.push(...generics.params);

  // Add bounds if available
  if (item.inner.assoc_type.bounds.length > 0) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: ":",
    });
    reviewLine.Tokens.push(...createGenericBoundTokens(item.inner.assoc_type.bounds));
  }

  // Handle default type
  if (item.inner.assoc_type.type) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "=",
    });
    reviewLine.Tokens.push(...typeToReviewTokens(item.inner.assoc_type.type));
  }

  // Add where clauses if present
  reviewLine.Tokens.push(...generics.wherePredicates);

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
