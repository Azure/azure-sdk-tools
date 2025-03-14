import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { isAssocTypeItem } from "./utils/typeGuards";
import { createGenericBoundTokens } from "./utils/processGenerics";

/**
 * Processes an associated type item and returns ReviewLine objects.
 *
 * @param {Item} item - The associated type item to process.
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processAssocType(item: Item): ReviewLine[] | null {
  if (!isAssocTypeItem(item)) return null;

  const reviewLines: ReviewLine[] = [];

  // Add documentation if available
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  // Add type keyword
  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "type",
  });

  // Add name
  reviewLine.Tokens.push({
    Kind: TokenKind.Text,
    Value: item.name || "unknown",
    HasSuffixSpace: false,
  });

  // Add bounds if available
  const assocType = item.inner.assoc_type;
  if (assocType && assocType.bounds && assocType.bounds.length > 0) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: ":",
    });

    reviewLine.Tokens.push(...createGenericBoundTokens(assocType.bounds));
  }

  if (assocType.type) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: " =",
    });
    reviewLine.Tokens.push(...typeToReviewTokens(assocType.type));
  }

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ";",
  });

  reviewLines.push(reviewLine);
  return reviewLines;
}
