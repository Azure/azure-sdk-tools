import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processAutoTraitImpls, processImpl } from "./processImpl";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";

export function processEnum(item: Item, apiJson: Crate): ReviewLine[] {
  if (!(typeof item.inner === "object" && "enum" in item.inner)) return [];
  const reviewLines: ReviewLine[] = [];

  if (item.docs) {
    reviewLines.push(createDocsReviewLine(item));
  }

  const enumLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  // TODO: generics, has_stripped_variants, 
  // Process derives and impls
  let implResult: ImplProcessResult = {
    deriveTokens: [],
    implBlock: null,
    closingBrace: null,
    traitImpls: [],
  };
  if (item.inner.enum && item.inner.enum.impls) {
    implResult = processImpl({ ...item, inner: { enum: item.inner.enum } }, apiJson);
    enumLine.Tokens.push(...implResult.deriveTokens);
  }

  enumLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub enum",
  });

  enumLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: item.name || "null",
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
  });
  enumLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
  });

  // Process enum variants
  if (item.inner.enum.variants) {
    enumLine.Children = item.inner.enum.variants.map((variant: number) => {
      const variantItem = apiJson.index[variant];
      return {
        LineId: variantItem.id.toString(),
        Tokens: [
          {
            Kind: TokenKind.TypeName,
            Value: variantItem.name || "null",
            NavigateToId: variantItem.id.toString(),
            NavigationDisplayName: variantItem.name || undefined,
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
    reviewLines.push(implResult.implBlock);
    reviewLines.push(implResult.closingBrace);
  }
  if(implResult.traitImpls.length>0) {reviewLines.push(...implResult.traitImpls);}
  return reviewLines;
}
