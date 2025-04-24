import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isUseItem } from "./utils/typeGuards";
import { addExternalReferencesIfNotExists } from "./utils/externalReexports";
import { replaceCratePath } from "./utils/cratePathUtils";

export function processUse(item: Item): ReviewLine[] | undefined {
  if (!isUseItem(item)) return;
  const useItemId = item.inner.use.id;
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
