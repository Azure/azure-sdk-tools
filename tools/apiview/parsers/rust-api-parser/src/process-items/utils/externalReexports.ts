import { Crate, Id, ItemSummary } from "../../../rustdoc-types/output/rustdoc-types";
import { getAPIJson } from "../../main";
import { ReviewLine, TokenKind } from "../../models/apiview-models";

/**
 * Processes external re-exports and creates a structured view
 * @param itemId id of the item being re-exported
 * @returns Object containing ReviewLine arrays for both items and modules
 */
export function externalReexports(itemId: Id): { items: ReviewLine[]; modules: ReviewLine[] } {
  const apiJson = getAPIJson();
  if (!isValidItemId(itemId, apiJson)) {
    return { items: [], modules: [] };
  }

  const itemSummary = apiJson.paths[itemId];
  // Check if the item has a path
  if (!hasValidPath(itemSummary)) {
    return { items: [], modules: [] };
  }

  // Handle differently based on item kind
  if (itemSummary.kind === "module") {
    return processModuleReexport(itemId, itemSummary, apiJson);
  } else {
    // For non-module items, just represent them directly
    return {
      items: [createItemLine(itemId, itemSummary)],
      modules: [],
    };
  }
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
function createItemLine(itemId: Id, itemSummary: ItemSummary): ReviewLine {
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
        Value: itemSummary.path.concat().join("::"),
        RenderClasses: ["dependencies"],
        NavigateToId: itemId.toString(),
      },
    ],
  };
}

/**
 * Processes a module re-export, including finding all its children
 */
function processModuleReexport(
  itemId: Id,
  itemSummary: ItemSummary,
  apiJson: Crate,
): { items: ReviewLine[]; modules: ReviewLine[] } {
  const moduleHeaderLine = createModuleHeaderLine(itemId, itemSummary);
  const children = findModuleChildren(itemSummary.path.join("::"), apiJson);

  if (children.length === 0) {
    // Add closing brace to the header line for empty modules
    moduleHeaderLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "}",
    });
    return {
      modules: [moduleHeaderLine],
      items: [],
    };
  } else {
    // Add children and a separate closing brace line for non-empty modules
    moduleHeaderLine.Children = children;
    return {
      modules: [
        moduleHeaderLine,
        {
          RelatedToLine: itemId.toString(),
          Tokens: [{ Kind: TokenKind.Punctuation, Value: "}" }],
        },
      ],
      items: [],
    };
  }
}

/**
 * Creates the header line for a module
 */
function createModuleHeaderLine(itemId: Id, itemSummary: ItemSummary): ReviewLine {
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
        Value: itemSummary.path.concat().join("::"),
        NavigateToId: itemId.toString(),
      },
      {
        Kind: TokenKind.Punctuation,
        Value: "{",
        HasSuffixSpace: false,
      },
    ],
  };
}

/**
 * Finds all child items of a module based on path
 */
function findModuleChildren(currentPath: string, apiJson: Crate): ReviewLine[] {
  const children: ReviewLine[] = [];

  // Process all items in paths to find children
  for (const childId in apiJson.paths) {
    const childItemSummary = apiJson.paths[childId];

    // Skip if the child doesn't have a path
    if (!hasValidPath(childItemSummary)) {
      continue;
    }

    const childPath = childItemSummary.path.join("::");

    // Check if this is a child path (starts with the current path and is not the same path)
    if (childPath !== currentPath && childPath.startsWith(currentPath + "::")) {
      // Add as a child
      children.push(createItemLine(Number(childId), childItemSummary));
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
