import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { isTraitItem } from "./utils/typeGuards";
import { createGenericBoundTokens, processGenerics } from "./utils/processGenerics";
import { getAPIJson } from "../main";
import { createContentBasedLineId } from "../utils/lineIdUtils";

/**
 * Processes a trait item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The trait item to process.
 * @param {string} lineIdPrefix - The prefix for hierarchical line IDs.
 */
export function processTrait(item: Item, lineIdPrefix: string = ""): ReviewLine[] {
  if (!isTraitItem(item)) return [];
  const apiJson = getAPIJson();

  // Build tokens first
  const tokens = [];

  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub",
  });

  if (item.inner.trait.is_unsafe) {
    tokens.push({
      Kind: TokenKind.Keyword,
      Value: "unsafe",
    });
  }

  tokens.push({
    Kind: TokenKind.Keyword,
    Value: "trait",
  });
  tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_trait",
    RenderClasses: ["type"],
    NavigateToId: item.id.toString(), // Will be updated in post-processing
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });

  const genericsTokens = processGenerics(item.inner.trait.generics);
  // Add generics params if present
  if (item.inner.trait.generics) {
    tokens.push(...genericsTokens.params);
  }

  if (item.inner.trait.bounds) {
    const boundTokens = createGenericBoundTokens(item.inner.trait.bounds);
    if (boundTokens.length > 0) {
      tokens.push({ Kind: TokenKind.Text, Value: ":", HasPrefixSpace: false });
      tokens.push(...boundTokens);
    }
  }

  // Add generics where clauses if present
  if (item.inner.trait.generics) {
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

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: contentBasedLineId,
    Tokens: tokens,
    Children: [],
  };

  if (item.inner.trait.items) {
    item.inner.trait.items.forEach((associatedItem: number) => {
      if (!reviewLine.Children) reviewLine.Children = [];
      const childReviewLines = processItem(apiJson.index[associatedItem], undefined, contentBasedLineId);
      if (childReviewLines) reviewLine.Children.push(...childReviewLines);
    });
  }

  reviewLines.push(reviewLine);
  reviewLines.push({
    RelatedToLine: contentBasedLineId,
    Tokens: [
      {
        Kind: TokenKind.Punctuation,
        Value: "}",
      },
    ],
    IsContextEndLine: true,
  });
  return reviewLines;
}
