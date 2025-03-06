import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isTraitAliasItem } from "./utils/typeGuards";

/**
 * Processes a trait alias item and returns ReviewLine objects.
 *
 * @param {Item} item - The trait alias item to process.
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @returns {ReviewLine[]} The ReviewLine objects if processing fails.
 */
export function processTraitAlias(item: Item): ReviewLine[] {
  if (!isTraitAliasItem(item)) return [];

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
    Value: "pub trait",
  });

  // Add name
  reviewLine.Tokens.push({
    Kind: TokenKind.Text,
    Value: item.name || "unknown",
    HasSuffixSpace: false,
  });

  // Add equals sign
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "=",
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.Text,
    Value: "/* trait alias definition */",
  });
  // TODO: Add example for trait alias in the template

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

  reviewLines.push(reviewLine);
  return reviewLines;
}
