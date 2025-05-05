import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processImpl } from "./processImpl";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isStructItem } from "./utils/typeGuards";
import { processStructField } from "./processStructField";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { getAPIJson } from "../main";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes a struct item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The struct item to process.
 */
export function processStruct(item: Item): ReviewLine[] {
  if (!isStructItem(item)) return [];
  const apiJson = getAPIJson();
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  lineIdMap.set(item.id.toString(), `struct_${item.name}`);
  // Process derives and impls
  let implResult: ImplProcessResult;
  if (item.inner.struct.impls) {
    implResult = processImpl(item);
  }

  const structLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  if (implResult.deriveTokens.length > 0) {
    const deriveTokensLine: ReviewLine = {
      Tokens: implResult.deriveTokens,
      RelatedToLine: item.id.toString(),
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
    RenderClasses: ["struct"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });

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

        structLine.Children.push(processStructField(fieldItem));
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
        RelatedToLine: item.id.toString(),
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
