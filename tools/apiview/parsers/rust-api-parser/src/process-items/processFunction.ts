import { ReviewLine, ReviewToken, TokenKind } from "../models/apiview-models";
import { Item, Type } from "../../rustdoc-types/output/rustdoc-types";
import { processStructField } from "./processStructField";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { typeToString } from "./utils/typeToString";

/**
 * Processes a function item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The function item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processFunction(item: Item) {
  if (!(typeof item.inner === "object" && "function" in item.inner)) return;
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
    Value: "pub fn",
  });
  
  reviewLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "null",
    HasSuffixSpace: false,
    RenderClasses: ["method"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
  });

  // Add generics and where clauses if present
  const genericsTokens = processGenerics(item.inner.function.generics);
  reviewLine.Tokens.push(...genericsTokens);

  // Process function parameters
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "(",
    HasSuffixSpace: false,
    HasPrefixSpace: false,
  });

  // TODO: function header is unused
  // Add function parameters
  if (item.inner.function.sig.inputs.length > 0) {
    item.inner.function.sig.inputs.forEach((input: [string, Type], index: number) => {
      if (index > 0) {
        reviewLine.Tokens.push({
          Kind: TokenKind.Punctuation,
          Value: ", ",
          HasSuffixSpace: false,
        });
      }

      if (input[0] === "self") {
        reviewLine.Tokens.push({
          Kind: TokenKind.StringLiteral,
          Value: input[0],
          HasSuffixSpace: false,
        });
      } else {
        reviewLine.Tokens.push({
          Kind: TokenKind.StringLiteral,
          Value: input[0],
          HasSuffixSpace: false,
        });

        reviewLine.Tokens.push({
          Kind: TokenKind.Punctuation,
          Value: ": ",
          HasSuffixSpace: false,
        });
        const token = processStructField(input[1]);
        reviewLine.Tokens.push(token);
      }
    });
  }

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ")",
    HasPrefixSpace: false,
  });

  // Add return type if present
  if (item.inner.function.sig.output) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "->",
    });
    reviewLine.Tokens.push({
      Kind: TokenKind.TypeName,
      Value: typeToString(item.inner.function.sig.output),
    });
  }

  if (item.inner.function.has_body) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "{}",
      HasSuffixSpace: false,
    });
  } else {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: ";",
      HasSuffixSpace: false,
    });
  }
  reviewLines.push(reviewLine);
  return reviewLines;
}