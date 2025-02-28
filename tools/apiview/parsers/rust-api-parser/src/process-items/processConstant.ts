import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { typeToString } from "./utils/typeToString";
import { isConstantItem } from "./utils/typeGuards";

export function processConstant(item: Item) {
  if (!isConstantItem(item)) return;
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
    Value: "pub const",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.Text,
    Value: item.name || "null",
    HasSuffixSpace: false,
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ":",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: typeToString(item.inner.constant.type),
    // TODO: const is unused
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

  reviewLines.push(reviewLine);
  return reviewLines;
}
