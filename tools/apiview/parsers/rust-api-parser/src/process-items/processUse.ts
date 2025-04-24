import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item, ItemKind } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { addExternalReferencesIfNotExists, createItemLineFromPath, getModuleChildIdsByPath, processModuleReexport } from "./utils/externalReexports";
import { replaceCratePath } from "./utils/cratePathUtils";
import { AnnotatedReviewLines } from "./utils/models";
import { getAPIJson } from "../main";
import { processItem } from "./processItem";
import { processModule } from "./processModule";

export function processUse(item: Item, parentModule: { prefix: string; id: number } | undefined): AnnotatedReviewLines {
  const annotatedReviewLines: AnnotatedReviewLines = {
    siblingModule: {},
    children: {},
  };

  if (!isUseItem(item)) return annotatedReviewLines;
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];
  const apiJson = getAPIJson()
  const dereferencedId = item.inner.use.id;
  if (item.inner.use.is_glob) {
    // case 1: [ glob = true; module = true; in index ] - collapse all the children on to the parent
    if ((dereferencedId in apiJson.index) && isModuleItem(apiJson.index[dereferencedId])) {
      // isModuleItem check is not needed, being extra careful
      const moduleItems = apiJson.index[dereferencedId].inner.module.items;
      moduleItems.forEach((childId) => {
        if (isModuleItem(apiJson.index[childId])) {
          annotatedReviewLines.siblingModule[childId] = processModule(apiJson.index[childId], /* TODO: add parent info */);
        } else if (!isUseItem(apiJson.index[childId])) {
          annotatedReviewLines.children[childId] = processItem(apiJson.index[childId]);
        } else {
          const useReviewLines = processUse(apiJson.index[childId], parentModule);
          annotatedReviewLines.siblingModule[childId] = useReviewLines.siblingModule[childId];
          annotatedReviewLines.children[childId] = useReviewLines.children[childId];
        }
      });
    }
    // case 2: [ glob = true; module = true; in paths ] - collapse all the children on to the parent
    else if (dereferencedId in apiJson.paths && (apiJson.paths[dereferencedId].kind === ItemKind.Module)) {
      const moduleChildIds = getModuleChildIdsByPath(apiJson.paths[dereferencedId].path.join("::"), apiJson);
      moduleChildIds.forEach((childId) => {
        annotatedReviewLines.children[childId] = [createItemLineFromPath(childId, apiJson.paths[childId])];
      });
    }
  }
  // case 3: [ glob = false; module = true; in index ] - sibling module
  else if ((dereferencedId in apiJson.index) && (isModuleItem(apiJson.index[dereferencedId]))) {
    annotatedReviewLines.siblingModule[dereferencedId] = processModule(apiJson.index[dereferencedId], parentModule);
  }
  // case 4: [ glob = false; module = true; in paths ] - sibling module with a mention of re-export
  else if ((dereferencedId in apiJson.paths) && (apiJson.paths[dereferencedId].kind === ItemKind.Module)) {
    annotatedReviewLines.siblingModule[dereferencedId] = processModuleReexport(dereferencedId, apiJson.paths[dereferencedId], apiJson, parentModule);
  }
  // case 5: [ glob = false; module = false; in index or paths ] - simple use item
  else if (dereferencedId in apiJson.index) {
    let useValue = item.inner.use.source || "null";
    annotatedReviewLines.children[item.id] = [{
      Tokens: [{
        Kind: TokenKind.Keyword,
        Value: "pub use",
      }, {
        Kind: TokenKind.Text,
        Value: item.inner.use.name
      },
      {
        Kind: TokenKind.Punctuation,
        Value: "=",
      }, {
        Kind: TokenKind.TypeName,
        Value: replaceCratePath(useValue),
        RenderClasses: ["dependencies"],
        NavigateToId: dereferencedId.toString(),
        NavigationDisplayName: item.inner.use.name,
      }]
    }]
  }
  addExternalReferencesIfNotExists(dereferencedId);
  return annotatedReviewLines;
}
