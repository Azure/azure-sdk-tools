import { ReviewLine, TokenKind, ReviewToken } from "../models/apiview-models";
import { Item, Crate, Impl, Struct, Union } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { typeToString } from "./utils/typeToString";

type ImplItem = Omit<Item, "inner"> & { inner: { impl: Impl } };
type StructItem = Omit<Item, "inner"> & { inner: { struct: Struct } };
type UnionItem = Omit<Item, "inner"> & { inner: { union: Union } };

export function processAutoTraitImpls(impls: number[], apiJson: Crate, reviewLine: ReviewLine) {
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

function processOtherTraitImpls(impls: number[], apiJson: Crate, reviewLine: ReviewLine) {
  impls
    .map((implId) => apiJson.index[implId] as ImplItem)
    .filter(
      (implItem) =>
        typeof implItem?.inner == "object" &&
        "impl" in implItem?.inner &&
        implItem.inner.impl.blanket_impl === null &&
        implItem.inner.impl.trait &&
        !implItem.attrs.includes("#[automatically_derived]"),
    )
    .map((implItem) => {
      const reviewLineForImpl: ReviewLine = {
        LineId: implItem.id.toString() + "_impl",
        Tokens: [
          { Kind: TokenKind.Keyword, Value: "impl" },
          { Kind: TokenKind.TypeName, Value: implItem.inner.impl.trait.name },
          { Kind: TokenKind.Punctuation, Value: "for" },
          {
            Kind: TokenKind.TypeName,
            Value: typeToString(implItem.inner.impl.for),
          },
          { Kind: TokenKind.Punctuation, Value: "{" },
        ],
        Children: implItem.inner.impl.items
          .map((item) => processItem(apiJson.index[item], apiJson))
          .flat(),
      };
      reviewLine.Children.push(reviewLineForImpl);
      reviewLine.Children.push({
        RelatedToLine: implItem.id.toString() + "_impl",
        Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
      });
    });
}

function processImpls(impls: number[], apiJson: Crate, reviewLine: ReviewLine) {
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
      if (!reviewLine.Children.length) reviewLine.Children = [];
      implItem.inner.impl.items.forEach((item) => {
        reviewLine.Children.push(...processItem(apiJson.index[item], apiJson));
      });
    });
}

export function processImpl(item: StructItem |UnionItem, apiJson: Crate, reviewLine: ReviewLine) {
  if ('struct' in item.inner) {
    processAutoTraitImpls(item.inner.struct.impls, apiJson, reviewLine);
  } else if ('union' in item.inner) {
    processAutoTraitImpls(item.inner.union.impls, apiJson, reviewLine);
  }
  // Create the ReviewLine object
  if (!reviewLine.Children.length) reviewLine.Children = [];
  const reviewLineForImpl: ReviewLine = {
    LineId: item.id.toString() + "_impl",
    Tokens: [
      {
        Kind: TokenKind.Keyword,
        Value: "impl",
      },
      {
        Kind: TokenKind.TypeName,
        Value: item.name || "null",
        RenderClasses: ["impl"],
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined,
      },
      {
        Kind: TokenKind.Punctuation,
        Value: "{",
      },
    ],
    Children: [],
  };
  reviewLine.Children.push(reviewLineForImpl);

  if ('struct' in item.inner) {
    processImpls(item.inner.struct.impls, apiJson, reviewLineForImpl);
  } else if ('union' in item.inner) {
    processImpls(item.inner.union.impls, apiJson, reviewLineForImpl);
  }

  reviewLine.Children.push({
    RelatedToLine: item.id.toString() + "_impl",
    Tokens: [
      {
        Kind: TokenKind.Punctuation,
        Value: "}",
      },
    ],
  });

  // TODO: Decide if we want to process other trait impls
  // processOtherTraitImpls(item.inner.struct.impls, apiJson, reviewLine);
}
