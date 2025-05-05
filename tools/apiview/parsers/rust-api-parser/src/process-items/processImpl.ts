import { ReviewLine, TokenKind, ReviewToken } from "../models/apiview-models";
import { Enum, Item, Struct, Union } from "../../rustdoc-types/output/rustdoc-types";
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
import { processGenericArgs, processGenerics } from "./utils/processGenerics";
import { getAPIJson } from "../main";
import { lineIdMap } from "../utils/lineIdUtils";

// Interface for the result of processing implementations
export interface ImplProcessResult {
  deriveTokens: ReviewToken[]; // Auto-derived trait implementations (#[derive(...)])
  implBlock: ReviewLine[]; // Inherent implementations (impl Type { ... })
  traitImpls: ReviewLine[]; // Manual trait implementations (impl Trait for Type { ... })
}

// Helper function to map impl IDs to their items and filter by type
function getFilteredImpls<T extends ImplItem>(
  impls: number[],
  filterFn: (item: ImplItem) => boolean,
): T[] {
  const apiJson = getAPIJson();
  return impls
    .map((implId) => apiJson.index[implId] as ImplItem)
    .filter((item) => isImplItem(item) && filterFn(item)) as T[];
}

// Process automatically derived trait implementations
export function processAutoTraitImpls(impls: number[]): ReviewToken[] {
  const traitItems = getFilteredImpls(impls, isAutoDerivedImpl);
  // Get all traits that have been derived
  const traits = traitItems.map<ReviewToken>((item) => ({
    Kind: TokenKind.MemberName,
    Value: item.inner.impl.trait!.name,
    HasSuffixSpace: false,
  }));
  if (!traits.length) return [];

  // Build the complete derive attribute with all derived traits
  return [
    // Start of the derive attribute
    { Kind: TokenKind.Punctuation, Value: "#[", HasSuffixSpace: false },
    { Kind: TokenKind.Keyword, Value: "derive", HasSuffixSpace: false },
    { Kind: TokenKind.Punctuation, Value: "(", HasSuffixSpace: false },
    // Add each trait name with commas between them
    ...traits.flatMap((token, index) => [
      token,
      ...(index < traits.length - 1 ? [{ Kind: TokenKind.Punctuation, Value: "," }] : []),
    ]),
    // Close the derive attribute
    { Kind: TokenKind.Punctuation, Value: ")]" },
    { Kind: TokenKind.Punctuation, Value: "\n", HasSuffixSpace: false },
  ];
}

// Process manually implemented trait implementations
function processOtherTraitImpls(impls: number[], prefixId: string): ReviewLine[] {
  const apiJson = getAPIJson();
  const traitImpls = getFilteredImpls(impls, isManualTraitImpl);
  return traitImpls.flatMap((implItem) => {
    if (!implItem.inner.impl.trait) return [];

    // Get the type that the trait is implemented for
    const parentName = typeToReviewTokens(implItem.inner.impl.for);

    const lineId = implItem.id.toString() + "_" + prefixId;
    // Process provided trait methods that are mentioned but not explicitly implemented
    const providedTraitMethods: ReviewLine[] = implItem.inner.impl.provided_trait_methods.map(
      (method) => {
        return {
          Tokens: [
            { Kind: TokenKind.Keyword, Value: "pub" },
            { Kind: TokenKind.Keyword, Value: "fn" },
            {
              Kind: TokenKind.MemberName,
              Value: method,
              RenderClasses: ["method"],
              HasSuffixSpace: false,
            },
            { Kind: TokenKind.Punctuation, Value: ";" },
            {
              Kind: TokenKind.Comment,
              Value: "// provided trait method",
              HasSuffixSpace: false,
            },
          ],
          RelatedToLine: lineId,
        };
      },
    );
    const implGenerics = processGenerics(implItem.inner.impl.generics);
    // Create the main impl line with trait name and type
    const reviewLineForImpl: ReviewLine = {
      LineId: lineId,
      Tokens: [
        {
          Kind: TokenKind.Keyword,
          Value: (implItem.inner.impl.is_unsafe ? "unsafe " : "") + "impl",
          HasSuffixSpace: false,
        },
        ...implGenerics.params,
        {
          Kind: TokenKind.MemberName,
          Value: (implItem.inner.impl.is_negative ? "!" : "") + implItem.inner.impl.trait.name,
          HasPrefixSpace: true,
          HasSuffixSpace: false,
          NavigateToId: lineId,
          // Create navigation display name by combining trait and type names
          NavigationDisplayName:
            implItem.inner.impl.trait.name + "_" + parentName.map((token) => token.Value).join(""),
          RenderClasses: ["interface"],
        },
        // Add any generic arguments the trait might have
        ...processGenericArgs(implItem.inner.impl.trait.args),
        { Kind: TokenKind.Punctuation, Value: "for", HasPrefixSpace: true },
        // Add the type the trait is implemented for
        ...parentName,
        ...implGenerics.wherePredicates,
        { Kind: TokenKind.Punctuation, Value: "{", HasPrefixSpace: true },
      ],
      // Add both implemented methods and provided methods as children
      Children: [
        ...implItem.inner.impl.items
          .map((item) => processItem(apiJson.index[item]))
          .filter((item) => item != null)
          .flat(),
        ...providedTraitMethods,
      ],
    };

    lineIdMap.set(
      lineId,
      prefixId +
        reviewLineForImpl.Tokens.map((token) => token.Value)
          .join("_")
          .replace(/[^a-zA-Z0-9]+/g, ""),
    );

    const closingLine: ReviewLine = {
      RelatedToLine: lineId,
      Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
    };

    return [reviewLineForImpl, closingLine];
  });
}

// Process inherent implementations
function processImpls(impls: number[]): ReviewLine[] {
  const apiJson = getAPIJson();
  const inherentImpls = getFilteredImpls(impls, isInherentImpl);

  return (
    inherentImpls
      // Process each implementation's items and flatten the results
      .flatMap((implItem) =>
        implItem.inner.impl.items
          // Process each item within the impl
          .map((item) => processItem(apiJson.index[item]))
          // Flatten nested arrays
          .flat(),
      )
  );
}

// Main function to process implementations for a given item
export function processImpl(
  item:
    | (Item & { inner: { struct: Struct } })
    | (Item & { inner: { enum: Enum } })
    | (Item & { inner: { union: Union } }),
): ImplProcessResult {
  const linedId = item.id.toString() + "_impl";
  lineIdMap.set(linedId, lineIdMap.get(item.id.toString()) + "_impl");
  // Get all implementations associated with this item
  const impls = getImplsFromItem(item);

  // Process auto-derived trait implementations (like #[derive(Debug, Clone)])
  const deriveTokens = processAutoTraitImpls(impls);

  // Process inherent method implementations (like impl Type { fn method() {} })
  // Process children first to check if they're empty
  const children = processImpls(impls).filter((item) => item != null);

  let implBlock: ReviewLine[] = [];
  if (children.length > 0) {
    // Only create an implBlock if there are children
    implBlock = [
      // Create the main implementation line with type name
      {
        LineId: linedId,
        Tokens: [
          { Kind: TokenKind.Keyword, Value: "impl" },
          {
            Kind: TokenKind.MemberName,
            Value: item.name || "unknown_impl",
            RenderClasses: ["interface"],
            NavigateToId: linedId,
            NavigationDisplayName: item.name || undefined,
          },
          { Kind: TokenKind.Punctuation, Value: "{" },
        ],
        Children: children,
      },
      {
        RelatedToLine: linedId,
        Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
      },
    ];
  }

  // Process manual trait implementations (like impl Trait for Type { ... })
  const traitImpls = processOtherTraitImpls(impls, item.name);

  return { deriveTokens, implBlock, traitImpls };
}
