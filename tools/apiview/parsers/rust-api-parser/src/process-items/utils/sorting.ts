import { Id, Item, ItemKind } from "../../../rustdoc-types/output/rustdoc-types";
import { getAPIJson } from "../../main";
import { isUseItem } from "./typeGuards";
import { ReviewLine } from "../../models/apiview-models";

export type ChildItem = { id: Id; name: string };
export type ChildItems = Record<ItemKind, ChildItem[]>;

/**
 * Defines the order of item kinds for sorting in the API view.
 * This determines the visual grouping and ordering in the rendered output.
 */
export const itemKindOrder: ItemKind[] = [
  ItemKind.Use,
  ItemKind.Struct,
  ItemKind.Enum,
  ItemKind.Trait,
  ItemKind.Function,
  ItemKind.TypeAlias,
  ItemKind.Constant,
  ItemKind.Static,
  ItemKind.ExternType,
  ItemKind.Impl,
  ItemKind.Union,
  ItemKind.TraitAlias,
  ItemKind.AssocConst,
  ItemKind.AssocType,
  ItemKind.Primitive,
  ItemKind.Keyword,
  ItemKind.ProcAttribute,
  ItemKind.ProcDerive,
  ItemKind.Variant,
  ItemKind.ExternCrate,
  ItemKind.StructField,
  ItemKind.Module,
  ItemKind.Macro,
];

/**
 * Creates an empty children object with arrays for each ItemKind.
 * @returns A fresh ChildItems object with empty arrays for each kind
 */
function createEmptyChildren(): ChildItems {
  const children: Partial<ChildItems> = {};

  // Initialize an empty array for each item kind
  for (const kind of Object.values(ItemKind)) {
    children[kind] = [];
  }

  return children as ChildItems;
}

/**
 * Determines the kind of an item from its inner structure.
 * @param item The item to check
 * @returns The ItemKind of the item
 */
function getItemKind(item: Item): ItemKind | undefined {
  if (!item || !item.inner) return undefined;
  return Object.keys(item.inner)[0] as ItemKind;
}

/**
 * Gets the name to use for sorting an item.
 * @param item The item to get the name for
 * @param kind The kind of the item
 * @returns The name to use for sorting
 */
function getItemSortName(item: Item, kind: ItemKind): string {
  if (kind === ItemKind.Use && isUseItem(item)) {
    return item.inner.use.source || "";
  }
  return item.name || "";
}

/**
 * Sorts children items by their kind and name.
 * @param childItems Array of child item IDs
 * @returns Object containing sorted arrays of module and non-module item IDs
 */
export function getSortedChildIds(childItems: number[]): { nonModule: number[]; module: number[] } {
  const apiJson = getAPIJson();
  const children: ChildItems = createEmptyChildren();
  const result = { nonModule: [] as number[], module: [] as number[] };

  // First pass: categorize all items by their kind
  for (const childId of childItems) {
    if (childId in apiJson.index) {
      const child = apiJson.index[childId];
      if (!child) continue;

      const kind = getItemKind(child);
      if (!kind || !children[kind]) continue;

      children[kind].push({
        id: child.id,
        name: getItemSortName(child, kind),
      });
    } else if (childId in apiJson.paths) {
      const child = apiJson.paths[childId];
      if (!child) continue;
      const kind = child.kind;
      if (!children[kind]) continue;

      children[kind].push({
        id: childId,
        name: child.path[child.path.length - 1], // Use the last part of the path as the name
      });
    }
  }

  // Sort items within each kind by name (case-insensitive)
  for (const kind of Object.values(ItemKind)) {
    if (children[kind]) {
      children[kind].sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { sensitivity: "base" }),
      );
    }
  }

  // Second pass: create final sorted arrays based on kind order
  for (const kind of itemKindOrder) {
    if (!children[kind] || children[kind].length === 0) continue;

    for (const child of children[kind]) {
      // We know modules are handled differently, so separate them
      if (kind === ItemKind.Module) {
        result.module.push(child.id);
      } else {
        result.nonModule.push(child.id);
      }
    }
  }

  return result;
}

/**
 * Sorts external items by item kind according to itemKindOrder and then by name within each kind
 * @param items Array of external ReviewLine items to sort
 * @returns The same array, sorted in place by kind and name
 */
export function sortExternalItems(items: ReviewLine[]): ReviewLine[] {
  return items.sort((a, b) => {
    const aTokens = a.Tokens.map((token) => token.Value)
      .join(" ")
      .split(" ");
    const bTokens = b.Tokens.map((token) => token.Value)
      .join(" ")
      .split(" ");

    // The second token represents the kind
    const aKind = aTokens[1] as ItemKind;
    const bKind = bTokens[1] as ItemKind;

    if (aKind !== bKind) {
      return itemKindOrder.indexOf(aKind) - itemKindOrder.indexOf(bKind);
    }

    // For items of the same kind, sort by name (the third token)
    return aTokens[2].localeCompare(bTokens[2]);
  });
}
