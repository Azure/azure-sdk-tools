using APIView.DIff;
using APIView.Model;
using APIViewWeb.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Helpers
{
    public class CodeFileHelpers
    {
        public static List<APITreeNodeForAPI> ComputeAPIForestDiff(List<APITreeNodeForAPI> activeAPIRevisionAPIForest, List<APITreeNodeForAPI> diffAPIRevisionAPIForest)
        {
            List<APITreeNodeForAPI> diffAPITree = new List<APITreeNodeForAPI>();
            ComputeAPITreeDiff(activeAPIRevisionAPIForest, diffAPIRevisionAPIForest, diffAPITree);
            return diffAPITree;
        }

        private static void ComputeAPITreeDiff(List<APITreeNodeForAPI> activeAPIRevisionAPIForest, List<APITreeNodeForAPI> diffAPIRevisionAPIForest, List<APITreeNodeForAPI> diffAPITree)
        {           
            var activeAPIRevisionTreeNodesAtLevel = new HashSet<APITreeNodeForAPI>(activeAPIRevisionAPIForest);
            var diffAPIRevisionTreeNodesAtLevel = new HashSet<APITreeNodeForAPI>(diffAPIRevisionAPIForest);

            var allNodesAtLevel = activeAPIRevisionAPIForest.InterleavedUnion(diffAPIRevisionAPIForest);
            var unChangedNodesAtLevel = activeAPIRevisionTreeNodesAtLevel.Intersect(diffAPIRevisionTreeNodesAtLevel);
            var removedNodesAtLevel = activeAPIRevisionTreeNodesAtLevel.Except(diffAPIRevisionTreeNodesAtLevel);
            var addedNodesAtLevel = diffAPIRevisionTreeNodesAtLevel.Except(activeAPIRevisionTreeNodesAtLevel);

            foreach (var node in allNodesAtLevel)
            {
                if (unChangedNodesAtLevel.Contains(node))
                {
                    var newEntry = CreateAPITreeDiffNode(node, DiffNodeKind.Unchanged);
                    diffAPITree.Add(newEntry);
                }
                else if (removedNodesAtLevel.Contains(node))
                {
                    var newEntry = CreateAPITreeDiffNode(node, DiffNodeKind.Removed);
                    diffAPITree.Add(newEntry);
                }
                else if (addedNodesAtLevel.Contains(node))
                {
                    var newEntry = CreateAPITreeDiffNode(node, DiffNodeKind.Added);
                    diffAPITree.Add(newEntry);
                }
            }

            foreach (var node in unChangedNodesAtLevel)
            {
                var activeAPIRevisionNode = activeAPIRevisionTreeNodesAtLevel.First(n => n.Equals(node));
                var diffAPIRevisionNode = diffAPIRevisionTreeNodesAtLevel.First(n => n.Equals(node));
                var diffResultNode = diffAPITree.First(n => n.Equals(node));

                diffResultNode.TopTokens = activeAPIRevisionNode.TopTokens;
                diffResultNode.BottomTokens = activeAPIRevisionNode.BottomTokens;
                diffResultNode.TopDiffTokens = diffAPIRevisionNode.TopTokens;
                diffResultNode.BottomDiffTokens = diffAPIRevisionNode.BottomTokens;

                var childrenResult = new List<APITreeNodeForAPI>();
                ComputeAPITreeDiff(activeAPIRevisionNode.Children, diffAPIRevisionNode.Children, childrenResult);
                diffResultNode.Children.AddRange(childrenResult);
            };
        }

        public static APITreeNodeForAPI CreateAPITreeDiffNode(APITreeNodeForAPI node, DiffNodeKind diffKind)
        {
            var result = new APITreeNodeForAPI
            {
                Name = node.Name,
                Id = node.Id,
                Kind    = node.Kind,
                Tags = node.Tags,
                Properties = node.Properties,
                DiffKind = diffKind
            };

            if (diffKind == DiffNodeKind.Added || diffKind == DiffNodeKind.Removed)
            {
                result.TopTokens = node.TopTokens;
                result.BottomTokens = node.BottomTokens;
                result.Children = node.Children;
            }

            return result;
        }
    }
}
