import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item, ItemKind } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { getAPIJson } from "../main";
import { getSortedChildIds } from "./utils/sorting";
import { getModuleChildIdsByPath, processModuleReexport } from "./utils/externalReexports";

/**
 * Collects all child IDs for a module, including direct children and those from 'use' re-exports.
 * @param item The module item.
 * @param apiJson The full API JSON object.
 * @returns An array of child item IDs.
 */
function getAllChildIds(item: Item, apiJson: Crate): number[] {
  if (!isModuleItem(item)) {
    return [];
  }

  const directChildIds = item.inner.module.items;
  const childrenFromUseModules: number[] = [];

  for (const childId of directChildIds) {
    const childItem = apiJson.index[childId];
    if (isUseItem(childItem)) {
      const useTargetId = childItem.inner.use.id;
      if (useTargetId in apiJson.index) {
        const useRefItem = apiJson.index[useTargetId];
        if (isModuleItem(useRefItem)) {
          // Recursively get children from the re-exported module
          // Note: This assumes no circular re-exports or handles them implicitly if getModuleChildIdsByPath does.
          // Consider adding cycle detection if necessary.
          childrenFromUseModules.push(...getAllChildIds(useRefItem, apiJson));
        }
      } else if (useTargetId in apiJson.paths) {
        const useRefItem = apiJson.paths[useTargetId];
        if (useRefItem.kind === ItemKind.Module) {
          if (childItem.inner.use.is_glob) {
            const temp = getModuleChildIdsByPath(useRefItem.path.join("::"), apiJson);
            childrenFromUseModules.push(...temp);
          } else {
            childrenFromUseModules.push(useTargetId);
          }
        }
      }
    }
  }
  // Combine direct non-use children and children from use modules
  // Filter out the 'use' items themselves that pointed to modules, keep other direct children.
  const nonUseDirectChildren = directChildIds.filter((id) => {
    const childItem = apiJson.index[id];
    if (isUseItem(childItem)) {
      const useTargetId = childItem.inner.use.id;
      return !(
        (useTargetId in apiJson.index && isModuleItem(apiJson.index[useTargetId])) ||
        (useTargetId in apiJson.paths && apiJson.paths[useTargetId].kind === ItemKind.Module)
      );
    }
    return true; // Keep non-use items
  });

  return [...nonUseDirectChildren, ...childrenFromUseModules];
}

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

  // Check if the module has any effective children (direct or via re-exports)
  const apiJson = getAPIJson();
  const allChildIds = getAllChildIds(item, apiJson);

  if (allChildIds.length > 0) {
    const result = processModuleChildren(item, reviewLine, parentModule, allChildIds);
    reviewLines.push(...result);
  } else {
    // Empty module case (no direct items or relevant re-exports)
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
 * @param {number[]} allChildIds - Pre-calculated list of all effective child IDs.
 * @returns {ReviewLine[]} Array of ReviewLine objects for the module and its children.
 */
function processModuleChildren(
  item: Item,
  moduleReviewLine: ReviewLine,
  parentModule: { prefix: string; id: number } | undefined,
  allChildIds: number[], // Receive pre-calculated child IDs
): ReviewLine[] {
  const apiJson = getAPIJson();
  const resultLines: ReviewLine[] = [];
  let nonModuleChildrenExist = false;

  // Sort the combined list of children
  const sortedChildIds = getSortedChildIds(allChildIds);

  // Process non-module children
  for (const childId of sortedChildIds.nonModule) {
    if (childId in apiJson.index) {
      const childItem = apiJson.index[childId];
      // Skip items that are modules or use-items pointing to modules
      if (isModuleItem(childItem)) continue;
      if (isUseItem(childItem)) {
        const useTargetId = childItem.inner.use.id;
        if (
          (useTargetId in apiJson.index && isModuleItem(apiJson.index[useTargetId])) ||
          (useTargetId in apiJson.paths && apiJson.paths[useTargetId].kind === ItemKind.Module)
        ) {
          continue;
        }
      }

      const childReviewLines = processItem(childItem);
      if (childReviewLines) {
        moduleReviewLine.Children = moduleReviewLine.Children || [];
        moduleReviewLine.Children.push(...childReviewLines.filter((line) => line != null));
        nonModuleChildrenExist = true;
      }
    } // else-case handled at processUse
  }

  // Process module children (siblings)
  const siblingModuleLines: ReviewLine[] = [];
  const modulePrefix = parentModule ? `${parentModule.prefix}::${item.name}` : item.name;

  for (const moduleChildId of sortedChildIds.module) {
    if (moduleChildId in apiJson.index) {
      const moduleChild = apiJson.index[moduleChildId];
      // Ensure it's actually a module before processing
      if (isModuleItem(moduleChild)) {
        siblingModuleLines.push(
          ...processModule(moduleChild, {
            id: item.id,
            prefix: modulePrefix,
          }),
        );
      }
    } else if (moduleChildId in apiJson.paths) {
      // Handle external modules re-exported into this scope
      const externalModules = processModuleReexport(
        moduleChildId,
        apiJson.paths[moduleChildId],
        apiJson,
        {
          id: item.id,
          prefix: modulePrefix,
        },
      );
      siblingModuleLines.push(...externalModules);
    }
  }

  // Assemble the final lines
  resultLines.push(moduleReviewLine);

  if (nonModuleChildrenExist) {
    // Add closing brace on a new line if there were non-module children inside
    resultLines.push({
      RelatedToLine: item.id.toString(),
      Tokens: [
        {
          Kind: TokenKind.Punctuation,
          Value: "}",
        },
      ],
    });
  } else {
    // Add closing brace directly to the module line if only module children (or no children) exist
    // This case should ideally be handled by the check in `processModule` before calling `processModuleChildren`,
    // but we add the brace here if `nonModuleChildrenExist` is false.
    moduleReviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "}",
    });
  }

  // Add sibling modules after the current module's closing brace
  resultLines.push(...siblingModuleLines);

  return resultLines;
}
