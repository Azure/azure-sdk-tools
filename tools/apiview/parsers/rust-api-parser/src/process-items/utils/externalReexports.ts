import { Crate, Id, ItemKind, ItemSummary } from "../../../rustdoc-types/output/rustdoc-types";
import { getAPIJson, processedItems } from "../../main";
import { ReviewLine, ReviewToken, TokenKind } from "../../models/apiview-models";
import { createContentBasedLineId, markAsExternal, lineIdMap, externalItemsMap } from "../../utils/lineIdUtils";

export const externalReferencesLines: ReviewLine[] = [];

/**
 * Registers external item references in the API view output.
 * This function checks if an item exists in the external paths collection and
 * adds it to the references list if it hasn't been processed yet.
 * @param itemId The ID of the item being used/re-exported.
 * @param lineIdPrefix The prefix for hierarchical line IDs.
 */
export function registerExternalItemReference(itemId: Id, lineIdPrefix: string = ""): void {
  const apiJson = getAPIJson();

  // Check if there's already an external item with this ID
  const hasExternalItemWithSameId = Array.from(lineIdMap.entries()).some(([lineId, originalId]) =>
    originalId === itemId.toString() && externalItemsMap.get(lineId) === true
  );

  if (
    processedItems.has(itemId) ||
    itemId in apiJson.index ||
    !(itemId in apiJson.paths) ||
    hasExternalItemWithSameId // Only skip if there's already an external item with same ID
  ) {
    return;
  }

  const itemIdString = itemId.toString();
  const itemSummary = apiJson.paths[itemId];
  if (itemSummary.path[itemSummary.path.length - 1].startsWith("__")) return;

  const transformedItemKind = transformItemKind(itemSummary.kind);
  const value = itemSummary.path.join("::");

  const tokens = [
    {
      Kind: TokenKind.Keyword,
      Value: "pub",
    },
    {
      Kind: TokenKind.Keyword,
      Value: transformedItemKind,
    },
    {
      Kind: TokenKind.TypeName,
      Value: value,
      RenderClasses: ["dependencies"],
      NavigateToId: itemIdString,
    },
  ];

  const contentBasedLineId = createContentBasedLineId(tokens, lineIdPrefix || "external", itemIdString);
  markAsExternal(contentBasedLineId);

  externalReferencesLines.push({
    LineId: contentBasedLineId,
    Tokens: tokens,
  });
}

/**
 * Checks if an item has a valid path
 */
function hasValidPath(itemSummary: ItemSummary): boolean {
  return !!(itemSummary.path && Array.isArray(itemSummary.path) && itemSummary.path.length > 0);
}

/**
 * Creates a single ReviewLine representing a non-module item
 */
export function createItemLineFromPath(
  itemId: Id,
  itemSummary: ItemSummary,
  lineIdPrefix: string = "",
): ReviewLine | undefined {
  const value = itemSummary.path[itemSummary.path.length - 1];
  if (value.startsWith("__")) return undefined;
  const transformedItemKind = transformItemKind(itemSummary.kind);

  const tokens = [
    {
      Kind: TokenKind.Keyword,
      Value: "pub",
    },
    {
      Kind: TokenKind.Keyword,
      Value: transformedItemKind,
    },
    {
      Kind: TokenKind.TypeName,
      Value: value,
      RenderClasses: ["dependencies"],
      NavigateToId: itemId.toString(),
      NavigationDisplayName: [
        ItemKind.Constant,
        ItemKind.Function,
        ItemKind.AssocConst,
        ItemKind.Static,
        ItemKind.StructField,
      ].includes(itemSummary.kind)
        ? undefined
        : value,
      HasSuffixSpace: true,
    },
    {
      Kind: TokenKind.Comment,
      Value: `/* re-export of ${itemSummary.path.join("::")} */`,
    },
  ];

  const contentBasedLineId = createContentBasedLineId(tokens, lineIdPrefix || "external", itemId.toString());
  markAsExternal(contentBasedLineId);

  return {
    LineId: contentBasedLineId,
    Tokens: tokens,
  };
}

export function transformItemKind(itemKind: ItemKind) {
  if (itemKind === ItemKind.Module) return "mod";
  if (itemKind === ItemKind.Function) return "fn";
  if (itemKind === ItemKind.Constant) return "const";
  return itemKind;
}

/**
 * Processes a module re-export, including finding all its children
 */
export function processModuleReexport(
  itemId: Id,
  itemSummary: ItemSummary,
  apiJson: Crate,
  parentModule?: { prefix: string; id: number },
  lineIdPrefix: string = "",
): ReviewLine[] {
  const moduleHeaderLine = createModuleHeaderLine(itemId, itemSummary, parentModule, lineIdPrefix);
  // Use the module's LineId as the prefix for its children
  const moduleLineIdPrefix = moduleHeaderLine.LineId;
  const children = findModuleChildrenByPath(itemSummary.path.join("::"), apiJson, moduleLineIdPrefix);

  if (children.length === 0) {
    // Add closing brace to the header line for empty modules
    moduleHeaderLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "}",
    });
    return [moduleHeaderLine];
  }
  // Add children and a separate closing brace line for non-empty modules
  moduleHeaderLine.Children = children;
  return [
    moduleHeaderLine,
    {
      RelatedToLine: moduleHeaderLine.LineId,
      Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
    },
  ];
}

/**
 * Creates the header line for a module
 */
function createModuleHeaderLine(
  itemId: Id,
  itemSummary: ItemSummary,
  parentModule?: { prefix: string; id: number },
  lineIdPrefix: string = "",
): ReviewLine {
  let lineIdValue = "mod";
  const tokens: ReviewToken[] = [
    {
      Kind: TokenKind.Keyword,
      Value: "pub mod",
    },
  ];

  if (parentModule) {
    tokens.push(
      {
        Kind: TokenKind.TypeName,
        Value: parentModule.prefix,
        NavigateToId: parentModule.id.toString(),
        HasSuffixSpace: false,
        RenderClasses: ["namespace"],
      },
      {
        Kind: TokenKind.Punctuation,
        Value: `::`,
        HasSuffixSpace: false,
      },
    );
  }

  const value = itemSummary.path[itemSummary.path.length - 1];
  tokens.push(
    {
      Kind: TokenKind.TypeName,
      Value: value,
      NavigateToId: itemId.toString(),
      NavigationDisplayName: parentModule ? parentModule.prefix + "::" + value : value,
      RenderClasses: ["namespace"],
    },
    {
      Kind: TokenKind.Punctuation,
      Value: "{",
      HasSuffixSpace: false,
    },
    {
      Kind: TokenKind.Comment,
      Value: `/* re-export of ${itemSummary.path.join("::")} */`,
      HasPrefixSpace: true,
      HasSuffixSpace: true,
    },
  );
  lineIdValue += `_${value}`;

  const contentBasedLineId = createContentBasedLineId(tokens, lineIdPrefix || "external", itemId.toString());
  markAsExternal(contentBasedLineId);

  return {
    LineId: contentBasedLineId,
    Tokens: tokens,
  };
}

/**
 * Returns all child item IDs of a module based on path
 */
export function getModuleChildIdsByPath(currentPath: string, apiJson: Crate): number[] {
  const childIds: number[] = [];
  for (const childId in apiJson.paths) {
    const childItemSummary = apiJson.paths[childId];
    if (!hasValidPath(childItemSummary)) continue;
    const childPath = childItemSummary.path.join("::");
    if (childPath !== currentPath && childPath.startsWith(currentPath + "::")) {
      childIds.push(Number(childId));
    }
  }
  return childIds;
}

/**
 * Finds all child items of a module based on path and returns ReviewLines
 */
function findModuleChildrenByPath(currentPath: string, apiJson: Crate, lineIdPrefix: string = ""): ReviewLine[] {
  const childIds = getModuleChildIdsByPath(currentPath, apiJson);
  const children: ReviewLine[] = [];
  const seenLineIds = new Set<string>();

  for (const childId of childIds) {
    const childItemSummary = apiJson.paths[childId];
    const childLine = createItemLineFromPath(Number(childId), childItemSummary, lineIdPrefix);
    if (childLine && !seenLineIds.has(childLine.LineId)) {
      children.push(childLine);
      seenLineIds.add(childLine.LineId);
    }
  }

  // Sort children by kind and then by path
  children.sort((a, b) => {
    const kindComparison = a.Tokens[1].Value.localeCompare(b.Tokens[1].Value);
    if (kindComparison !== 0) {
      return kindComparison;
    }
    return a.Tokens[2].Value.localeCompare(b.Tokens[2].Value);
  });

  return children;
}
