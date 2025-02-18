import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";

/**
 * Processes a struct item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The struct item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processUse(item: Item) {
  if (!(typeof item.inner === "object" && "use" in item.inner)) return;
  const reviewLines: ReviewLine[] = [];
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

  // Create the ReviewLine object
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
