using APIView.Model;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using MongoDB.Driver.Core.WireProtocol.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Helpers
{
    public class CodeFileHelpers
    {
        public const string DIFF_PROPERTY = "DiffKind";
        public const string UNCHANGED = "Unchanged";
        public const string ADDED = "Added";
        public const string REMOVED = "Removed";

        public static List<APITreeNode> ComputeAPIForestDiff(List<APITreeNode> activeAPIRevisionAPIForest, List<APITreeNode> diffAPIRevisionAPIForest)
        {
            List<APITreeNode> result = new List<APITreeNode>();
            List<Task> tasks = new List<Task>();
            ComputeAPITreeDiff(activeAPIRevisionAPIForest, diffAPIRevisionAPIForest, result);
            return result;
        }

        private static void ComputeAPITreeDiff(List<APITreeNode> activeAPIRevisionAPIForest, List<APITreeNode> diffAPIRevisionAPIForest, List<APITreeNode> result)
        {           
            var activeAPIRevisionTreeNodesAtLevel = new HashSet<APITreeNode>(activeAPIRevisionAPIForest);
            var diffAPIRevisionTreeNodesAtLevel = new HashSet<APITreeNode>(diffAPIRevisionAPIForest);

            var allNodesAtLevelSorted = activeAPIRevisionTreeNodesAtLevel.Union(diffAPIRevisionTreeNodesAtLevel).OrderBy(node => node.Id);
            var unChangedNodesAtLevel = activeAPIRevisionTreeNodesAtLevel.Intersect(diffAPIRevisionTreeNodesAtLevel);
            var removedNodesAtLevel = activeAPIRevisionTreeNodesAtLevel.Except(diffAPIRevisionTreeNodesAtLevel);
            var addedNodesAtLevel = diffAPIRevisionTreeNodesAtLevel.Except(activeAPIRevisionTreeNodesAtLevel);

            foreach (var node in allNodesAtLevelSorted)
            {
                if (unChangedNodesAtLevel.Contains(node))
                {
                    var newEntry = CloneAPITreeNode(node);
                    newEntry.Properties[DIFF_PROPERTY] = UNCHANGED;
                    result.Add(newEntry);
                }
                else if (removedNodesAtLevel.Contains(node))
                {
                   node.Properties[DIFF_PROPERTY] = REMOVED;
                   result.Add(node);
                }
                else if (addedNodesAtLevel.Contains(node))
                {
                    node.Properties[DIFF_PROPERTY] = ADDED;
                    result.Add(node);
                }
            }

            Parallel.ForEach(unChangedNodesAtLevel, node =>
            {
                var activeNode = activeAPIRevisionTreeNodesAtLevel.First(n => n.Equals(node));
                var diffNode = diffAPIRevisionTreeNodesAtLevel.First(n => n.Equals(node));
                var resultNode = result.First(n => n.Equals(node));
                //ComputeTokenDiffForNode(activeNode, diffNode, resultNode);

                var childrenResult = new List<APITreeNode>();
                ComputeAPITreeDiff(activeNode.Children, diffNode.Children, childrenResult);
                resultNode.Children.AddRange(childrenResult);
            });
        }

        public static void ComputeTokenDiffForNode(APITreeNode activeAPIRevisionTreeNode, APITreeNode diffAPIRevisionTreeNode, APITreeNode resultNode)
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
                    token.Properties[DIFF_PROPERTY] = ADDED;
                    diffResult.Add(token);
                }
                else if (i >= diffApiRevisionTokenline.Count)
                {
                    var token = activeApiRevisionTokenLine[i];
                    token.Properties[DIFF_PROPERTY] = REMOVED;
                    diffResult.Add(token);
                }
                else if (!activeApiRevisionTokenLine[i].Equals(diffApiRevisionTokenline[i]))
                {
                    var token = activeApiRevisionTokenLine[i];
                    token.Properties[DIFF_PROPERTY] = REMOVED;
                    diffResult.Add(token);
                    token = diffApiRevisionTokenline[i];
                    token.Properties[DIFF_PROPERTY] = ADDED;
                    diffResult.Add(token);
                }
                else
                {
                    var token = activeApiRevisionTokenLine[i];
                    token.Properties[DIFF_PROPERTY] = UNCHANGED;
                    diffResult.Add(token);
                }
            }

            return diffResult;
        }

        public static APITreeNode CloneAPITreeNode(APITreeNode node)
        {
            return new APITreeNode
            {
                Name = node.Name,
                Id = node.Id,
                Kind    = node.Kind
            };
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
