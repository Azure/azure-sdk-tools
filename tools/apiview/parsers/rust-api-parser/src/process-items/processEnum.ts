import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processImpl } from "./processImpl";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isEnumItem } from "./utils/typeGuards";
import { getAPIJson } from "../main";
import { createContentBasedLineId } from "../utils/lineIdUtils";

export function processEnum(item: Item, lineIdPrefix: string = ""): ReviewLine[] {
  if (!isEnumItem(item)) return [];
  const apiJson = getAPIJson();

  // Create initial placeholder docs (will be updated with correct LineId after enum tokens are generated)
  const reviewLines: ReviewLine[] = [];

  // Process derives and impls
  let implResult: ImplProcessResult = { deriveTokens: [], implBlock: [], traitImpls: [] };
  if (item.inner.enum.impls) {
    implResult = processImpl({ ...item, inner: { enum: item.inner.enum } }, lineIdPrefix);
  }

  if (implResult.deriveTokens.length > 0) {
    const deriveTokensLine: ReviewLine = {
      Tokens: implResult.deriveTokens,
    };
    reviewLines.push(deriveTokensLine);
  }

  const enumLine: ReviewLine = {
    Tokens: [],
    Children: [],
  };

  enumLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub enum",
  });

  enumLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_enum",
    RenderClasses: ["enum"],
    NavigateToId: item.id.toString(), // Will be updated in post-processing
    NavigationDisplayName: item.name || undefined,
    HasSuffixSpace: false,
  });

  const genericsTokens = processGenerics(item.inner.enum.generics);
  // Add generics params if present
  if (item.inner.enum.generics) {
    enumLine.Tokens.push(...genericsTokens.params);
  }

  // Add generics where clauses if present
  if (item.inner.enum.generics) {
    enumLine.Tokens.push(...genericsTokens.wherePredicates);
  }

  enumLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
    HasPrefixSpace: true,
  });

  // Create content-based LineId from the tokens
  const contentBasedLineId = createContentBasedLineId(enumLine.Tokens, lineIdPrefix, item.id.toString());
  enumLine.LineId = contentBasedLineId;

  // Add documentation with correct RelatedToLine
  if (item.docs) {
    const docsLines = createDocsReviewLines(item, contentBasedLineId);
    reviewLines.unshift(...docsLines);
  }

  // Set RelatedToLine for derive tokens
  if (implResult.deriveTokens.length > 0) {
    reviewLines[reviewLines.length - 1].RelatedToLine = contentBasedLineId;
  }

  // Process enum variants
  if (item.inner.enum.variants) {
    enumLine.Children = item.inner.enum.variants.map((variant: number) => {
      const variantItem = apiJson.index[variant];
      return {
        LineId: createContentBasedLineId(
          [{ Kind: TokenKind.Text, Value: variantItem.name || "unknown_variant" }],
          contentBasedLineId,
          variantItem.id.toString()
        ),
        Tokens: [
          {
            Kind: TokenKind.Text,
            Value: variantItem.name || "unknown_variant",
            HasSuffixSpace: false,
          },
          {
            Kind: TokenKind.Punctuation,
            Value: ",",
            HasSuffixSpace: false,
          },
        ],
      };
    });
  }

  reviewLines.push(enumLine);
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
