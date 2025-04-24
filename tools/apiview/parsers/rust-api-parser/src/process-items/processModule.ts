import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Id, Item, ItemKind } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { processedItems, getAPIJson } from "../main";
import { getSortedChildIds } from "./utils/sorting";
import {
  createItemLineFromPath,
  getModuleChildIdsByPath,
  processModuleReexport,
} from "./utils/externalReexports";

function processNonModuleChildren(
  apiJson: Crate,
  parentModule: { prefix: string; id: number } | undefined,
  nonModuleChildIds: number[],
): { siblingModule: { [id: number]: ReviewLine[] }; children: { [id: number]: ReviewLine[] } } {
  const resultLines: {
    siblingModule: { [id: number]: ReviewLine[] };
    children: { [id: number]: ReviewLine[] };
  } = {
    siblingModule: {},
    children: {},
  };
  // Process each child ID in the provided list
  const finalChildIds: number[] = [];
  for (const childId of nonModuleChildIds) {
    if (!isUseItem(apiJson.index[childId])) {
      finalChildIds.push(childId);
      processedItems.add(childId);
      continue;
    }
    const dereferencedId = apiJson.index[childId].inner.use.id;
    if (!processedItems.has(dereferencedId)) {
      // it is a Use item, but not been dereferenced so far in any of the sub-modules
      // so, dereference it here
      // it could be a
      //  single item in paths
      //  single item in index
      //  glob module
      //  non-glob module
      if (dereferencedId in apiJson.index) {
        if (!isModuleItem(apiJson.index[dereferencedId])) {
          finalChildIds.push(dereferencedId);
          processedItems.add(dereferencedId);
        } else {
          if (apiJson.index[childId].inner.use.is_glob) {
            // if it is a glob, collapse all the children on to the parent
            console.log(apiJson.index[dereferencedId].inner.module.items)
            apiJson.index[dereferencedId].inner.module.items.forEach((childId) => {
              if (!processedItems.has(childId) && !isModuleItem(apiJson.index[childId])) {
                // TODO: we need to check if the child item is a use item or not - recursive logic broke
                finalChildIds.push(childId);
                processedItems.add(childId);
              } else if (!processedItems.has(childId) && isModuleItem(apiJson.index[childId])) {
                resultLines.siblingModule[childId] = processModule(
                  apiJson.index[childId],
                  parentModule,
                );
                processedItems.add(childId);
              }
            });
            processedItems.add(dereferencedId);
          } else {
            resultLines.siblingModule[dereferencedId] = processModule(
              apiJson.index[dereferencedId],
              parentModule,
            );
            processedItems.add(dereferencedId);
          }
        }
      } else if (dereferencedId in apiJson.paths) {
        // Handle external modules re-exported into this scope
        if (apiJson.index[childId].inner.use.is_glob) {
          getModuleChildIdsByPath(apiJson.paths[dereferencedId].path.join("::"), apiJson).forEach(
            (childId) => {
              if (!processedItems.has(childId)) {
                finalChildIds.push(childId);
                processedItems.add(childId);
              }
            },
          );
        } else if (apiJson.paths[dereferencedId].kind === ItemKind.Module) {
          resultLines.siblingModule[dereferencedId] = processModuleReexport(
            dereferencedId,
            apiJson.paths[dereferencedId],
            apiJson,
            parentModule,
          );
        } else {
          finalChildIds.push(childId);
        }
        processedItems.add(dereferencedId);
      }
    } else {
      // treat it as a simple use item
      finalChildIds.push(childId);
      processedItems.add(dereferencedId);
    }
  }

  for (const childId of finalChildIds) {
    if (!(childId in apiJson.index) && childId in apiJson.paths) {
      resultLines.children[childId] = [createItemLineFromPath(childId, apiJson.paths[childId])];
    }
    resultLines.children[childId] = processItem(apiJson.index[childId]) || [];
  }
  return resultLines;
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

  const apiJson = getAPIJson();
  const allChildIds = item.inner.module.items;
  const nonModuleChildIds = [];
  const siblingModuleLines: { [id: Id]: ReviewLine[] } = {};
  for (const childId of allChildIds) {
    if (isModuleItem(apiJson.index[childId])) {
      siblingModuleLines[childId] = processModule(apiJson.index[childId], {
        id: item.id,
        prefix: fullName,
      });
    } else {
      nonModuleChildIds.push(childId);
    }
  }

  let typicalChildren: {
    siblingModule: {
      [id: number]: ReviewLine[];
    };
    children: {
      [id: number]: ReviewLine[];
    };
  } = { siblingModule: {}, children: {} };
  if (nonModuleChildIds.length > 0) {
    typicalChildren = processNonModuleChildren(
      apiJson,
      { id: item.id, prefix: fullName },
      nonModuleChildIds,
    );
    // Take the keys from the children object, sort using getSortedChildren
    // and then push to reviewLine.Children in that order
    if (Object.keys(typicalChildren.children).length > 0) {
      const sortedChildIds = getSortedChildIds(
        Object.keys(typicalChildren.children).map((key) => parseInt(key)),
      );
      for (const childId of sortedChildIds.nonModule) {
        reviewLine.Children.push(...typicalChildren.children[childId]);
      }
    }
    // Append siblingModuleLines and typicalChildren.siblingModule
    for (const childId of Object.keys(typicalChildren.siblingModule)) {
      siblingModuleLines[childId] = typicalChildren.siblingModule[childId];
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
    Object.keys(siblingModuleLines).map((key) => parseInt(key)),
  );
  for (const childId of sortedSiblingModuleIds.module) {
    reviewLines.push(...siblingModuleLines[childId]);
  }

  return reviewLines;
}
