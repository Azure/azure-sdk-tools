using ApiView;
using APIView.DIff;
using APIView.Model;
using APIViewWeb.Extensions;
using APIViewWeb.LeanModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Helpers
{
    public class CodeFileHelpers
    {
        public static CodePanelData GenerateCodePanelDataAsync(CodeFile codeFile)
        {
            var codePanelData = new CodePanelData();
            var navigationTree = new List<NavigationTreeNode>();

            Parallel.ForEach(codeFile.APIForest, node => {
                BuildAPITree(node, navigationTree);
            });

            codePanelData.NavigationTree = navigationTree;

            return codePanelData;
        }

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

        private static void BuildAPITree(APITreeNodeForAPI apiTreeNode, List<NavigationTreeNode> navigationTree, int indent = 0)
        {
            BuildNodeTokens(apiTreeNode, LinesOfTokensPosition.Top, indent);

            // Set Navigation Icons
            var navIcon = apiTreeNode.Kind.ToLower();
            if (apiTreeNode.Properties.ContainsKey("SubKind"))
            {
                navIcon = apiTreeNode.Properties["SubKind"].ToLower();
            }

            if (apiTreeNode.Properties.ContainsKey("IconName"))
            {
                navIcon = apiTreeNode.Properties["IconName"].ToLower();
            }

            var navTreeNode = new NavigationTreeNode()
            {
                Label = apiTreeNode.Name,
                Data = new NavigationTreeNodeData()
                {
                    Kind = apiTreeNode.Properties.ContainsKey("SubKind") ? apiTreeNode.Properties["SubKind"] : apiTreeNode.Properties["SubKind"],
                    Icon = navIcon,
                },
                Expanded = true,
            };

            if (!apiTreeNode.Tags.Contains("HideFromNavigation"))
            {
                navigationTree.Add(navTreeNode);
            }
            

            Parallel.ForEach(apiTreeNode.Children, node =>
            {
                BuildAPITree(node, navTreeNode.Children, indent + 1);
            });

            if (apiTreeNode.BottomTokens.Any())
            {
                BuildNodeTokens(apiTreeNode, LinesOfTokensPosition.Bottom, indent);
            }
        }

        private static void BuildNodeTokens(APITreeNodeForAPI apiTreeNode, LinesOfTokensPosition linesOfTokensPosition, int indent)
        {
            if (apiTreeNode.DiffKind == DiffNodeKind.NoneDiff)
            {
                BuildTokensForNonDiffNodes(apiTreeNode, linesOfTokensPosition, indent);
            }
            else
            { }
        }

        private static void BuildTokensForNonDiffNodes(APITreeNodeForAPI apiTreeNode, LinesOfTokensPosition linesOfTokensPosition, int indent)
        {
            var nodeId = GetTokenNodeIdHash(apiTreeNode, linesOfTokensPosition);
            var tokensInNode = (linesOfTokensPosition == LinesOfTokensPosition.Top) ? apiTreeNode.TopTokens : apiTreeNode.BottomTokens;

            var tokensInLine = new List<StructuredToken>();
            var rowClasses = new HashSet<string>();
            var tokenIdsInline = new HashSet<string>();

            foreach (var token in tokensInNode)
            {
                if (token.Properties.ContainsKey("GroupId"))
                {
                    rowClasses.Add(token.Properties["GroupId"]);
                }

                if (token.Kind == StructuredTokenKind.LineBreak)
                {
                    var row = new CodePanelRowData();


                }
                else 
                {
                    tokensInLine.Add(token);
                    if (token.Id) { 
                    }
                }
            }

        }

        private static string GetTokenNodeIdHash(APITreeNodeForAPI apiTreeNode, LinesOfTokensPosition linesOfTokensPosition)
        {
            var idPart = apiTreeNode.Kind;

            if (apiTreeNode.Properties.ContainsKey("SubKind"))
            {
                idPart = $"{idPart}-{apiTreeNode.Properties["SubKind"]}";
            }
            idPart = $"{idPart}-{apiTreeNode.Id}";
            idPart = $"{idPart}-{linesOfTokensPosition.ToString()}";
            return CreateHashFromString(idPart);
        }

        private static string CreateHashFromString(string inputString)
        {
            int hash = inputString.Aggregate(0, (prevHash, currVal) =>
                ((prevHash << 5) - prevHash) + currVal);

            string cssId = "id" + hash.ToString();

            return cssId;
        }
    }
}
