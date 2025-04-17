import { Crate, Id, ItemSummary } from "../../../rustdoc-types/output/rustdoc-types";
import { getAPIJson } from "../../main";
import { ReviewLine, TokenKind } from "../../models/apiview-models";

export const reexportLines: {
  internal: ReviewLine[];
  external: { items: ReviewLine[]; modules: ReviewLine[] };
} = {
  internal: [],
  external: { items: [], modules: [] },
};

/**
 * Adds external re-export lines if the item exists in paths and is not already added.
 * @param useItemId The ID of the item being used/re-exported.
 */
export function addExternalReexportIfNotExists(useItemId: Id): void {
  const apiJson = getAPIJson();
  if (
    useItemId in apiJson.index ||
    !(useItemId in apiJson.paths) ||
    reexportLines.external.items.some((line) => line.LineId === useItemId.toString()) // Check if the re-export line already exists
  ) {
    return;
  }

  const lines = externalReexports(useItemId, apiJson);
  reexportLines.external.items.push(...lines);
}

/**
 * Processes external non-module re-exports and creates a structured view
 * @param itemId id of the item being re-exported
 * @param apiJson The parsed rustdoc JSON object
 * @returns Object containing ReviewLine arrays for both items and modules
 */
export function externalReexports(itemId: Id, apiJson: Crate): ReviewLine[] {
  if (!isValidItemId(itemId, apiJson)) return [];
  const itemSummary = apiJson.paths[itemId];
  // Check if the item has a path
  if (!hasValidPath(itemSummary)) return [];
  return [createItemLine(itemId, itemSummary, apiJson)];
}

/**
 * Checks if the item ID is valid in the given API JSON
 */
function isValidItemId(itemId: Id, apiJson: Crate): boolean {
  return !!(itemId && apiJson && apiJson.paths && apiJson.paths[itemId]);
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
function createItemLine(itemId: Id, itemSummary: ItemSummary, apiJson: Crate): ReviewLine {
  return {
    LineId: itemId.toString(),
    Tokens: [
      {
        Kind: TokenKind.Keyword,
        Value: "pub",
      },
      {
        Kind: TokenKind.Keyword,
        Value: itemSummary.kind,
      },
      {
        Kind: TokenKind.TypeName,
        Value: itemSummary.path.join("::"),
        RenderClasses: ["dependencies"],
        NavigateToId: itemId.toString(),
      },
    ],
  };
}

/**
 * Processes a module re-export, including finding all its children
 */
export function processModuleReexport(
  itemId: Id,
  itemSummary: ItemSummary,
  apiJson: Crate,
  parentModule?: { prefix: string; id: number },
): ReviewLine[] {
  const moduleHeaderLine = createModuleHeaderLine(itemId, itemSummary, parentModule);
  const children = findModuleChildren(itemSummary.path.join("::"), apiJson);

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
      RelatedToLine: itemId.toString(),
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
): ReviewLine {
  return {
    LineId: itemId.toString(),
    Tokens: [
      {
        Kind: TokenKind.Keyword,
        Value: "pub mod",
      },
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
      {
        Kind: TokenKind.TypeName,
        Value: itemSummary.path[itemSummary.path.length - 1],
        NavigateToId: itemId.toString(),
        NavigationDisplayName:
          parentModule.prefix + "::" + itemSummary.path[itemSummary.path.length - 1],
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
    ],
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
function findModuleChildren(currentPath: string, apiJson: Crate): ReviewLine[] {
  const childIds = getModuleChildIdsByPath(currentPath, apiJson);
  const children: ReviewLine[] = [];

  for (const childId of childIds) {
    const childItemSummary = apiJson.paths[childId];
    children.push(createItemLine(Number(childId), childItemSummary, apiJson));
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
