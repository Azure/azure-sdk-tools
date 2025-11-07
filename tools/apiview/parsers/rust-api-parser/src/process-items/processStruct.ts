import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processImpl } from "./processImpl";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isStructItem } from "./utils/typeGuards";
import { processStructField } from "./processStructField";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { getAPIJson } from "../main";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a struct item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The struct item to process.
 * @param {string} lineIdPrefix - The prefix from ancestors for hierarchical LineId
 */
export function processStruct(item: Item, lineIdPrefix: string = ""): ReviewLine[] {
  if (!isStructItem(item)) return [];
  const apiJson = getAPIJson();

  // Create initial placeholder docs (will be updated with correct LineId after struct tokens are generated)
  const reviewLines: ReviewLine[] = [];

  // Process derives and impls
  let implResult: ImplProcessResult = { deriveTokens: [], implBlock: [], traitImpls: [] };
  if (item.inner.struct.impls) {
    implResult = processImpl(item, lineIdPrefix);
  }

  const structLine: ReviewLine = {
    Tokens: [],
    Children: [],
  };

  if (implResult.deriveTokens.length > 0) {
    const deriveTokensLine: ReviewLine = {
      Tokens: implResult.deriveTokens,
    };
    reviewLines.push(deriveTokensLine);
  }

  structLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub struct",
  });

  structLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_struct",
    RenderClasses: ["class"],
    NavigateToId: item.id.toString(), // Will be updated in post-processing
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });

  // Create content-based LineId from the tokens and set it
  const contentBasedLineId = createContentBasedLineId(structLine.Tokens, lineIdPrefix, item.id.toString());
  structLine.LineId = contentBasedLineId;

  // Add documentation with correct RelatedToLine
  if (item.docs) {
    const docsLines = createDocsReviewLines(item, contentBasedLineId);
    reviewLines.unshift(...docsLines);
  }

  // Set RelatedToLine for derive tokens
  if (implResult.deriveTokens.length > 0) {
    reviewLines[reviewLines.length - 1].RelatedToLine = contentBasedLineId;
  }

  const genericsTokens = processGenerics(item.inner.struct.generics);
  // Add generics params if present
  if (item.inner.struct.generics) {
    structLine.Tokens.push(...genericsTokens.params);
  }

  // Add generics where clauses if present
  if (item.inner.struct.generics) {
    structLine.Tokens.push(...genericsTokens.wherePredicates);
  }

  // Process struct fields
  if (
    typeof item.inner.struct.kind === "object" &&
    "plain" in item.inner.struct.kind &&
    item.inner.struct.kind.plain.fields
  ) {
    structLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "{",
      HasSuffixSpace: false,
      HasPrefixSpace: true,
    });
    item.inner.struct.kind.plain.fields.forEach((fieldId: number) => {
      const fieldItem = apiJson.index[fieldId];
      if (fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner) {
        if (!structLine.Children) {
          structLine.Children = [];
        }

        structLine.Children.push(processStructField(fieldItem, contentBasedLineId));
      }
    });

    if (structLine.Children.length == 0) {
      structLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: "}",
      });
      reviewLines.push(structLine);
    } else {
      reviewLines.push(structLine);
      reviewLines.push({
        RelatedToLine: contentBasedLineId,
        Tokens: [
          {
            Kind: TokenKind.Punctuation,
            Value: "}",
          },
        ],
      });
    }
  } else if (
    typeof item.inner.struct.kind === "object" &&
    "tuple" in item.inner.struct.kind &&
    item.inner.struct.kind.tuple
  ) {
    structLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "(",
      HasSuffixSpace: false,
    });
    const tuple = item.inner.struct.kind.tuple;
    tuple.forEach((fieldId: number, index: number) => {
      const fieldItem = apiJson.index[fieldId];
      if (fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner) {
        if (index > 0) {
          structLine.Tokens.push({
            Kind: TokenKind.Punctuation,
            Value: ",",
          });
        }
        structLine.Tokens.push({
          Kind: TokenKind.Keyword,
          Value: "pub",
        });
        structLine.Tokens.push(...typeToReviewTokens(fieldItem.inner.struct_field));
      }
    });

    structLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: ")",
    });
    reviewLines.push(structLine);
  } else {
    // "unit" struct kind
    structLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "{",
      HasSuffixSpace: false,
    });
    structLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "}",
    });
    reviewLines.push(structLine);
  }

  if (implResult.implBlock) {
    reviewLines.push(...implResult.implBlock);
  }
  if (implResult.traitImpls.length > 0) {
    reviewLines.push(...implResult.traitImpls);
  }
  return reviewLines;
}
