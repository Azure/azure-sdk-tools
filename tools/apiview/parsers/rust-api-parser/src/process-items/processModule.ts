import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { getAPIJson } from "../main";
import { getSortedChildIds } from "./utils/sorting";
import { processUse } from "./processUse";
import { AnnotatedReviewLines } from "./utils/models";

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
  if (!isModuleItem(item)) return [];
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

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

  const apiJson = getAPIJson();
  const allChildIds = item.inner.module.items;
  const regularChildIds = [];
  const useChildIds = [];

  const annotatedReviewLines: AnnotatedReviewLines = { siblingModule: {}, children: {} };

  for (const childId of allChildIds) {
    if (isModuleItem(apiJson.index[childId])) {
      annotatedReviewLines.siblingModule[childId] = processModule(apiJson.index[childId], {
        id: item.id,
        prefix: fullName,
      });
    } else if (!isUseItem(apiJson.index[childId])) {
      regularChildIds.push(childId);
    } else if (isUseItem(apiJson.index[childId])) {
      useChildIds.push(childId);
    }
  }

  if (regularChildIds.length > 0) {
    for (const childId of regularChildIds) {
      if (childId in apiJson.index) {
        annotatedReviewLines.children[childId] = processItem(apiJson.index[childId]) || [];
      }
    }
  }

  // Process the use items
  if (useChildIds.length > 0) {
    // Process the use items
    for (const childId of useChildIds) {
      const useResult = processUse(apiJson.index[childId], { id: item.id, prefix: fullName });
      annotatedReviewLines.siblingModule[childId] = useResult.siblingModule[childId];
      annotatedReviewLines.children[childId] = useResult.children[childId];
    }
  }

  // Take the keys from the children object, sort using getSortedChildren
  // and then push to reviewLine.Children in that order
  if (Object.keys(annotatedReviewLines.children).length > 0) {
    const sortedChildIds = getSortedChildIds(
      Object.keys(annotatedReviewLines.children).map((key) => parseInt(key)),
    );
    for (const childId of sortedChildIds.nonModule) {
      if (annotatedReviewLines.children[childId]) reviewLine.Children.push(...annotatedReviewLines.children[childId]);
    }
  }

  if (reviewLine.Children.length > 0) {
    // Add the closing brace for the module
    reviewLines.push(reviewLine);
    reviewLines.push({
      Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
      IsContextEndLine: true,
      RelatedToLine: reviewLine.LineId,
    });
  } else {
    // Empty module case (no direct items or relevant re-exports)
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "}",
    });
    reviewLines.push(reviewLine);
  }

  // there will be sibling modules from the re-exported Use modules
  // add them to the allChildIds.module, sort them again
  const sortedSiblingModuleIds = getSortedChildIds(
    Object.keys(annotatedReviewLines.siblingModule).map((key) => parseInt(key)),
  );
  for (const childId of sortedSiblingModuleIds.module) {
    reviewLines.push(...annotatedReviewLines.siblingModule[childId]);
  }

  return reviewLines;
}
