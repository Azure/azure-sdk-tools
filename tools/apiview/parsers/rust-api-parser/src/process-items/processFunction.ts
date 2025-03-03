import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item, Type } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isFunctionItem } from "./utils/typeGuards";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";

/**
 * Processes a function item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The function item to process.
 */
export function processFunction(item: Item) {
  if (!isFunctionItem(item)) return;
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

  const genericsTokens = processGenerics(item.inner.function.generics);
  // Add generics params if present
  if (item.inner.function.generics) {
    reviewLine.Tokens.push(...genericsTokens.params);
  }

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
        reviewLine.Tokens.push(...typeToReviewTokens(input[1]));
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
    reviewLine.Tokens.push(...typeToReviewTokens(item.inner.function.sig.output));
  }

  // Add generics where clauses if present
  if (item.inner.function.generics) {
    reviewLine.Tokens.push(...genericsTokens.wherePredicates);
  }

  if (item.inner.function.has_body) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "{}",
      HasSuffixSpace: false,
      HasPrefixSpace: true,
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
