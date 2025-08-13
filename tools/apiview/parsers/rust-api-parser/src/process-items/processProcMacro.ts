import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isProcMacroItem } from "./utils/typeGuards";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes a procedural macro item and returns ReviewLine objects.
 *
 * @param {Item} item - The procedural macro item to process.
 * @returns {ReviewLine[] | null} The ReviewLine objects or null if processing fails.
 */
export function processProcMacro(item: Item): ReviewLine[] | null {
  if (!isProcMacroItem(item)) return null;

  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  lineIdMap.set(item.id.toString(), item.name || "unknown_proc_macro");
  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  // Add proc-macro attribute
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "#[proc_macro",
  });

  // Add proc macro type if available
  const procMacro = item.inner.proc_macro;
  if (procMacro && procMacro.kind) {
    switch (procMacro.kind) {
      case "derive":
        reviewLine.Tokens.push({
          Kind: TokenKind.Text,
          Value: "_derive",
        });
        break;
      case "attr":
        reviewLine.Tokens.push({
          Kind: TokenKind.Text,
          Value: "_attr",
        });
      case "bang":
        reviewLine.Tokens.push({
          Kind: TokenKind.Text,
          Value: "_bang",
        });
      // Handle other types if needed
    }
  }

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "]",
  });

  // Add pub and fn keywords
  const functionLine: ReviewLine = {
    Tokens: [
      {
        Kind: TokenKind.Keyword,
        Value: "pub fn",
      },
      {
        Kind: TokenKind.Text,
        Value: item.name || "unknown",
        HasSuffixSpace: false,
      },
      {
        Kind: TokenKind.Punctuation,
        Value: item.inner.proc_macro.helpers.toString(),
      },
    ],
    Children: [],
    RelatedToLine: reviewLine.LineId,
  };

  reviewLines.push(reviewLine);
  reviewLines.push(functionLine);

  return reviewLines;
}
