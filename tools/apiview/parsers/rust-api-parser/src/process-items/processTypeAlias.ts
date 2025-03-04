import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { isTypeAliasItem } from "./utils/typeGuards";
import { processGenerics } from "./utils/processGenerics";

/**
 * Processes a type alias item and returns ReviewLine objects.
 *
 * @param {Item} item - The type alias item to process.
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @returns {ReviewLine[]} The ReviewLine objects or null if processing fails.
 */
export function processTypeAlias(item: Item, apiJson: Crate): ReviewLine[] {
  if (!isTypeAliasItem(item)) return [];

  const reviewLines: ReviewLine[] = [];

  // Add documentation if available
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  // Add pub modifier
  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub type",
  });

  // Add name
  reviewLine.Tokens.push({
    Kind: TokenKind.Text,
    Value: item.name || "unknown",
  });

  // Add equals sign
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "=",
  });

  // Add the type
  reviewLine.Tokens.push(...typeToReviewTokens(item.inner.type_alias.type));
  const genericsTokens = processGenerics(item.inner.type_alias.generics);
  // Add generics params if present
  if (item.inner.type_alias.generics) {
    reviewLine.Tokens.push(...genericsTokens.params);
  }

  // Add generics where clauses if present
  if (item.inner.type_alias.generics) {
    reviewLine.Tokens.push(...genericsTokens.wherePredicates);
  }

  // TODO: Add example for type alias with generics

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

  reviewLines.push(reviewLine);
  return reviewLines;
}
