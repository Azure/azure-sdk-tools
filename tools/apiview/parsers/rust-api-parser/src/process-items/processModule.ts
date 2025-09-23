import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { getAPIJson, processedItems } from "../main";
import { getSortedChildIds } from "./utils/sorting";
import { processUse } from "./processUse";
import { AnnotatedReviewLines } from "./utils/models";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a module item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The module item to process.
 * @param {Object} parentModule - Optional parent module information.
 * @param {string} lineIdPrefix - The prefix for hierarchical line IDs.
 * @returns {ReviewLine[]} Array of ReviewLine objects.
 */
export function processModule(
  item: Item,
  parentModule?: { prefix: string; id: number },
  lineIdPrefix: string = "",
): ReviewLine[] {
  if (!isModuleItem(item)) return [];
  const apiJson = getAPIJson();
  const isRootModule = item.id === apiJson.root;

  // Build tokens first for non-root modules
  const tokens = [];
  let fullName = "";
  let contentBasedLineId = "";

  if (!isRootModule) {
    tokens.push({
      Kind: TokenKind.Keyword,
      Value: "pub mod",
    });
    // parent module
    if (parentModule) {
      fullName += parentModule.prefix + "::";
      tokens.push({
        Kind: TokenKind.TypeName,
        Value: parentModule.prefix,
        RenderClasses: ["namespace"],
        NavigateToId: parentModule.id.toString(), // Will be updated in post-processing
        HasSuffixSpace: false,
      });
      tokens.push({
        Kind: TokenKind.Punctuation,
        Value: `::`,
        HasSuffixSpace: false,
      });
    }
    fullName += item.name;
    // current module
    tokens.push({
      Kind: TokenKind.TypeName,
      Value: item.name || "unknown_mod",
      RenderClasses: ["namespace"],
      NavigateToId: item.id.toString(), // Will be updated in post-processing
      NavigationDisplayName: fullName,
    });

    tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "{",
      HasSuffixSpace: false,
    });

    // Create content-based LineId from tokens
    contentBasedLineId = createContentBasedLineId(tokens, lineIdPrefix, item.id.toString());
  }

  // Create docs with content-based LineId (empty string for root modules)
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item, contentBasedLineId) : [];

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: contentBasedLineId,
    Tokens: tokens,
    Children: [],
  };

  // For child processing, use the current content-based lineId if non-root, or the passed lineIdPrefix if root
  const childLineIdPrefix = isRootModule ? lineIdPrefix : (reviewLine.LineId || lineIdPrefix);

  const allChildIds = item.inner.module.items;
  const regularChildIds = [];
  const useChildIds = [];

  const annotatedReviewLines: AnnotatedReviewLines = { siblingModule: {}, children: {} };
  const passThroughParentInfo = isRootModule
    ? parentModule
    : {
      id: item.id,
      prefix: fullName,
    };
  for (const childId of allChildIds) {
    if (isModuleItem(apiJson.index[childId])) {
      // 1. Process child modules first so that any references in the parent modules can be shown as links than the fully dereferenced version
      annotatedReviewLines.siblingModule[childId] = processModule(
        apiJson.index[childId],
        passThroughParentInfo,
        childLineIdPrefix,
      );
      processedItems.add(childId);
    } else if (!isUseItem(apiJson.index[childId])) {
      // 2. Non-use children should be processed before the use items and after the child modules
      regularChildIds.push(childId);
    } else if (isUseItem(apiJson.index[childId])) {
      // 3. handle use items; with globs, non-globs, modules, items, etc; either as references or add as children to this module
      useChildIds.push(childId);
    }
  }

  if (regularChildIds.length > 0) {
    for (const childId of regularChildIds) {
      if (childId in apiJson.index) {
        annotatedReviewLines.children[childId] = processItem(apiJson.index[childId], passThroughParentInfo, childLineIdPrefix) || [];
        processedItems.add(childId);
      }
    }
  }

  // Process the use items
  if (useChildIds.length > 0) {
    for (const childId of useChildIds) {
      const useResult = processUse(apiJson.index[childId], passThroughParentInfo, childLineIdPrefix);
      processedItems.add(childId);
      for (const key in useResult.siblingModule) {
        annotatedReviewLines.siblingModule[key] = useResult.siblingModule[key];
      }
      for (const key in useResult.children) {
        annotatedReviewLines.children[key] = useResult.children[key];
      }
    }
  }

  // Take the keys from the children object, sort using getSortedChildren
  // and then push to reviewLine.Children in that order
  if (Object.keys(annotatedReviewLines.children).length > 0) {
    const sortedChildIds = getSortedChildIds(
      Object.keys(annotatedReviewLines.children).map((key) => parseInt(key)),
    );
    for (const childId of sortedChildIds.nonModule) {
      if (annotatedReviewLines.children[childId]) {
        if (!isRootModule) {
          // If it is not rootModule, add items as children to the reviewLine
          reviewLine.Children.push(...annotatedReviewLines.children[childId]);
        } else {
          // If it is rootModule, add items to the top-level
          reviewLines.push(...annotatedReviewLines.children[childId]);
        }
      }
    }
  }

  if (!isRootModule) {
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
