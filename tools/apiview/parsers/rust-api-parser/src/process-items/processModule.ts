import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item, ItemKind } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { getAPIJson } from "../main";
import { getSortedChildIds } from "./utils/sorting";
import { externalReexports, getModuleChildIdsByPath } from "./utils/externalReexports";

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

  if (item.inner.module.items.length > 0) {
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

  const childrenFromUseModules = [];
  // get "id"s of use items that are modules
  for (let i = 0; i < item.inner.module.items.length; i++) {
    const childId = item.inner.module.items[i];
    const childItem = apiJson.index[childId];
    if (isUseItem(childItem)) {
      if (childItem.inner.use.id in apiJson.index) {
        const useRefItem = apiJson.index[childItem.inner.use.id];
        if (isModuleItem(useRefItem)) {
          // we only want to process the items inside the glob, do not show the glob name
          // to avoid showing the internal details such as "generated::*"
          // we will recursively process the items inside the glob, which may also include more globs
          childrenFromUseModules.push(...useRefItem.inner.module.items);
        }
        // else not a module, do nothing
      } else if (childItem.inner.use.id in apiJson.paths) {
        // it is in paths
        const useRefItem = apiJson.paths[childItem.inner.use.id];
        if (useRefItem.kind == ItemKind.Module) {
          childrenFromUseModules.push(
            ...getModuleChildIdsByPath(useRefItem.path.join("::"), apiJson),
          );
        }
        // else not a module, do nothing
      }
    }
  }

  const sortedChildIds = getSortedChildIds(item.inner.module.items.concat(childrenFromUseModules));

  // Process non-module children in the sorted order
  for (let i = 0; i < sortedChildIds.nonModule.length; i++) {
    const childId = sortedChildIds.nonModule[i];
    if (childId in apiJson.index) {
      const childItem = apiJson.index[childId];
      // Check if the child item is a module or a use item that refers to a module
      // If it is not, process it as a regular item
      if (isModuleItem(childItem)) {
        continue;
      }
      if (isUseItem(childItem)) {
        const useItemId = childItem.inner.use.id;
        // Check if the use item refers to a module
        // If it does, skip processing it as a regular item
        if (
          (useItemId in apiJson.index && isModuleItem(apiJson.index[useItemId])) ||
          (useItemId in apiJson.paths && apiJson.paths[useItemId].kind == ItemKind.Module)
        ) {
          continue;
        }
      }
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

  const siblingModuleLines: ReviewLine[] = [];

  const modulePrefix = parentModule
    ? parentModule.prefix
      ? `${parentModule.prefix}::${item.name}`
      : item.name
    : item.name;
  // Then process module children
  for (let i = 0; i < sortedChildIds.module.length; i++) {
    const moduleChildId = sortedChildIds.module[i];
    if (moduleChildId in apiJson.index) {
      const moduleChild = apiJson.index[moduleChildId];
      siblingModuleLines.push(
        ...processModule(moduleChild, {
          id: item.id,
          prefix: modulePrefix,
        }),
      );
    } else if (moduleChildId in apiJson.paths) {
      const externalModules = externalReexports(moduleChildId).modules;
      siblingModuleLines.push(...externalModules);
      // TODO: fix how we are showing them
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

  if (siblingModuleLines) {
    resultLines.push(...siblingModuleLines);
  }

  return resultLines;
}
