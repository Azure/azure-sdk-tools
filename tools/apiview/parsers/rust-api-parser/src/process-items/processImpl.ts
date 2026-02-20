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
import { createContentBasedLineId } from "../utils/lineIdUtils";
import { getPath } from "./utils/pathUtils";

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
  currentItem: Item,
): T[] {
  const apiJson = getAPIJson();
  return impls
    .map((implId) => apiJson.index[implId] as ImplItem)
    .filter((item) => isImplItem(item) && filterFn(item)) as T[];
}

// Process automatically derived trait implementations
export function processAutoTraitImpls(impls: number[], currentItem: Item): ReviewToken[] {
  const traitItems = getFilteredImpls(impls, isAutoDerivedImpl, currentItem);
  // Get all traits that have been derived
  const traits = traitItems.map<ReviewToken>((item) => ({
    Kind: TokenKind.MemberName,
    Value: getPath(item.inner.impl.trait),
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
function processOtherTraitImpls(impls: number[], lineIdPrefix: string, currentItem: Item): ReviewLine[] {
  const apiJson = getAPIJson();
  const traitImpls = getFilteredImpls(impls, isManualTraitImpl, currentItem);

  // Track trait implementations to add sequence numbers for duplicates
  const traitImplCounts = new Map<string, number>();

  return traitImpls.flatMap((implItem) => {
    if (!implItem.inner.impl.trait) return [];

    // Get the type that the trait is implemented for
    const parentName = typeToReviewTokens(implItem.inner.impl.for);

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
          // RelatedToLine will be set after contentBasedLineId is created
        };
      },
    );
    const implGenerics = processGenerics(implItem.inner.impl.generics);
    const implTraitName =
      (implItem.inner.impl.is_negative ? "!" : "") + getPath(implItem.inner.impl.trait);

    // Build tokens first to generate the LineId
    const tokens = [
      {
        Kind: TokenKind.Keyword,
        Value: (implItem.inner.impl.is_unsafe ? "unsafe " : "") + "impl",
        HasSuffixSpace: false,
      },
      ...implGenerics.params,
      {
        Kind: TokenKind.MemberName,
        Value: implTraitName,
        HasPrefixSpace: true,
        HasSuffixSpace: false,
        NavigateToId: implItem.id.toString(), // Will be updated in post-processing
        RenderClasses: ["interface"],
      },
      // Add any generic arguments the trait might have
      ...processGenericArgs(implItem.inner.impl.trait.args),
      { Kind: TokenKind.Punctuation, Value: "for", HasPrefixSpace: true, HasSuffixSpace: true },
      // Add the type the trait is implemented for
      ...parentName,
      ...implGenerics.wherePredicates,
      { Kind: TokenKind.Punctuation, Value: "{", HasPrefixSpace: true },
    ];

    // Create content-based LineId from the tokens  
    // Include context of which item is being processed to distinguish duplicates
    const contextualId = `${currentItem.id}_${implItem.id}`;
    const contextName = currentItem.name || "unknown_item";
    let contextualLineIdPrefix = lineIdPrefix ? `${lineIdPrefix}.for_${contextName}` : `for_${contextName}`;

    // Add sequence number for duplicate trait implementations
    const traitKey = `${implTraitName}_for_${parentName.map(t => t.Value).join('')}`;
    const count = (traitImplCounts.get(traitKey) || 0) + 1;
    traitImplCounts.set(traitKey, count);
    if (count > 1) {
      contextualLineIdPrefix = `${contextualLineIdPrefix}.impl${count}`;
    }

    const contentBasedLineId = createContentBasedLineId(tokens, contextualLineIdPrefix, contextualId);

    // Update providedTraitMethods to use the content-based LineId
    providedTraitMethods.forEach(method => {
      method.RelatedToLine = contentBasedLineId;
    });

    // Create the main impl line with trait name and type
    const reviewLineForImpl: ReviewLine = {
      LineId: contentBasedLineId,
      Tokens: tokens,
      // Add both implemented methods and provided methods as children
      Children: [
        ...implItem.inner.impl.items
          .map((item) => processItem(apiJson.index[item], undefined, contentBasedLineId))
          .filter((item) => item != null)
          .flat(),
        ...providedTraitMethods,
      ],
    };

    const tokenStringForDisplay = reviewLineForImpl.Tokens.slice(
      0,
      reviewLineForImpl.Tokens.length - 1,
    )
      .map(
        (token) =>
          (token.HasPrefixSpace ? " " : "") + token.Value + (token.HasSuffixSpace ? " " : ""),
      )
      .join("");
    const matchingToken = reviewLineForImpl.Tokens.find((token) => token.Value === implTraitName);
    if (matchingToken) {
      matchingToken.NavigationDisplayName = tokenStringForDisplay;
    }

    const closingLine: ReviewLine = {
      RelatedToLine: contentBasedLineId,
      Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
    };

    return [reviewLineForImpl, closingLine];
  });
}

// Process inherent implementations
function processImpls(impls: number[], lineIdPrefix: string = "", currentItem: Item): ReviewLine[] {
  const apiJson = getAPIJson();
  const inherentImpls = getFilteredImpls(impls, isInherentImpl, currentItem);

  return (
    inherentImpls
      // Process each implementation's items and flatten the results
      .flatMap((implItem) => {
        // Create the impl block lineId first
        // For inherent impls, extract type name from the 'for' field
        const forTokens = typeToReviewTokens(implItem.inner.impl.for);
        const typeName = forTokens.find(token => token.Kind === TokenKind.TypeName)?.Value ||
          forTokens.find(token => token.Value && token.Value.length > 0)?.Value ||
          "unknown_type";
        const implTokens = [
          { Kind: TokenKind.Keyword, Value: "impl" },
          {
            Kind: TokenKind.MemberName,
            Value: typeName,
            RenderClasses: ["interface"],
            NavigateToId: implItem.id.toString(),
          },
        ];
        // Include context of which item is being processed to distinguish duplicates
        const contextualId = `${currentItem.id}_${implItem.id}`;
        const contextName = currentItem.name || "unknown_item";
        const contextualLineIdPrefix = lineIdPrefix ? `${lineIdPrefix}.for_${contextName}` : `for_${contextName}`;
        const implLineId = createContentBasedLineId(implTokens, contextualLineIdPrefix, contextualId);

        return implItem.inner.impl.items
          // Process each item within the impl with the proper lineId prefix
          .map((item) => processItem(apiJson.index[item], undefined, implLineId))
          // Flatten nested arrays
          .flat();
      })
  );
}

// Main function to process implementations for a given item
export function processImpl(
  item:
    | (Item & { inner: { struct: Struct } })
    | (Item & { inner: { enum: Enum } })
    | (Item & { inner: { union: Union } }),
  lineIdPrefix: string = "",
): ImplProcessResult {
  // Get all implementations associated with this item
  const impls = getImplsFromItem(item);

  // Process auto-derived trait implementations (like #[derive(Debug, Clone)])
  const deriveTokens = processAutoTraitImpls(impls, item);

  // Process inherent method implementations (like impl Type { fn method() {} })
  // Process children first to check if they're empty
  const children = processImpls(impls, lineIdPrefix, item).filter((item) => item != null);

  let implBlock: ReviewLine[] = [];
  if (children.length > 0) {
    // Create tokens for the impl block
    const implTokens = [
      { Kind: TokenKind.Keyword, Value: "impl" },
      {
        Kind: TokenKind.MemberName,
        Value: item.name || "unknown_impl",
        RenderClasses: ["interface"],
        NavigateToId: item.id.toString(), // Will be updated in post-processing
        NavigationDisplayName: `impl ${item.name}`,
      },
      { Kind: TokenKind.Punctuation, Value: "{" },
    ];

    // Create content-based LineId from the tokens
    const implLineId = createContentBasedLineId(implTokens, lineIdPrefix, item.id.toString());

    // Only create an implBlock if there are children
    implBlock = [
      // Create the main implementation line with type name
      {
        LineId: implLineId,
        Tokens: implTokens,
        Children: children,
      },
      {
        RelatedToLine: implLineId,
        Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
      },
    ];
  }

  // Process manual trait implementations (like impl Trait for Type { ... })
  const traitImpls = processOtherTraitImpls(impls, lineIdPrefix, item);

  return { deriveTokens, implBlock, traitImpls };
}
