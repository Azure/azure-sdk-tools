import { ReviewLine, ReviewToken, TokenKind } from "../models/apiview-models";
import { Item, ItemKind } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import {
  registerExternalItemReference,
  createItemLineFromPath,
  getModuleChildIdsByPath,
  processModuleReexport,
} from "./utils/externalReexports";
import { replaceCratePath } from "./utils/pathUtils";
import { AnnotatedReviewLines } from "./utils/models";
import { getAPIJson, processedItems } from "../main";
import { processItem } from "./processItem";
import { processModule } from "./processModule";
import { lineIdMap } from "../utils/lineIdUtils";

function processSimpleUseItem(item: Item): AnnotatedReviewLines {
  const annotatedReviewLines: AnnotatedReviewLines = {
    siblingModule: {},
    children: {},
  };

  if (!isUseItem(item)) return annotatedReviewLines;

  const dereferencedId = item.inner.use.id;
  const tokens: ReviewToken[] = [
    {
      Kind: TokenKind.Keyword,
      Value: "pub use",
    },
  ];

  if (item.inner.use.is_glob) {
    tokens.push(
      {
        Kind: TokenKind.TypeName,
        Value: item.inner.use.name,
        RenderClasses: ["dependencies"],
        NavigateToId: dereferencedId.toString(),
        NavigationDisplayName: item.inner.use.name,
        HasSuffixSpace: false,
      },
      {
        Kind: TokenKind.Punctuation,
        Value: "::*",
      },
    );
  } else {
    const useValue = item.inner.use.source || "unknown_use_source";
    tokens.push(
      {
        Kind: TokenKind.Text,
        Value: item.inner.use.name,
      },
      {
        Kind: TokenKind.Punctuation,
        Value: "=",
      },
      {
        Kind: TokenKind.TypeName,
        Value: replaceCratePath(useValue),
        RenderClasses: ["dependencies"],
        NavigateToId: dereferencedId.toString(),
        NavigationDisplayName: item.inner.use.name,
      },
    );
  }
  lineIdMap.set(item.id.toString(), item.inner.use.name);
  annotatedReviewLines.children[item.id] = [{ LineId: item.id.toString(), Tokens: tokens }];
  return annotatedReviewLines;
}

/**
 * Processes a use item and returns the annotated review lines.
 *
 * @param {Item} item - The use item to process.
 * @param {Object} parentModule - The parent module information.
 * @returns {AnnotatedReviewLines} The annotated review lines for the use item.
 */
export function processUse(
  item: Item,
  parentModule: { prefix: string; id: number } | undefined,
): AnnotatedReviewLines {
  let annotatedReviewLines: AnnotatedReviewLines = {
    siblingModule: {},
    children: {},
  };

  if (!isUseItem(item)) return annotatedReviewLines;
  const docsLines = item.docs ? createDocsReviewLines(item) : [];
  const apiJson = getAPIJson();
  const dereferencedId = item.inner.use.id;
  if (item.inner.use.is_glob) {
    if (processedItems.has(dereferencedId)) {
      // case 0: This item has already been processed, so we only add a reference.
      annotatedReviewLines = processSimpleUseItem(item);
    }
    // case 1: [ glob = true; module = true; in index ] - collapse all the children on to the parent
    else if (dereferencedId in apiJson.index && isModuleItem(apiJson.index[dereferencedId])) {
      // isModuleItem check is not needed, added for clarity
      const moduleItems = apiJson.index[dereferencedId].inner.module.items;
      moduleItems.forEach((childId) => {
        if (isModuleItem(apiJson.index[childId])) {
          annotatedReviewLines.siblingModule[childId] = processModule(
            apiJson.index[childId],
            parentModule,
          );
          processedItems.add(childId);
        } else if (!isUseItem(apiJson.index[childId])) {
          annotatedReviewLines.children[childId] = processItem(apiJson.index[childId]);
          processedItems.add(childId);
        } else {
          const useReviewLines = processUse(apiJson.index[childId], parentModule);
          for (const key in useReviewLines.siblingModule) {
            annotatedReviewLines.siblingModule[key] = useReviewLines.siblingModule[key];
          }
          for (const key in useReviewLines.children) {
            annotatedReviewLines.children[key] = useReviewLines.children[key];
          }
        }
      });
    }
    // case 2: [ glob = true; module = true; in paths ] - collapse all the children on to the parent
    else if (
      dereferencedId in apiJson.paths &&
      apiJson.paths[dereferencedId].kind === ItemKind.Module
    ) {
      const moduleChildIds = getModuleChildIdsByPath(
        apiJson.paths[dereferencedId].path.join("::"),
        apiJson,
      );
      moduleChildIds.forEach((childId) => {
        annotatedReviewLines.children[childId] = [
          createItemLineFromPath(childId, apiJson.paths[childId]),
        ];
      });
    }
  }
  // case 3: [ glob = false; module = true; in index ] - sibling module
  else if (dereferencedId in apiJson.index && isModuleItem(apiJson.index[dereferencedId])) {
    annotatedReviewLines.siblingModule[dereferencedId] = processModule(
      apiJson.index[dereferencedId],
      parentModule,
    );
  }
  // case 4: [ glob = false; module = true; in paths ] - sibling module with a mention of re-export
  else if (
    dereferencedId in apiJson.paths &&
    apiJson.paths[dereferencedId].kind === ItemKind.Module
  ) {
    annotatedReviewLines.siblingModule[dereferencedId] = processModuleReexport(
      dereferencedId,
      apiJson.paths[dereferencedId],
      apiJson,
      parentModule,
    );
  }
  // case 5: [ glob = false; module = false; in index ]
  else if (dereferencedId in apiJson.index) {
    // case 5.1: Has not been processed yet, so we process it.
    if (!processedItems.has(dereferencedId)) {
      annotatedReviewLines.children[dereferencedId] = processItem(apiJson.index[dereferencedId]);
      processedItems.add(dereferencedId);
    }
    // case 5.2: Already expanded, so we just add a reference.
    else {
      annotatedReviewLines = processSimpleUseItem(item);
    }
  }
  // case 6: [ glob = false; module = false; in paths ] - simple use item
  else if (dereferencedId in apiJson.paths) {
    annotatedReviewLines = processSimpleUseItem(item);
  }
  registerExternalItemReference(dereferencedId);
  return annotatedReviewLines;
}
