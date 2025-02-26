import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processImpl } from "./processImpl";
import { processStructField } from "./processStructField";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { processGenerics } from "./utils/processGenerics";
import { isUnionItem } from "./utils/typeGuards";

/**
 * Processes a union item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The union item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processUnion(item: Item, apiJson: Crate): ReviewLine[] {
  if (!isUnionItem(item)) return [];
  const reviewLines: ReviewLine[] = [];

  if (item.docs) {
    reviewLines.push(createDocsReviewLine(item));
  }

  // Process derives and impls
  let implResult: ImplProcessResult = {
    deriveTokens: [],
    implBlock: null,
    closingBrace: null,
    traitImpls: [],
  };
  if (item.inner.union && item.inner.union.impls) {
    implResult = processImpl({ ...item, inner: { union: item.inner.union } }, apiJson);
  }

  const unionLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  // Add derive tokens if present
  unionLine.Tokens.push(...implResult.deriveTokens);

  unionLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub union",
  });

  unionLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: item.name || "null",
    RenderClasses: ["union"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
  });

  // Add generics if present
  if (item.inner.union.generics) {
    const genericsTokens = processGenerics(item.inner.union.generics);
    unionLine.Tokens.push(...genericsTokens);
  }

  unionLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
  });

  // Process fields
  if (item.inner.union.fields) {
    item.inner.union.fields.forEach((fieldId: number) => {
      const fieldItem = apiJson.index[fieldId];
      if (fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner) {
        unionLine.Children.push({
          LineId: fieldItem.id.toString(),
          Tokens: [
            {
              Kind: TokenKind.Keyword,
              Value: "pub",
            },
            {
              Kind: TokenKind.MemberName,
              Value: fieldItem.name || "null",
              HasSuffixSpace: false,
            },
            {
              Kind: TokenKind.Punctuation,
              Value: ":",
            },
            processStructField(fieldItem.inner.struct_field),
          ],
        });
      }
    });
  }

  reviewLines.push(unionLine);
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
  // TODO: check if has_stripped_fields needs to be considered for rendering
}
