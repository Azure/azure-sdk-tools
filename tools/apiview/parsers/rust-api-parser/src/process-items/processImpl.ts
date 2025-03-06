import { ReviewLine, TokenKind, ReviewToken } from "../models/apiview-models";
import { Crate, Enum, Item, Struct, Union } from "../../rustdoc-types/output/rustdoc-types";
import { processItem } from "./processItem";
import { isImplItem } from "./utils/typeGuards";
import {
  isAutoDerivedImpl,
  isManualTraitImpl,
  isInherentImpl,
  getImplsFromItem,
  ImplItem,
} from "./utils/implTypeGuards";
import { typeToReviewTokens } from "./utils/typeToReviewTokens";
import { processGenericArgs } from "./utils/processGenerics";
import { getAPIJson } from "../main";

export function processAutoTraitImpls(impls: number[]): ReviewToken[] {
  const apiJson = getAPIJson();
  const traitImpls = (impls: number[]) =>
    impls
      .map((implId) => apiJson.index[implId] as ImplItem)
      .filter((implItem) => isImplItem(implItem) && isAutoDerivedImpl(implItem))
      .map<ReviewToken>((implItem) => ({
        Kind: TokenKind.TypeName,
        Value: implItem.inner.impl.trait!.name,
        RenderClasses: ["trait"],
        HasSuffixSpace: false,
      }));

  const traits = traitImpls(impls);
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

function processOtherTraitImpls(impls: number[]): ReviewLine[] {
  const apiJson = getAPIJson();
  return impls
    .map((implId) => apiJson.index[implId] as ImplItem)
    .filter((implItem) => isImplItem(implItem) && isManualTraitImpl(implItem))
    .flatMap((implItem) => {
      if (!implItem.inner.impl.trait) return [];
      const reviewLineForImpl: ReviewLine = {
        LineId: implItem.id.toString() + "_impl",
        Tokens: [
          { Kind: TokenKind.Keyword, Value: "impl" },
          {
            Kind: TokenKind.TypeName,
            Value: implItem.inner.impl.trait.name,
            HasSuffixSpace: false,
          },
          ...processGenericArgs(implItem.inner.impl.trait.args),
          { Kind: TokenKind.Punctuation, Value: "for", HasPrefixSpace: true },
          ...typeToReviewTokens(implItem.inner.impl.for),
          { Kind: TokenKind.Punctuation, Value: "{", HasPrefixSpace: true },
        ],
        Children: implItem.inner.impl.items
          .map((item) => processItem(apiJson.index[item]))
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

function processImpls(impls: number[]): ReviewLine[] {
  const apiJson = getAPIJson();
  return impls
    .map((implId) => apiJson.index[implId] as ImplItem)
    .filter((implItem) => isImplItem(implItem) && isInherentImpl(implItem))
    .flatMap((implItem) =>
      implItem.inner.impl.items.map((item) => processItem(apiJson.index[item])).flat(),
    );
}

export interface ImplProcessResult {
  deriveTokens: ReviewToken[];
  implBlock: ReviewLine[];
  traitImpls: ReviewLine[];
}

export function processImpl(
  item:
    | (Item & { inner: { struct: Struct } })
    | (Item & { inner: { enum: Enum } })
    | (Item & { inner: { union: Union } }),
): ImplProcessResult {
  const impls = getImplsFromItem(item);
  const deriveTokens = processAutoTraitImpls(impls);

  // Process children first to check if they're empty
  const children = processImpls(impls).filter((item) => item != null);

  // Only create an implBlock if there are children
  const implBlock: ReviewLine[] =
    children.length === 0
      ? [] // Empty implBlock if no children
      : [
          {
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
            Children: children,
          },
          {
            RelatedToLine: item.id.toString() + "_impl",
            Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
          },
        ];

  const traitImpls = processOtherTraitImpls(impls);

  return { deriveTokens, implBlock, traitImpls };
  // TODO: generics unused
  // TODO: provided_trait_methods unused
  // TODO: trait
  // TODO: is_negative
}
