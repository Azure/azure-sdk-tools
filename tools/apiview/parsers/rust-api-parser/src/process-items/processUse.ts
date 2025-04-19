import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item, ItemKind } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { addExternalReferencesIfNotExists } from "./utils/externalReexports";
import { getAPIJson } from "../main";
import { replaceCratePath } from "./utils/cratePathUtils";

export function processUse(item: Item): ReviewLine[] | undefined {
  if (!isUseItem(item)) return;
  const apiJson = getAPIJson();
  // code path where use items are "modules" is handled in processModule
  const useItemId = item.inner.use.id;
  if (
    (useItemId in apiJson.index && isModuleItem(apiJson.index[useItemId])) ||
    (useItemId in apiJson.paths && apiJson.paths[useItemId].kind == ItemKind.Module)
  ) {
    return;
  }

  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  let useValue = item.inner.use.source || "null";

  // for all the non-module use items
  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub use",
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.Text,
    Value: item.inner.use.name,
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "=",
  });

  reviewLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: replaceCratePath(useValue),
    RenderClasses: ["dependencies"],
    NavigateToId: useItemId.toString(),
    NavigationDisplayName: item.inner.use.name,
  });

  reviewLines.push(reviewLine);
  addExternalReferencesIfNotExists(useItemId);

  return reviewLines;
}
