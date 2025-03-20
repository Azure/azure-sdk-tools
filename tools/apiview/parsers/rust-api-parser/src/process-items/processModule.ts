import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Id, Item, ItemKind } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isModuleItem } from "./utils/typeGuards";
import { getAPIJson } from "../main";
import { getSortedChildIds } from "./utils/sorting";

/**
 * Processes a module item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The module item to process.
 * @param {Object} parentModule - Optional parent module information.
 * @returns {ReviewLine[]} Array of ReviewLine objects.
 */
export function processModule(
  item: Item,
  parentModule?: { prefix: string; id: number },
): ReviewLine[] {
  if (!isModuleItem(item)) return;
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
      RenderClasses: ["namespace"],
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
    RenderClasses: ["namespace"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: fullName,
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
    HasSuffixSpace: false,
  });

  if (item.inner && "module" in item.inner && item.inner.module.items) {
    const result = processModuleChildren(item, reviewLine, parentModule);
    reviewLines.push(...result);
  } else {
    // Empty module case
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "}",
    });
    reviewLines.push(reviewLine);
  }

  return reviewLines;
}

/**
 * Processes the children of a module item.
 *
 * @param {Item} item - The module item whose children are to be processed.
 * @param {ReviewLine} moduleReviewLine - The ReviewLine for the module declaration.
 * @param {Object} parentModule - Optional parent module information.
 * @returns {ReviewLine[]} Array of ReviewLine objects for the module and its children.
 */
function processModuleChildren(
  item: Item,
  moduleReviewLine: ReviewLine,
  parentModule?: { prefix: string; id: number },
): ReviewLine[] {
  const apiJson = getAPIJson();
  const resultLines: ReviewLine[] = [];
  let nonModuleChildrenExist = false;

  if (!isModuleItem(item)) {
    return [];
  }
  const sortedChildIds = getSortedChildIds(item.inner.module.items);

  // Process non-module children in the sorted order
  for (let i = 0; i < sortedChildIds.nonModule.length; i++) {
    const childId = sortedChildIds.nonModule[i];
    const childItem = apiJson.index[childId];
    if (!isModuleItem(childItem)) {
      const childReviewLines = processItem(childItem);
      if (childReviewLines) {
        if (!moduleReviewLine.Children) {
          moduleReviewLine.Children = [];
        }
        moduleReviewLine.Children.push(...childReviewLines.filter((item) => item != null));
      }
      nonModuleChildrenExist = true;
    }
  }

  // Add the current module's review line after processing non-module children
  resultLines.push(moduleReviewLine);
  if (!nonModuleChildrenExist) {
    moduleReviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "}",
    });
  } else {
    resultLines.push({
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
  for (let i = 0; i < sortedChildIds.module.length; i++) {
    const moduleChildId = sortedChildIds.module[i];
    const moduleChild = apiJson.index[moduleChildId];
    const childItem = apiJson.index[moduleChild.id];
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
      resultLines.push(...siblingModuleLines);
    }
  }

  return resultLines;
}
