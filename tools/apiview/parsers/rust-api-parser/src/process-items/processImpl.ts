import { ReviewLine, TokenKind, ReviewToken } from "../models/apiview-models";
import { Item, Crate, Impl, Struct } from "../models/rustdoc-json-types";
import { processItem } from "./processItem";

type ImplItem = Omit<Item, "inner"> & { inner: { impl: Impl } };
type StructItem = Omit<Item, "inner"> & { inner: { struct: Struct } };

export function processTraitImpls(impls: number[], apiJson: Crate, reviewLine: ReviewLine) {
  const traitImpls = (impls: number[], apiJson: Crate) =>
    impls
      .map((implId) => apiJson.index[implId] as ImplItem)
      .filter(
        (implItem) =>
          typeof implItem?.inner == "object" &&
          "impl" in implItem?.inner &&
          implItem.inner.impl.blanket_impl === null &&
          implItem.inner.impl.trait &&
          implItem.attrs.includes("#[automatically_derived]"),
      )
      .map<ReviewToken>((implItem) => ({
        Kind: TokenKind.TypeName,
        Value: implItem.inner.impl.trait.name,
        RenderClasses: ["trait"],
        HasSuffixSpace: false,
      }));

  const addTokens = (tokens: ReviewToken[]) => {
    reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: "#[", HasSuffixSpace: false });
    reviewLine.Tokens.push({ Kind: TokenKind.Keyword, Value: "derive", HasSuffixSpace: false });
    reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: "(", HasSuffixSpace: false });
    tokens.forEach((token, index) => {
      reviewLine.Tokens.push(token);
      if (index < tokens.length - 1) {
        reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: "," });
      }
    });
    reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: ")]" });
    reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: "\n", HasSuffixSpace: false });
  };

  const traits = traitImpls(impls, apiJson);
  if (traits.length) addTokens(traits);
}

function processNonTraitImpls(impls: number[], apiJson: Crate, reviewLine: ReviewLine) {
  const nonTraitImpls = (impls: number[], apiJson: Crate): void =>
    impls
      .map((implId) => apiJson.index[implId] as ImplItem)
      .filter(
        (implItem) =>
          implItem?.inner &&
          "impl" in implItem.inner &&
          implItem.inner.impl.blanket_impl === null &&
          implItem.inner.impl.trait === null,
      )
      .forEach((implItem) => {
        const items = implItem.inner.impl.items;
        if (!reviewLine.Children.length) reviewLine.Children = [];
        items.forEach((item) => {
          reviewLine.Children.push(...processItem(apiJson, apiJson.index[item]));
        });
      });

  nonTraitImpls(impls, apiJson);
}

export function processImpl(item: StructItem, apiJson: Crate, reviewLine: ReviewLine) {
  processTraitImpls(item.inner.struct.impls, apiJson, reviewLine);
  processNonTraitImpls(item.inner.struct.impls, apiJson, reviewLine);
}
