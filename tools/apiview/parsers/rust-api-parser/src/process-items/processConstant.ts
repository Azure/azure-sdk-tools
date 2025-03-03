import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isConstantItem } from "./utils/typeGuards";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";

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
  reviewLine.Tokens.push(...typeToReviewTokens(item.inner.constant.type));
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "=",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.Text,
    Value: item.inner.constant.const.expr || "null",
    HasSuffixSpace: false,
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });
  if (item.inner.constant.const.value) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "//",
    });
    reviewLine.Tokens.push({
      Kind: TokenKind.Text,
      Value: item.inner.constant.const.value,
    });
  }
  reviewLines.push(reviewLine);
  return reviewLines;
}
