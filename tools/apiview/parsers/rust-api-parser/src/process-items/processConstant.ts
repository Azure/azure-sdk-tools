import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isConstantItem } from "./utils/typeGuards";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { lineIdMap } from "../utils/lineIdUtils";

export function processConstant(item: Item) {
  if (!isConstantItem(item)) return;
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

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
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_const",
    HasSuffixSpace: false,
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name,
    RenderClasses: ["interface"],
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ":",
  });
  reviewLine.Tokens.push(...typeToReviewTokens(item.inner.constant.type));
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: " =",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.Text,
    Value: item.inner.constant.const.expr || "unknown_const_expr",
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
  lineIdMap.set(item.id.toString(), `const_${item.name}`);
  return reviewLines;
}
