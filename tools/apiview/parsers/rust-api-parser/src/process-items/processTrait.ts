import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../models/rustdoc-json-types";
import { processItem } from "./processItem";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";

/**
 * Processes a trait item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The trait item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processTrait(item: Item, apiJson: Crate) {
  if (!(typeof item.inner === "object" && "trait" in item.inner)) return;
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
    RenderClasses: ["trait"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
  });
  if (item.inner.trait.items) {
    item.inner.trait.items.forEach((associatedItem: number) => {
      if (!reviewLine.Children) reviewLine.Children = [];
      const childReviewLines = processItem(apiJson.index[associatedItem], apiJson);
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
