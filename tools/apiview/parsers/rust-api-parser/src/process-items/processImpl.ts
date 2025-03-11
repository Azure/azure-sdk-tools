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
import { processGenericArgs } from "./utils/processGenerics";
import { getAPIJson } from "../main";

// Process automatically derived trait implementations
export function processAutoTraitImpls(impls: number[]): ReviewToken[] {
  const apiJson = getAPIJson();

  // Helper function that transforms impl IDs to ReviewTokens
  const traitImpls = (impls: number[]) =>
    impls
      // each impl ID to its corresponding item
      .map((implId) => apiJson.index[implId] as ImplItem)
      // Filter for only auto-derived trait implementations
      .filter((implItem) => isImplItem(implItem) && isAutoDerivedImpl(implItem))
      // Create a token for each trait that's been derived
      .map<ReviewToken>((implItem) => ({
        Kind: TokenKind.TypeName,
        Value: implItem.inner.impl.trait!.name,
        RenderClasses: ["tname", "trait"],
        HasSuffixSpace: false,
      }));

  // Get all traits that have been derived
  const traits = traitImpls(impls);
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
function processOtherTraitImpls(impls: number[]): ReviewLine[] {
  const apiJson = getAPIJson();

  return (
    impls
      // impl IDs to corresponding items
      .map((implId) => apiJson.index[implId] as ImplItem)
      // Filter for only manual trait implementations
      .filter((implItem) => isImplItem(implItem) && isManualTraitImpl(implItem))
      .flatMap((implItem) => {
        if (!implItem.inner.impl.trait) return [];

        // Get the type that the trait is implemented for
        const parentName = typeToReviewTokens(implItem.inner.impl.for);

        // Process provided trait methods that are mentioned but not explicitly implemented
        const providedTraitMethods: ReviewLine[] = implItem.inner.impl.provided_trait_methods.map(
          (method) => {
            return {
              LineId: implItem.id.toString() + "_impl_" + method,
              Tokens: [
                { Kind: TokenKind.Keyword, Value: "pub" },
                { Kind: TokenKind.Keyword, Value: "fn" },
                {
                  Kind: TokenKind.MemberName,
                  Value: method,
                  RenderClasses: ["tname", "method"],
                  HasSuffixSpace: false,
                },
                { Kind: TokenKind.Punctuation, Value: ";" },
                {
                  Kind: TokenKind.Punctuation,
                  Value: "// provided trait method",
                  HasSuffixSpace: false,
                },
              ],
            };
          },
        );

        // Create the main impl line with trait name and type
        const reviewLineForImpl: ReviewLine = {
          LineId: implItem.id.toString() + "_impl",
          Tokens: [
            { Kind: TokenKind.Keyword, Value: "impl" },
            {
              Kind: TokenKind.TypeName,
              Value: implItem.inner.impl.trait.name,
              HasSuffixSpace: false,
              NavigateToId: implItem.id.toString() + "_impl",
              // Create navigation display name by combining trait and type names
              NavigationDisplayName:
                implItem.inner.impl.trait.name +
                "_" +
                parentName.map((token) => token.Value).join(""),
            },
            // Add any generic arguments the trait might have
            ...processGenericArgs(implItem.inner.impl.trait.args),
            { Kind: TokenKind.Punctuation, Value: "for", HasPrefixSpace: true },
            // Add the type the trait is implemented for
            ...parentName,
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

        const closingLine: ReviewLine = {
          RelatedToLine: implItem.id.toString() + "_impl",
          Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
        };

        return [reviewLineForImpl, closingLine];
      })
  );
}

// Process inherent implementations
function processImpls(impls: number[]): ReviewLine[] {
  const apiJson = getAPIJson();

  return (
    impls
      // impl IDs to corresponding items
      .map((implId) => apiJson.index[implId] as ImplItem)
      // Filter for only inherent implementations (not trait implementations)
      .filter((implItem) => isImplItem(implItem) && isInherentImpl(implItem))
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

// Interface for the result of processing implementations
export interface ImplProcessResult {
  deriveTokens: ReviewToken[]; // Auto-derived trait implementations (#[derive(...)])
  implBlock: ReviewLine[]; // Inherent implementations (impl Type { ... })
  traitImpls: ReviewLine[]; // Manual trait implementations (impl Trait for Type { ... })
}

// Main function to process implementations for a given item
export function processImpl(
  item:
    | (Item & { inner: { struct: Struct } })
    | (Item & { inner: { enum: Enum } })
    | (Item & { inner: { union: Union } }),
): ImplProcessResult {
  // Get all implementations associated with this item
  const impls = getImplsFromItem(item);

  // Process auto-derived trait implementations (like #[derive(Debug, Clone)])
  const deriveTokens = processAutoTraitImpls(impls);

  // Process inherent method implementations (like impl Type { fn method() {} })
  // Process children first to check if they're empty
  const children = processImpls(impls).filter((item) => item != null);

  // Only create an implBlock if there are children
  const implBlock: ReviewLine[] =
    children.length === 0
      ? [] // Empty implBlock if no children
      : [
          // Create the main implementation line with type name
          {
            LineId: item.id.toString() + "_impl",
            Tokens: [
              { Kind: TokenKind.Keyword, Value: "impl" },
              {
                Kind: TokenKind.TypeName,
                Value: item.name || "null",
                RenderClasses: ["tname"],
                NavigateToId: item.id.toString() + "_impl",
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

  // Process manual trait implementations (like impl Trait for Type { ... })
  const traitImpls = processOtherTraitImpls(impls);

  return { deriveTokens, implBlock, traitImpls };
}
