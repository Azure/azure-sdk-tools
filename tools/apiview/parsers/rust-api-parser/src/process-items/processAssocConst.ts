import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { isAssocConstItem } from "./utils/typeGuards";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes an associated constant item and returns ReviewLine objects.
 *
 * @param {Item} item - The associated constant item to process.
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processAssocConst(item: Item): ReviewLine[] | null {
  if (!isAssocConstItem(item)) return null;
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  // Add const keyword
  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "const",
  });

  // Add name
  reviewLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_assoc_const",
    HasSuffixSpace: false,
    RenderClasses: ["interface"],
  });

  // Add colon
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ":",
  });

  reviewLine.Tokens.push(...typeToReviewTokens(item.inner.assoc_const.type));

  if (item.inner.assoc_const.value) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: " =",
    });

    reviewLine.Tokens.push({
      Kind: TokenKind.Text,
      Value: item.inner.assoc_const.value,
      HasSuffixSpace: false,
    });
  }

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

  reviewLines.push(reviewLine);
  lineIdMap.set(item.id.toString(), `const_${item.name}`);
  return reviewLines;
}
