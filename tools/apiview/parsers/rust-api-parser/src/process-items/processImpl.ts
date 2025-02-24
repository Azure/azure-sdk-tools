import { ReviewLine, TokenKind, ReviewToken } from "../models/apiview-models";
import { Item, Crate, Impl, Struct, Union, Enum } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { typeToString } from "./utils/typeToString";

type ImplItem = Omit<Item, "inner"> & { inner: { impl: Impl } };
type StructItem = Omit<Item, "inner"> & { inner: { struct: Struct } };
type UnionItem = Omit<Item, "inner"> & { inner: { union: Union } };
type EnumItem = Omit<Item, "inner"> & { inner: { enum: Enum } };

export function processAutoTraitImpls(impls: number[], apiJson: Crate): ReviewToken[] {
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

  const traits = traitImpls(impls, apiJson);
  if (!traits.length) return [];

  return [
    { Kind: TokenKind.Punctuation, Value: "#[", HasSuffixSpace: false },
    { Kind: TokenKind.Keyword, Value: "derive", HasSuffixSpace: false },
    { Kind: TokenKind.Punctuation, Value: "(", HasSuffixSpace: false },
    ...traits.flatMap((token, index) => [
      token,
      ...(index < traits.length - 1 ? [{ Kind: TokenKind.Punctuation, Value: "," }] : []),
    ]),
    { Kind: TokenKind.Punctuation, Value: ")]" },
    { Kind: TokenKind.Punctuation, Value: "\n", HasSuffixSpace: false },
  ];
}

function processOtherTraitImpls(impls: number[], apiJson: Crate): ReviewLine[] {
  return impls
    .map((implId) => apiJson.index[implId] as ImplItem)
    .filter(
      (implItem) =>
        typeof implItem?.inner == "object" &&
        "impl" in implItem?.inner &&
        implItem.inner.impl.blanket_impl === null &&
        implItem.inner.impl.trait &&
        !implItem.attrs.includes("#[automatically_derived]"),
    )
    .flatMap((implItem) => {
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
          .map((item) => processItem(apiJson.index[item], apiJson)).filter(item => item != null)
          .flat(),
      };

      const closingLine: ReviewLine = {
        RelatedToLine: implItem.id.toString() + "_impl",
        Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
      };

      return [reviewLineForImpl, closingLine];
    });
}

function processImpls(impls: number[], apiJson: Crate): ReviewLine[] {
  return impls
    .map((implId) => apiJson.index[implId] as ImplItem)
    .filter(
      (implItem) =>
        implItem?.inner &&
        "impl" in implItem.inner &&
        implItem.inner.impl.blanket_impl === null &&
        implItem.inner.impl.trait === null,
    )
    .flatMap((implItem) =>
      implItem.inner.impl.items.map((item) => processItem(apiJson.index[item], apiJson)).flat(),
    );
}

export interface ImplProcessResult {
  deriveTokens: ReviewToken[];
  implBlock: ReviewLine;
  closingBrace: ReviewLine;
  traitImpls: ReviewLine[];
}

export function processImpl(
  item: StructItem | UnionItem | EnumItem,
  apiJson: Crate,
): ImplProcessResult {
  const impls =
    "struct" in item.inner
      ? item.inner.struct.impls
      : "union" in item.inner
        ? item.inner.union.impls
        : item.inner.enum.impls;

  const deriveTokens = processAutoTraitImpls(impls, apiJson);

  const implBlock: ReviewLine = {
    LineId: item.id.toString() + "_impl",
    Tokens: [
      { Kind: TokenKind.Keyword, Value: "impl" },
      {
        Kind: TokenKind.TypeName,
        Value: item.name || "null",
        RenderClasses: ["impl"],
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined,
      },
      { Kind: TokenKind.Punctuation, Value: "{" },
    ],
    Children: processImpls(impls, apiJson),
  };

  const closingBrace: ReviewLine = {
    RelatedToLine: item.id.toString() + "_impl",
    Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
  };

  const traitImpls = processOtherTraitImpls(impls, apiJson);

  return { deriveTokens, implBlock, closingBrace, traitImpls };
}
