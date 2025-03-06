import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isTraitItem } from "./utils/typeGuards";
import { createGenericBoundTokens, processGenerics } from "./utils/processGenerics";
import { getAPIJson } from "../main";

/**
 * Processes a trait item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The trait item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processTrait(item: Item) {
  if (!isTraitItem(item)) return;
  const apiJson = getAPIJson();
  const reviewLines: ReviewLine[] = [];
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub trait",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: item.name || "null",
    RenderClasses: ["tname", "trait"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });

  const genericsTokens = processGenerics(item.inner.trait.generics);
  // Add generics params if present
  if (item.inner.trait.generics) {
    reviewLine.Tokens.push(...genericsTokens.params);
  }

  if (item.inner.trait.bounds) {
    reviewLine.Tokens.push({ Kind: TokenKind.Text, Value: ":", HasPrefixSpace: false });
    reviewLine.Tokens.push(...createGenericBoundTokens(item.inner.trait.bounds));
  }

  // Add generics where clauses if present
  if (item.inner.trait.generics) {
    reviewLine.Tokens.push(...genericsTokens.wherePredicates);
  }

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
    HasPrefixSpace: true,
  });

  if (item.inner.trait.items) {
    item.inner.trait.items.forEach((associatedItem: number) => {
      if (!reviewLine.Children) reviewLine.Children = [];
      const childReviewLines = processItem(apiJson.index[associatedItem]);
      if (childReviewLines) reviewLine.Children.push(...childReviewLines);
    });
  }

  reviewLines.push(reviewLine);
  reviewLines.push({
    RelatedToLine: item.id.toString(),
    Tokens: [
      {
        Kind: TokenKind.Punctuation,
        Value: "}",
      },
    ],
    IsContextEndLine: true,
  });
  return reviewLines;
}
