import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processImpl } from "./processImpl";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isUnionItem } from "./utils/typeGuards";
import { processStructField } from "./processStructField";
import { getAPIJson } from "../main";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a union item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The union item to process.
 * @param {string} lineIdPrefix - The prefix for hierarchical line IDs.
 */
export function processUnion(item: Item, lineIdPrefix: string = ""): ReviewLine[] {
  if (!isUnionItem(item)) return [];
  const apiJson = getAPIJson();

  // Process derives and impls
  let implResult: ImplProcessResult = { deriveTokens: [], implBlock: [], traitImpls: [] };
  if (item.inner.union && item.inner.union.impls) {
    implResult = processImpl({ ...item, inner: { union: item.inner.union } }, lineIdPrefix);
  }

  // Build tokens first
  const tokens = [];

  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub union",
  });

  tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_union_name",
    RenderClasses: ["class"],
    NavigateToId: item.id.toString(), // Will be updated in post-processing
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });

  const genericsTokens = processGenerics(item.inner.union.generics);
  // Add generics params if present
  if (item.inner.union.generics) {
    tokens.push(...genericsTokens.params);
  }

  // Add generics where clauses if present
  if (item.inner.union.generics) {
    tokens.push(...genericsTokens.wherePredicates);
  }

  tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
    HasPrefixSpace: true,
  });

  // Create content-based LineId from tokens
  const contentBasedLineId = createContentBasedLineId(tokens, lineIdPrefix, item.id.toString());

  // Create docs with content-based LineId
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item, contentBasedLineId) : [];

  // Add derive tokens if any
  if (implResult.deriveTokens.length > 0) {
    const deriveTokensLine: ReviewLine = {
      RelatedToLine: contentBasedLineId,
      Tokens: implResult.deriveTokens,
    };
    reviewLines.push(deriveTokensLine);
  }

  // Create the union line
  const unionLine: ReviewLine = {
    LineId: contentBasedLineId,
    Tokens: tokens,
    Children: [],
  };

  // Process fields
  if (item.inner.union.fields) {
    item.inner.union.fields.forEach((fieldId: number) => {
      const fieldItem = apiJson.index[fieldId];
      if (fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner) {
        unionLine.Children.push(processStructField(fieldItem, contentBasedLineId));
      }
    });
  }

  reviewLines.push(unionLine);
  reviewLines.push({
    RelatedToLine: contentBasedLineId,
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
