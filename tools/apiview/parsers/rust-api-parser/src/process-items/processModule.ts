import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isModuleItem } from "./utils/typeGuards";
import { getAPIJson } from "../main";

/**
 * Processes a module item and adds its documentation to the ReviewLine.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The module item to process.
 */
export function processModule(
  item: Item,
  parentModule?: { prefix: string; id: number },
): ReviewLine[] {
  if (!isModuleItem(item)) return;
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
    Value: "pub mod",
  });
  let fullName = "";
  // parent module
  if (parentModule) {
    fullName += parentModule.prefix + "::";
    reviewLine.Tokens.push({
      Kind: TokenKind.TypeName,
      Value: parentModule.prefix,
      RenderClasses: ["module"],
      NavigateToId: parentModule.id.toString(),
      HasSuffixSpace: false,
    });
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: `::`,
      HasSuffixSpace: false,
    });
  }

  fullName += item.name;
  // current module
  reviewLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: item.name || "null",
    RenderClasses: ["module"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: fullName,
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
    HasSuffixSpace: false,
  });
  if (item.inner.module.items) {
    // First process non-module children
    let nonModuleChildrenExist = false;
    item.inner.module.items.forEach((childId: number) => {
      const childItem = apiJson.index[childId];
      if (!isModuleItem(childItem)) {
        const childReviewLines = processItem(childItem);
        if (childReviewLines) {
          if (!reviewLine.Children) {
            reviewLine.Children = [];
          }
          reviewLine.Children.push(...childReviewLines.filter((item) => item != null));
        }
        nonModuleChildrenExist = true;
      }
    });

    // Add the current module's review line after processing non-module children
    reviewLines.push(reviewLine);
    if (!nonModuleChildrenExist) {
      reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: "}",
      });
    } else {
      reviewLines.push({
        RelatedToLine: item.id.toString(),
        Tokens: [
          {
            Kind: TokenKind.Punctuation,
            Value: "}",
          },
        ],
      });
    }

    // Then process module children
    item.inner.module.items.forEach((childId: number) => {
      const childItem = apiJson.index[childId];
      if (isModuleItem(childItem)) {
        const modulePrefix = parentModule
          ? parentModule.prefix
            ? `${parentModule.prefix}::${item.name}`
            : item.name
          : item.name;
        const siblingModuleLines = processModule(childItem, {
          id: item.id,
          prefix: modulePrefix,
        });
        if (siblingModuleLines) {
          reviewLines.push(...siblingModuleLines);
        }
      }
    });
  }

  return reviewLines;
}
