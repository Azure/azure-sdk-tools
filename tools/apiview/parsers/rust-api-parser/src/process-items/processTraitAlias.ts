import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isTraitAliasItem } from "./utils/typeGuards";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes a trait alias item and returns ReviewLine objects.
 *
 * @param {Item} item - The trait alias item to process.
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @returns {ReviewLine[]} The ReviewLine objects if processing fails.
 */
export function processTraitAlias(item: Item): ReviewLine[] {
  if (!isTraitAliasItem(item)) return [];

  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  lineIdMap.set(item.id.toString(), `trait_alias_${item.name}`);
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
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown",
    HasSuffixSpace: false,
    RenderClasses: ["interface"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
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

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

  reviewLines.push(reviewLine);
  return reviewLines;
}
