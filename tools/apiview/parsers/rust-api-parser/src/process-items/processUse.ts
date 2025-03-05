import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { processItem } from "./processItem";
import { externalReexports } from "./utils/externalReexports";

export const reexportLines: {
  internal: ReviewLine[];
  external: ReviewLine[];
} = {
  internal: [],
  external: [],
};

export function processUse(item: Item, apiJson: Crate): ReviewLine[] | undefined {
  if (!isUseItem(item)) return;
  const reviewLines: ReviewLine[] = [];
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub use",
  });

  let useValue = item.inner.use.source || "null";
  if (item.inner.use.is_glob) {
    useValue += "::*";
  }

  reviewLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: useValue,
    RenderClasses: ["use"],
    NavigateToId: item.inner.use.id.toString(),
  });

  reviewLines.push(reviewLine);
  console.log("Processing use item", item.inner.use.id, item.inner.use.source);

  if (item.inner.use.id in apiJson.index) {
    reexportLines.internal.push(...processItem(apiJson.index[item.inner.use.id], apiJson));
    // for the re-exports in the crate
    // if (isModuleItem(apiJson.index[item.inner.use.id])) {
    //   reexportLines.internal.push(...processItem(apiJson.index[item.inner.use.id], apiJson));
    // }
    // else {
    //   // TODO: get the string that you'll get after stripping "item.inner.use.name" at the end of "item.inner.use.source"
    // }
  } else if (item.inner.use.id in apiJson.paths) {
    // for the re-exports in the external crates
    reexportLines.external.push(...externalReexports(apiJson.paths[item.inner.use.id], apiJson));
  }

  return reviewLines;
}