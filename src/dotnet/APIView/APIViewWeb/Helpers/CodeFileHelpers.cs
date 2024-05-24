
using APIView.Model;
using APIViewWeb.Extensions;
using APIViewWeb.LeanModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APIViewWeb.Helpers
{
    public class CodeFileHelpers
    {
        public static CodePanelData GenerateCodePanelDataAsync(CodePanelRawData codePanelRawData)
        {
            var codePanelData = new CodePanelData();
            var navigationTree = new List<NavigationTreeNode>();

            for (int idx = 0; idx < codePanelRawData.APIForest.Count; idx++)
            {
                var node = codePanelRawData.APIForest[idx];
                BuildAPITree(codePanelData: codePanelData, codePanelRawData: codePanelRawData, apiTreeNode: node, navigationTree: navigationTree, 
                    parentNodeIdHashed: "root", nodePositionAtLevel: idx);
            };

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
                    var newEntry = CreateAPITreeDiffNode(node, DiffKind.Unchanged);
                    diffAPITree.Add(newEntry);
                }
                else if (removedNodesAtLevel.Contains(node))
                {
                    var newEntry = CreateAPITreeDiffNode(node, DiffKind.Removed);
                    diffAPITree.Add(newEntry);
                }
                else if (addedNodesAtLevel.Contains(node))
                {
                    var newEntry = CreateAPITreeDiffNode(node, DiffKind.Added);
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

        public static APITreeNodeForAPI CreateAPITreeDiffNode(APITreeNodeForAPI node, DiffKind diffKind)
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

            if (diffKind == DiffKind.Added || diffKind == DiffKind.Removed)
            {
                result.TopTokens = node.TopTokens;
                result.BottomTokens = node.BottomTokens;
                result.Children = node.Children;
            }

            return result;
        }

        private static void BuildAPITree(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNodeForAPI apiTreeNode, List<NavigationTreeNode> navigationTree, string parentNodeIdHashed, int nodePositionAtLevel, int indent = 0)
        {
            var nodeIdHashed = GetTokenNodeIdHash(apiTreeNode, LinesOfTokensPosition.Top);
            codePanelData.NodeMetaData[nodeIdHashed].ParentNodeId = parentNodeIdHashed;
            codePanelData.NodeMetaData[nodeIdHashed].IsClosingNode = false;
            codePanelData.NodeMetaData[parentNodeIdHashed].ChildrenNodeIdsInOrder.Add(nodePositionAtLevel, nodeIdHashed);

            BuildNodeTokens(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, LinesOfTokensPosition.Top, indent);

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

            for (int idx = 0; idx < apiTreeNode.Children.Count; idx++) {
                var node = apiTreeNode.Children[idx];
                BuildAPITree(codePanelData: codePanelData, codePanelRawData: codePanelRawData, apiTreeNode: node, navigationTree: navTreeNode.Children, 
                    parentNodeIdHashed: nodeIdHashed, nodePositionAtLevel: idx, indent: indent + 1);
            };

            if (apiTreeNode.BottomTokens.Any())
            {
                nodeIdHashed = GetTokenNodeIdHash(apiTreeNode, LinesOfTokensPosition.Bottom);
                codePanelData.NodeMetaData[nodeIdHashed].ParentNodeId = parentNodeIdHashed;
                codePanelData.NodeMetaData[nodeIdHashed].IsClosingNode = true;
                BuildNodeTokens(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, LinesOfTokensPosition.Bottom, indent);
            }
        }

        private static void BuildNodeTokens(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNodeForAPI apiTreeNode, string nodeIdHashed, LinesOfTokensPosition linesOfTokensPosition, int indent)
        {
            if (apiTreeNode.DiffKind == DiffKind.NoneDiff)
            {
                BuildTokensForNonDiffNodes(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, linesOfTokensPosition, indent);
            }
            else
            { 
            }
        }

        private static void BuildTokensForNonDiffNodes(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNodeForAPI apiTreeNode, string nodeIdHashed, LinesOfTokensPosition linesOfTokensPosition, int indent)
        {
            var tokensInNode = (linesOfTokensPosition == LinesOfTokensPosition.Top) ? apiTreeNode.TopTokens : apiTreeNode.BottomTokens;

            var tokensInRow = new List<StructuredToken>();
            var rowClasses = new HashSet<string>();
            var tokenIdsInRow = new HashSet<string>();

            foreach (var token in tokensInNode)
            {
                if (token.Properties.ContainsKey("GroupId"))
                {
                    rowClasses.Add(token.Properties["GroupId"]);
                }

                if (token.Kind == StructuredTokenKind.LineBreak)
                {
                    InsertCodePanelRowData(codePanelData: codePanelData, codePanelRawData: codePanelRawData, tokensInRow: tokensInRow,
                        rowClasses: rowClasses, tokenIdsInRow: tokenIdsInRow, nodeIdHashed: nodeIdHashed, nodeId: apiTreeNode.Id, indent: indent, linesOfTokensPosition: linesOfTokensPosition);
                }
                else 
                {
                    tokensInRow.Add(token);
                    if (!String.IsNullOrWhiteSpace(token.Id))
                    {
                        tokenIdsInRow.Add(token.Id);
                    }
                }
            }

            if (tokensInRow.Any())
            {
                InsertCodePanelRowData(codePanelData: codePanelData, codePanelRawData: codePanelRawData, tokensInRow: tokensInRow,
                    rowClasses: rowClasses, tokenIdsInRow: tokenIdsInRow, nodeIdHashed: nodeIdHashed, nodeId: apiTreeNode.Id, indent: indent, linesOfTokensPosition: linesOfTokensPosition);
            }

            AddDiagnoasticRow(codePanelData, codePanelRawData, apiTreeNode.Id, nodeIdHashed, linesOfTokensPosition);
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

        private static CodePanelRowData CollectUserCommentsForRow(CodePanelRawData codePanelRawData, HashSet<string> tokenIdsInRow, string nodeId, string nodeIdHashed, LinesOfTokensPosition linesOfTokensPosition, CodePanelRowData codePanelRowData)
        {
            var commentRowData = new CodePanelRowData();
            var toggleCommentClass = (codePanelRawData.Diagnostics.Any(d => d.TargetId == nodeId)) ? "bi bi-chat-right-text show" : "";

            if (tokenIdsInRow.Any())
            {
                toggleCommentClass = (!String.IsNullOrWhiteSpace(toggleCommentClass)) ? "bi bi-chat-right-text can-show" : toggleCommentClass;
                codePanelRowData.ToggleCommentsClasses = toggleCommentClass;

                var commentsForRow = codePanelRawData.Comments.Where(c => tokenIdsInRow.Contains(c.ElementId));
                if (commentsForRow.Any())
                {
                    commentRowData.Type = CodePanelRowDatatype.CommentThread;
                    commentRowData.NodeIdHashed = nodeIdHashed;
                    commentRowData.NodeId = nodeId;
                    commentRowData.linesOfTokensPosition = linesOfTokensPosition;
                    commentRowData.RowClasses.Add("user-comment-thread");
                    commentRowData.Comments = commentsForRow.ToList();
                }

                toggleCommentClass = toggleCommentClass.Replace("can-show", "show");
                codePanelRowData.ToggleCommentsClasses = toggleCommentClass;
            }
            else
            {
                toggleCommentClass = (!String.IsNullOrWhiteSpace(toggleCommentClass)) ? toggleCommentClass.Replace("can-show", "show") : "bi bi-chat-right-text hide";
                codePanelRowData.ToggleCommentsClasses = toggleCommentClass;
            }
            return commentRowData;
        }

        private static void InsertCodePanelRowData(CodePanelData codePanelData, CodePanelRawData codePanelRawData, List<StructuredToken> tokensInRow,
            HashSet<string> rowClasses, HashSet<string> tokenIdsInRow, string nodeIdHashed, string nodeId, int indent, LinesOfTokensPosition linesOfTokensPosition)
        {
            var rowData = new CodePanelRowData()
            {
                Type = (rowClasses.Contains("documentation")) ? CodePanelRowDatatype.Documentation : CodePanelRowDatatype.CodeLine,
                RowOfTokens = tokensInRow,
                NodeIdHashed = nodeIdHashed,
                NodeId = nodeId,
                linesOfTokensPosition = linesOfTokensPosition,
                Indent = indent,
                DiffKind = DiffKind.NoneDiff,
            };

            // Need to collect comments before adding the row to the codePanelData
            var commentsForRow = CollectUserCommentsForRow(codePanelRawData, tokenIdsInRow, nodeId, nodeIdHashed, linesOfTokensPosition, rowData);

            if (rowData.Type == CodePanelRowDatatype.Documentation)
            {
                codePanelData.NodeMetaData[nodeIdHashed].Documentation.Add(rowData);
            }

            if (rowData.Type == CodePanelRowDatatype.CodeLine)
            {
                codePanelData.NodeMetaData[nodeIdHashed].CodeLines.Add(rowData);
            }

            if (commentsForRow != default(CodePanelRowData))
            {
                codePanelData.NodeMetaData[nodeIdHashed].CommentThread.Add(commentsForRow);
            }

            tokensInRow.Clear();
            rowClasses.Clear();
            tokenIdsInRow.Clear();
        }

        private static void AddDiagnoasticRow(CodePanelData codePanelData, CodePanelRawData codePanelRawData, string nodeId, string nodeIdHashed, LinesOfTokensPosition linesOfTokensPosition)
        {
            if (codePanelRawData.Diagnostics.Any(d => d.TargetId == nodeId) && linesOfTokensPosition != LinesOfTokensPosition.Bottom)
            {
                var diagnosticsForRow = codePanelRawData.Diagnostics.Where(d => d.TargetId == nodeId);
                foreach (var diagnosticRow in diagnosticsForRow)
                {
                    var rowData = new CodePanelRowData()
                    {
                        Type = CodePanelRowDatatype.Diagnostics,
                        NodeIdHashed = nodeIdHashed,
                        NodeId = nodeId,
                        linesOfTokensPosition = linesOfTokensPosition,
                        Diagnostics = diagnosticRow
                    };
                    codePanelData.NodeMetaData[nodeIdHashed].CommentThread.Add(rowData);
                }
            }
        }
    }
}
