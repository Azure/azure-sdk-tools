import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isConstantItem } from "./utils/typeGuards";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { createContentBasedLineId } from "../utils/lineIdUtils";

export function processConstant(item: Item, lineIdPrefix: string = "") {
  if (!isConstantItem(item)) return;

  // Build tokens first
  const tokens = [
    {
      Kind: TokenKind.Keyword,
      Value: "pub const",
    },
    {
      Kind: TokenKind.MemberName,
      Value: item.name || "unknown_const",
      HasSuffixSpace: false,
      NavigateToId: item.id.toString(), // Will be updated in post-processing
    },
    {
      Kind: TokenKind.Punctuation,
      Value: ":",
    },
    ...typeToReviewTokens(item.inner.constant.type),
    {
      Kind: TokenKind.Punctuation,
      Value: " =",
    },
    {
      Kind: TokenKind.Text,
      Value: item.inner.constant.const.expr || "unknown_const_expr",
      HasSuffixSpace: false,
    },
    {
      Kind: TokenKind.Punctuation,
      Value: ";",
    }
  ];

  if (item.inner.constant.const.value) {
    tokens.push(
      {
        Kind: TokenKind.Punctuation,
        Value: "//",
      },
      {
        Kind: TokenKind.Text,
        Value: item.inner.constant.const.value,
      }
    );
  }

  // Create content-based LineId from tokens
  const contentBasedLineId = createContentBasedLineId(tokens, lineIdPrefix, item.id.toString());

  // Create docs with content-based LineId
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item, contentBasedLineId) : [];

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: contentBasedLineId,
    Tokens: tokens,
    Children: [],
  };

  reviewLines.push(reviewLine);
  return reviewLines;
}
