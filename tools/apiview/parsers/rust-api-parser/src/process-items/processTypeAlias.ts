import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { isTypeAliasItem } from "./utils/typeGuards";
import { processGenerics } from "./utils/processGenerics";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a type alias item and returns ReviewLine objects.
 *
 * @param {Item} item - The type alias item to process.
 * @param {string} lineIdPrefix - The prefix for hierarchical line IDs.
 * @returns {ReviewLine[]} The ReviewLine objects or null if processing fails.
 */
export function processTypeAlias(item: Item, lineIdPrefix: string = ""): ReviewLine[] {
  if (!isTypeAliasItem(item)) return [];

  // Build tokens first
  const tokens = [];

  // Add pub modifier
  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub type",
  });

  // Add name
  tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown",
    NavigateToId: item.id.toString(), // Will be updated in post-processing
  });

  // Add equals sign
  tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "=",
  });

  // Add the type
  tokens.push(...typeToReviewTokens(item.inner.type_alias.type));
  const genericsTokens = processGenerics(item.inner.type_alias.generics);
  // Add generics params if present
  if (item.inner.type_alias.generics) {
    tokens.push(...genericsTokens.params);
  }

  // Add generics where clauses if present
  if (item.inner.type_alias.generics) {
    tokens.push(...genericsTokens.wherePredicates);
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
