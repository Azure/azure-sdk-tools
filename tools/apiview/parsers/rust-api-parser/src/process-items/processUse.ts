import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isUseItem } from "./utils/typeGuards";

export function processUse(item: Item): ReviewLine[] | undefined {
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
  });

  reviewLines.push(reviewLine);
  return reviewLines;
}
