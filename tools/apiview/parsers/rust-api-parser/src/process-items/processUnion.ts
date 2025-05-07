import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processImpl } from "./processImpl";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isUnionItem } from "./utils/typeGuards";
import { processStructField } from "./processStructField";
import { getAPIJson } from "../main";
import { lineIdMap } from "../utils/lineIdUtils";

/**
 * Processes a union item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The union item to process.
 */
export function processUnion(item: Item): ReviewLine[] {
  if (!isUnionItem(item)) return [];
  const apiJson = getAPIJson();
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  lineIdMap.set(item.id.toString(), `union_${item.name}`);
  // Process derives and impls
  let implResult: ImplProcessResult;
  if (item.inner.union && item.inner.union.impls) {
    implResult = processImpl({ ...item, inner: { union: item.inner.union } });
  }

  const unionLine: ReviewLine = {
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

  unionLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub union",
  });

  unionLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_union_name",
    RenderClasses: ["struct"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });

  const genericsTokens = processGenerics(item.inner.union.generics);
  // Add generics params if present
  if (item.inner.union.generics) {
    unionLine.Tokens.push(...genericsTokens.params);
  }

  // Add generics where clauses if present
  if (item.inner.union.generics) {
    unionLine.Tokens.push(...genericsTokens.wherePredicates);
  }

  unionLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
    HasPrefixSpace: true,
  });

  // Process fields
  if (item.inner.union.fields) {
    item.inner.union.fields.forEach((fieldId: number) => {
      const fieldItem = apiJson.index[fieldId];
      if (fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner) {
        unionLine.Children.push(processStructField(fieldItem));
      }
    });
  }

  reviewLines.push(unionLine);
  reviewLines.push({
    RelatedToLine: item.id.toString(),
    Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
  });

  if (implResult.implBlock) {
    reviewLines.push(...implResult.implBlock);
  }
  if (implResult.traitImpls.length > 0) {
    reviewLines.push(...implResult.traitImpls);
  }
  return reviewLines;
}
