import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../models/rustdoc-json-types";
import { processItem } from "./processItem";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";

/**
 * Processes a module item and adds its documentation to the ReviewLine.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The module item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processModule(apiJson: Crate, item: Item) {
  if (!(typeof item.inner === "object" && "module" in item.inner)) return;
  const reviewLines = [];
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub mod",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: item.name || "null",
    RenderClasses: ["module"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
  });
  if (item.inner.module.items) {
    item.inner.module.items.forEach((childId: number) => {
      const childItem = apiJson.index[childId];
      const childReviewLines = processItem(childItem, apiJson);
      if (childReviewLines) {
        if (!reviewLine.Children) {
          reviewLine.Children = [];
        }
        reviewLine.Children.push(...childReviewLines);
      }
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
  });
  return reviewLines;
}
