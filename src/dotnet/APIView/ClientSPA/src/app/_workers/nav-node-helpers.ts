import { NavigationTreeNode } from '../_models/navigationTreeModels';
import { DIFF_REMOVED } from '../_helpers/common-helpers';

/**
 * Adds a nav node to the tree while deduplicating modified elements.
 *
 * When a diff is computed, both sides of a modification appear as separate nodes:
 * - removed-side: all codeLines have diffKind === "removed"
 * - added-side:   codeLines have diffKind === "added" or "unchanged"
 *
 * Strategy:
 * - Removed-side nodes are pushed tentatively. A non-removed counterpart with
 *   the same label (the modification case) will replace it later, so the nav
 *   entry points to the active-revision line. Purely-deleted elements have no
 *   such counterpart, so their entry is kept.
 * - Non-removed nodes replace any existing removed-side entry with the same
 *   label, or are pushed as new entries.
 */
export function applyNavNodeToTree(
  navTree: NavigationTreeNode[],
  navNode: NavigationTreeNode,
  codeLineDiffKinds: string[]
): void {
  const isAllRemovedNode = codeLineDiffKinds.length > 0 && codeLineDiffKinds.every(k => k === DIFF_REMOVED);
  if (isAllRemovedNode) {
    navTree.push(navNode);
  } else {
    const existingRemovedIndex = navTree.findIndex(n => n.label === navNode.label);
    if (existingRemovedIndex >= 0) {
      navTree[existingRemovedIndex] = navNode;
    } else {
      navTree.push(navNode);
    }
  }
}
