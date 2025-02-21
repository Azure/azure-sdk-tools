import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { ImplProcessResult, processImpl } from "./processImpl";
import { processStructField } from "./processStructField";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";

/**
 * Processes a struct item and adds its documentation to the ReviewLine.
 *
 * @param {Crate} apiJson - The API JSON object containing all items.
 * @param {Item} item - The struct item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processStruct(item: Item, apiJson: Crate): ReviewLine[] {
  if (!(typeof item.inner === "object" && "struct" in item.inner)) return [];
  const reviewLines: ReviewLine[] = [];

  if (item.docs) {
    reviewLines.push(createDocsReviewLine(item));
  }

  const structLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  // Process derives and impls
  let implResult: ImplProcessResult = {
    deriveTokens: [],
    implBlock: null,
    closingBrace: null,
    traitImpls: [],
  };
  if (item.inner.struct && item.inner.struct.impls) {
    implResult = processImpl({ ...item, inner: { struct: item.inner.struct } }, apiJson);
    structLine.Tokens.push(...implResult.deriveTokens);
  }

  structLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub struct",
  });
  structLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: item.name || "null",
    RenderClasses: ["struct"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
  });

  structLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "{",
  });
  // fields
  if (
    typeof item.inner.struct.kind === "object" &&
    "plain" in item.inner.struct.kind &&
    item.inner.struct.kind.plain.fields
  ) {
    item.inner.struct.kind.plain.fields.forEach((fieldId: number) => {
      const fieldItem = apiJson.index[fieldId];
      if (fieldItem && typeof fieldItem.inner === "object" && "struct_field" in fieldItem.inner) {
        if (!structLine.Children) {
          structLine.Children = [];
        }

        structLine.Children.push({
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

  if (implResult.implBlock) {
    reviewLines.push(implResult.implBlock);
    reviewLines.push(implResult.closingBrace);
  }
  if(implResult.traitImpls.length>0) {reviewLines.push(...implResult.traitImpls);}
  return reviewLines;
}
