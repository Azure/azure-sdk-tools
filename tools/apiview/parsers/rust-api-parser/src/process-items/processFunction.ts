import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item, Type, FunctionHeader } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isFunctionItem } from "./utils/typeGuards";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes the function header and adds modifiers and ABI information to the tokens
 *
 * @param {FunctionHeader} header - The function header containing const, unsafe, async and ABI information
 * @param {ReviewLine} reviewLine - The review line to add tokens to
 */
function processFunctionHeader(header: FunctionHeader, reviewLine: ReviewLine) {
  if (header.is_const) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Keyword,
      Value: "const",
    });
  }

  // Add async before unsafe (correct Rust syntax order)
  if (header.is_async) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Keyword,
      Value: "async",
    });
  }

  if (header.is_unsafe) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Keyword,
      Value: "unsafe",
    });
  }

  // Add ABI if it's not the default Rust ABI
  if (header.abi !== "Rust") {
    reviewLine.Tokens.push({
      Kind: TokenKind.Keyword,
      Value: "extern",
    });

    // Format the ABI string based on the type
    let abiString = Object.keys(header.abi)[0];

    reviewLine.Tokens.push({
      Kind: TokenKind.StringLiteral,
      Value: `"${abiString}"`,
      HasSuffixSpace: true,
    });
  }
}

/**
 * Processes a function item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The function item to process.
 * @param {string} lineIdPrefix - The prefix for hierarchical line IDs.
 */
export function processFunction(item: Item, lineIdPrefix: string = ""): ReviewLine[] {
  if (!isFunctionItem(item)) return [];

  // Build tokens first
  const tokens = [];

  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub",
  });

  // Process function header modifiers and ABI
  const tempReviewLine = { Tokens: [] as any[] };
  processFunctionHeader(item.inner.function.header, tempReviewLine);
  tokens.push(...tempReviewLine.Tokens);

  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "fn",
  });

  tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_fn",
    HasSuffixSpace: false,
    RenderClasses: ["method"],
    NavigateToId: item.id.toString(), // Will be updated in post-processing
  });

  const genericsTokens = processGenerics(item.inner.function.generics);
  // Add generics params if present
  if (item.inner.function.generics) {
    tokens.push(...genericsTokens.params);
  }

  // Process function parameters
  tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "(",
    HasSuffixSpace: false,
    HasPrefixSpace: false,
  });

  // Add function parameters
  if (item.inner.function.sig.inputs.length > 0) {
    item.inner.function.sig.inputs.forEach((input: [string, Type], index: number) => {
      if (index > 0) {
        tokens.push({
          Kind: TokenKind.Punctuation,
          Value: ", ",
          HasSuffixSpace: false,
        });
      }

      if (input[0] === "self") {
        tokens.push({
          Kind: TokenKind.StringLiteral,
          Value: `&${input[0]}`,
          HasSuffixSpace: false,
        });
      } else {
        tokens.push({
          Kind: TokenKind.StringLiteral,
          Value: input[0],
          HasSuffixSpace: false,
        });

        tokens.push({
          Kind: TokenKind.Punctuation,
          Value: ": ",
          HasSuffixSpace: false,
        });
        tokens.push(...typeToReviewTokens(input[1]));
      }
    });
  }

  tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ")",
    HasPrefixSpace: false,
    HasSuffixSpace: false,
  });

  // Add return type if present
  if (item.inner.function.sig.output) {
    tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "->",
      HasPrefixSpace: true,
    });
    tokens.push(...typeToReviewTokens(item.inner.function.sig.output));
  }

  // Add generics where clauses if present
  if (item.inner.function.generics) {
    tokens.push(...genericsTokens.wherePredicates);
  }

  if (item.inner.function.has_body) {
    tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "{}",
      HasSuffixSpace: false,
      HasPrefixSpace: true,
    });
  } else {
    tokens.push({
      Kind: TokenKind.Punctuation,
      Value: ";",
      HasSuffixSpace: false,
    });
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
