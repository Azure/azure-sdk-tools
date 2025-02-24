import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";

/**
 * Processes a module item and adds its documentation to the ReviewLine.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The module item to process.
 */
export function processModule(apiJson: Crate, item: Item, parentModuleName?: string) {
  if (!(typeof item.inner === "object" && "module" in item.inner)) return;
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
    Value: "pub mod",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: parentModuleName ? `${parentModuleName}::${item.name}` : item.name || "null",
    RenderClasses: ["module"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: parentModuleName ? `${parentModuleName}::${item.name}` : item.name || undefined,
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
  });
  if (item.inner.module.items) {
    // First process non-module children
    item.inner.module.items.forEach((childId: number) => {
      const childItem = apiJson.index[childId];
      const isChildModule = childItem.inner && typeof childItem.inner === "object" && "module" in childItem.inner;

      if (!isChildModule) {
        const childReviewLines = processItem(childItem, apiJson);
        if (childReviewLines) {
          if (!reviewLine.Children) {
            reviewLine.Children = [];
          }
          reviewLine.Children.push(...childReviewLines.filter(item => item != null));
        }
      }
    });

    // Add the current module's review line after processing non-module children
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

    // Then process module children
    item.inner.module.items.forEach((childId: number) => {
      const childItem = apiJson.index[childId];
      const isChildModule = childItem.inner && typeof childItem.inner === "object" && "module" in childItem.inner;

      if (isChildModule) {
        const modulePrefix = parentModuleName ? `${parentModuleName}::${item.name}` : item.name;
        const siblingModuleLines = processModule(apiJson, childItem, modulePrefix);
        if (siblingModuleLines) {
          reviewLines.push(...siblingModuleLines);
        }
      }
    });
  }

  return reviewLines;
}
