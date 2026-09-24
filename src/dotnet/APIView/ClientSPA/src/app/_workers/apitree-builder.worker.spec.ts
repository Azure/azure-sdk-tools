import { NavigationTreeNode, NavigationTreeNodeData } from '../_models/navigationTreeModels';
import { DIFF_ADDED, DIFF_REMOVED } from '../_helpers/common-helpers';
import { applyNavNodeToTree } from './nav-node-helpers';

function makeNavNode(label: string, nodeIdHashed: string): NavigationTreeNode {
  const node = new NavigationTreeNode();
  node.label = label;
  node.data = new NavigationTreeNodeData();
  node.data.nodeIdHashed = nodeIdHashed;
  node.data.kind = 'class';
  return node;
}

describe('apitree-builder nav deduplication', () => {
  describe('modified element (removed-side + added-side with same label)', () => {
    it('results in a single nav entry', () => {
      const navTree: NavigationTreeNode[] = [];
      const removedNode = makeNavNode('SearchClient', 'hash-removed');
      const addedNode = makeNavNode('SearchClient', 'hash-added');

      applyNavNodeToTree(navTree, removedNode, [DIFF_REMOVED]);
      applyNavNodeToTree(navTree, addedNode, [DIFF_ADDED]);

      expect(navTree.length).toBe(1);
    });

    it('keeps the added-side (active-revision) entry, not the removed-side', () => {
      const navTree: NavigationTreeNode[] = [];
      const removedNode = makeNavNode('SearchClient', 'hash-removed');
      const addedNode = makeNavNode('SearchClient', 'hash-added');

      applyNavNodeToTree(navTree, removedNode, [DIFF_REMOVED]);
      applyNavNodeToTree(navTree, addedNode, [DIFF_ADDED]);

      expect(navTree[0].data.nodeIdHashed).toBe('hash-added');
    });
  });

  describe('purely deleted element (no active-revision counterpart)', () => {
    it('keeps the nav entry so the deleted element remains navigable', () => {
      const navTree: NavigationTreeNode[] = [];
      const deletedNode = makeNavNode('DeletedClass', 'hash-deleted');

      applyNavNodeToTree(navTree, deletedNode, [DIFF_REMOVED]);

      expect(navTree.length).toBe(1);
      expect(navTree[0].data.nodeIdHashed).toBe('hash-deleted');
    });
  });

  describe('unchanged element', () => {
    it('adds the nav entry normally', () => {
      const navTree: NavigationTreeNode[] = [];
      const node = makeNavNode('UnchangedClass', 'hash-unchanged');

      applyNavNodeToTree(navTree, node, ['unchanged']);

      expect(navTree.length).toBe(1);
      expect(navTree[0].data.nodeIdHashed).toBe('hash-unchanged');
    });
  });

  describe('mixed siblings', () => {
    it('produces one entry per unique element (modified + deleted + unchanged)', () => {
      const navTree: NavigationTreeNode[] = [];

      // Modified element: removed first, then added
      applyNavNodeToTree(navTree, makeNavNode('ModifiedClass', 'modified-removed'), [DIFF_REMOVED]);
      applyNavNodeToTree(navTree, makeNavNode('ModifiedClass', 'modified-added'), [DIFF_ADDED]);

      // Purely deleted element
      applyNavNodeToTree(navTree, makeNavNode('DeletedClass', 'deleted'), [DIFF_REMOVED]);

      // Unchanged element
      applyNavNodeToTree(navTree, makeNavNode('UnchangedClass', 'unchanged'), ['unchanged']);

      expect(navTree.length).toBe(3);
      expect(navTree.map(n => n.label)).toEqual(['ModifiedClass', 'DeletedClass', 'UnchangedClass']);
      expect(navTree.find(n => n.label === 'ModifiedClass')!.data.nodeIdHashed).toBe('modified-added');
      expect(navTree.find(n => n.label === 'DeletedClass')!.data.nodeIdHashed).toBe('deleted');
    });
  });
});
