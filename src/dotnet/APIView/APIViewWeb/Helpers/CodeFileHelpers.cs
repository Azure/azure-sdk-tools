using APIView.DIff;
using APIView.Model;
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

            var allNodesAtLevelSorted = activeAPIRevisionTreeNodesAtLevel.Union(diffAPIRevisionTreeNodesAtLevel).OrderBy(node => node.Id);
            var unChangedNodesAtLevel = activeAPIRevisionTreeNodesAtLevel.Intersect(diffAPIRevisionTreeNodesAtLevel);
            var removedNodesAtLevel = activeAPIRevisionTreeNodesAtLevel.Except(diffAPIRevisionTreeNodesAtLevel);
            var addedNodesAtLevel = diffAPIRevisionTreeNodesAtLevel.Except(activeAPIRevisionTreeNodesAtLevel);

            foreach (var node in allNodesAtLevelSorted)
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

        public static void ComputeTokenDiffForNode(APITreeNodeForAPI activeAPIRevisionTreeNode, APITreeNodeForAPI diffAPIRevisionTreeNode, APITreeNodeForAPI resultNode)
        {
            var activeAPIRevisionTopTokens = new Queue<StructuredToken>(activeAPIRevisionTreeNode.TopTokens);
            var diffAPIRevisionTopTokens = new Queue<StructuredToken>(activeAPIRevisionTreeNode.TopTokens);
            var topTokensDiffResult = new List<StructuredToken>();
            ComputeTokenDiffForNode(activeAPIRevisionTopTokens, diffAPIRevisionTopTokens, topTokensDiffResult);

            var activeAPIRevisionBottomTokens = new Queue<StructuredToken>(activeAPIRevisionTreeNode.BottomTokens);
            var diffAPIRevisionBottomTokens = new Queue<StructuredToken>(activeAPIRevisionTreeNode.BottomTokens);
            var bottomTokensDiffResult = new List<StructuredToken>();
            ComputeTokenDiffForNode(activeAPIRevisionBottomTokens, diffAPIRevisionBottomTokens, bottomTokensDiffResult);

            resultNode.TopTokens = topTokensDiffResult;
            resultNode.BottomTokens = bottomTokensDiffResult;
        }

        public static List<StructuredToken> ComputeTokenDiff(List<StructuredToken> activeApiRevisionTokenLine, List<StructuredToken> diffApiRevisionTokenline)
        {
            var diffResult = new List<StructuredToken>();

            for (int i = 0; i < Math.Max(activeApiRevisionTokenLine.Count, diffApiRevisionTokenline.Count); i++)
            {
                if (i >= activeApiRevisionTokenLine.Count)
                {
                    var token = diffApiRevisionTokenline[i];
                    token.Properties["DiffKind"] = "Added";
                    diffResult.Add(token);
                }
                else if (i >= diffApiRevisionTokenline.Count)
                {
                    var token = activeApiRevisionTokenLine[i];
                    token.Properties["DiffKind"] = "Removed";
                    diffResult.Add(token);
                }
                else if (!activeApiRevisionTokenLine[i].Equals(diffApiRevisionTokenline[i]))
                {
                    var token = activeApiRevisionTokenLine[i];
                    token.Properties["DiffKind"] = "Removed";
                    diffResult.Add(token);
                    token = diffApiRevisionTokenline[i];
                    token.Properties["DiffKind"] = "Added";
                    diffResult.Add(token);
                }
                else
                {
                    var token = activeApiRevisionTokenLine[i];
                    token.Properties["DiffKind"] = "Unchanged";
                    diffResult.Add(token);
                }
            }

            return diffResult;
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

        private static void ComputeTokenDiffForNode(Queue<StructuredToken> activeAPIRevisionTokens, Queue<StructuredToken> diffAPIRevisionToken, List<StructuredToken> tokenDiffResult)
        {
            while (activeAPIRevisionTokens.Count > 0 || diffAPIRevisionToken.Count > 0)
            {
                List<StructuredToken> activeAPIRevisionTokenLine = new List<StructuredToken>();
                List<StructuredToken> diffAPIRevisionTokenLine = new List<StructuredToken>();
                while (activeAPIRevisionTokens.Count > 0)
                {
                    var token = activeAPIRevisionTokens.Dequeue();
                    if (token.Kind == StructuredTokenKind.LineBreak)
                    {
                        break;
                    }
                    activeAPIRevisionTokenLine.Add(token);
                }

                while (diffAPIRevisionTokenLine.Count > 0)
                {
                    var token = diffAPIRevisionToken.Dequeue();
                    if (token.Kind == StructuredTokenKind.LineBreak)
                    {
                        break;
                    }
                    diffAPIRevisionTokenLine.Add(token);
                }

                var diffResult = ComputeTokenDiff(activeAPIRevisionTokenLine, diffAPIRevisionTokenLine);
                tokenDiffResult.AddRange(diffResult);
                tokenDiffResult.Add(StructuredToken.CreateLineBreakToken());
            }

        }
    }
}
