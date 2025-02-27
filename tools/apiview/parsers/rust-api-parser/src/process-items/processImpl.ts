import { ReviewLine, TokenKind, ReviewToken } from "../models/apiview-models";
import { Crate, Enum, Item, Struct, Union } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { typeToString } from "./utils/typeToString";
import { isImplItem } from "./utils/typeGuards";
import {
  isAutoDerivedImpl,
  isManualTraitImpl,
  isInherentImpl,
  getImplsFromItem,
  ImplItem,
} from "./utils/implTypeGuards";

export function processAutoTraitImpls(impls: number[], apiJson: Crate): ReviewToken[] {
  const traitImpls = (impls: number[], apiJson: Crate) =>
    impls
      .map((implId) => apiJson.index[implId] as ImplItem)
      .filter((implItem) => isImplItem(implItem) && isAutoDerivedImpl(implItem))
      .map<ReviewToken>((implItem) => ({
        Kind: TokenKind.TypeName,
        Value: implItem.inner.impl.trait!.name,
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
    .filter((implItem) => isImplItem(implItem) && isManualTraitImpl(implItem))
    .flatMap((implItem) => {
      const reviewLineForImpl: ReviewLine = {
        LineId: implItem.id.toString() + "_impl",
        Tokens: [
          { Kind: TokenKind.Keyword, Value: "impl" },
          { Kind: TokenKind.TypeName, Value: implItem.inner.impl.trait!.name },
          { Kind: TokenKind.Punctuation, Value: "for" },
          {
            Kind: TokenKind.TypeName,
            Value: typeToString(implItem.inner.impl.for),
          },
          { Kind: TokenKind.Punctuation, Value: "{" },
        ],
        Children: implItem.inner.impl.items
          .map((item) => processItem(apiJson.index[item], apiJson))
          .filter((item) => item != null)
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
    .filter((implItem) => isImplItem(implItem) && isInherentImpl(implItem))
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
  item:
    | (Item & { inner: { struct: Struct } })
    | (Item & { inner: { enum: Enum } })
    | (Item & { inner: { union: Union } }),
  apiJson: Crate,
): ImplProcessResult {
  const impls = getImplsFromItem(item);
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
    Children: processImpls(impls, apiJson).filter((item) => item != null),
  };

  const closingBrace: ReviewLine = {
    RelatedToLine: item.id.toString() + "_impl",
    Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
  };

  const traitImpls = processOtherTraitImpls(impls, apiJson);

  return { deriveTokens, implBlock, closingBrace, traitImpls };
  // TODO: generics unused
  // TODO: provided_trait_methods unused
  // TODO: trait
  // TODO: is_negative
}
