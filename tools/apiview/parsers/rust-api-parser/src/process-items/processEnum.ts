import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processImpl } from "./processImpl";
import { createDocsReviewLines } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isEnumItem } from "./utils/typeGuards";
import { getAPIJson } from "../main";
import { lineIdMap } from "../utils/lineIdUtils";

export function processEnum(item: Item): ReviewLine[] {
  if (!isEnumItem(item)) return [];
  const apiJson = getAPIJson();
  const reviewLines: ReviewLine[] = item.docs ? createDocsReviewLines(item) : [];

  lineIdMap.set(item.id.toString(), `enum_${item.name}`);
  // Process derives and impls
  let implResult: ImplProcessResult;
  if (item.inner.enum.impls) {
    implResult = processImpl({ ...item, inner: { enum: item.inner.enum } });
  }

  const enumLine: ReviewLine = {
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

  enumLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub enum",
  });

  enumLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "unknown_enum",
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
    RenderClasses: ["enum"],
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

  // Process enum variants
  if (item.inner.enum.variants) {
    enumLine.Children = item.inner.enum.variants.map((variant: number) => {
      const variantItem = apiJson.index[variant];
      lineIdMap.set(variantItem.id.toString(), `variant_${variantItem.name}`);
      return {
        LineId: variantItem.id.toString(),
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
