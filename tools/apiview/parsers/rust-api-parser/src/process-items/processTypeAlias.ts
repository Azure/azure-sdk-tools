import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { isTypeAliasItem } from "./utils/typeGuards";
import { processGenerics } from "./utils/processGenerics";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes a type alias item and returns ReviewLine objects.
 *
 * @param {Item} item - The type alias item to process.
 * @returns {ReviewLine[]} The ReviewLine objects or null if processing fails.
 */
export function processTypeAlias(item: Item): ReviewLine[] {
  if (!isTypeAliasItem(item)) return [];
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  lineIdMap.set(item.id.toString(), `type_alias_${item.name}`);
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
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown",
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || "unknown",
    RenderClasses: ["interface"],
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

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

  reviewLines.push(reviewLine);
  return reviewLines;
}
