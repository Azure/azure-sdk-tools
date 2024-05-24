
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

            for (int idx = 0; idx < codePanelRawData.APIForest.Count; idx++)
            {
                var node = codePanelRawData.APIForest[idx];
                BuildAPITree(codePanelData: codePanelData, codePanelRawData: codePanelRawData, apiTreeNode: node, 
                    parentNodeIdHashed: "root", nodePositionAtLevel: idx);
            };

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

        private static void BuildAPITree(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNodeForAPI apiTreeNode, string parentNodeIdHashed, int nodePositionAtLevel, int indent = 0)
        {
            var nodeIdHashed = GetTokenNodeIdHash(apiTreeNode, RowOfTokensPosition.Top);

            if (codePanelData.NodeMetaData.ContainsKey(nodeIdHashed))
            {
                codePanelData.NodeMetaData[nodeIdHashed].ParentNodeId = parentNodeIdHashed;
            }
            else
            {
                codePanelData.NodeMetaData[nodeIdHashed] = new CodePanelNodeMetaData()
                {
                    ParentNodeId = parentNodeIdHashed
                };
            }

            if (codePanelData.NodeMetaData.ContainsKey(parentNodeIdHashed))
            {
                codePanelData.NodeMetaData[parentNodeIdHashed].ChildrenNodeIdsInOrder.Add(nodePositionAtLevel, nodeIdHashed);
            }
            else
            {
                codePanelData.NodeMetaData[parentNodeIdHashed] = new CodePanelNodeMetaData();
                codePanelData.NodeMetaData[parentNodeIdHashed].ChildrenNodeIdsInOrder.Add(nodePositionAtLevel, nodeIdHashed);
            }
            
            BuildNodeTokens(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, RowOfTokensPosition.Top, indent);

            if (!apiTreeNode.Tags.Contains("HideFromNavigation"))
            {
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
                        Kind = apiTreeNode.Properties.ContainsKey("SubKind") ? apiTreeNode.Properties["SubKind"] : apiTreeNode.Kind.ToLower(),
                        Icon = navIcon,
                    },
                    Expanded = true,
                };

                if (codePanelData.NodeMetaData.ContainsKey(nodeIdHashed))
                {
                    codePanelData.NodeMetaData[nodeIdHashed].NavigationTreeNode = navTreeNode;
                }
                else 
                {
                    codePanelData.NodeMetaData[nodeIdHashed] = new CodePanelNodeMetaData()
                    {
                        NavigationTreeNode = navTreeNode
                    };
                }
            }

            for (int idx = 0; idx < apiTreeNode.Children.Count; idx++) {
                var node = apiTreeNode.Children[idx];
                BuildAPITree(codePanelData: codePanelData, codePanelRawData: codePanelRawData, apiTreeNode: node, 
                    parentNodeIdHashed: nodeIdHashed, nodePositionAtLevel: idx, indent: indent + 1);
            };

            if (apiTreeNode.BottomTokens.Any())
            {
                var bottomNodeIdHashed = GetTokenNodeIdHash(apiTreeNode, RowOfTokensPosition.Bottom);
                codePanelData.NodeMetaData[nodeIdHashed].BottomTokenNodeIdHash = bottomNodeIdHashed;
                if (codePanelData.NodeMetaData.ContainsKey(bottomNodeIdHashed))
                {
                    codePanelData.NodeMetaData[bottomNodeIdHashed].ParentNodeId = parentNodeIdHashed;
                }
                else
                {
                    codePanelData.NodeMetaData[bottomNodeIdHashed] = new CodePanelNodeMetaData()
                    {
                        ParentNodeId = parentNodeIdHashed,
                    };
                }

                BuildNodeTokens(codePanelData, codePanelRawData, apiTreeNode, bottomNodeIdHashed, RowOfTokensPosition.Bottom, indent);
            }
        }

        private static void BuildNodeTokens(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNodeForAPI apiTreeNode, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition, int indent)
        {
            if (apiTreeNode.DiffKind == DiffKind.NoneDiff)
            {
                BuildTokensForNonDiffNodes(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, linesOfTokensPosition, indent);
            }
            else
            { 
            }
        }

        private static void BuildTokensForNonDiffNodes(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNodeForAPI apiTreeNode, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition, int indent)
        {
            var tokensInNode = (linesOfTokensPosition == RowOfTokensPosition.Top) ? apiTreeNode.TopTokens : apiTreeNode.BottomTokens;

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
                    InsertCodePanelRowData(codePanelData: codePanelData, codePanelRawData: codePanelRawData, tokensInRow: new List<StructuredToken>(tokensInRow),
                        rowClasses: new HashSet<string>(rowClasses), tokenIdsInRow: new HashSet<string>(tokenIdsInRow), nodeIdHashed: nodeIdHashed, nodeId: apiTreeNode.Id, indent: indent, linesOfTokensPosition: linesOfTokensPosition);

                    tokensInRow.Clear();
                    rowClasses.Clear();
                    tokenIdsInRow.Clear();
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
                InsertCodePanelRowData(codePanelData: codePanelData, codePanelRawData: codePanelRawData, tokensInRow: new List<StructuredToken>(tokensInRow),
                    rowClasses: new HashSet<string>(rowClasses), tokenIdsInRow: new HashSet<string>(tokenIdsInRow), nodeIdHashed: nodeIdHashed, nodeId: apiTreeNode.Id, indent: indent, linesOfTokensPosition: linesOfTokensPosition);

                tokensInRow.Clear();
                rowClasses.Clear();
                tokenIdsInRow.Clear();
            }

            AddDiagnoasticRow(codePanelData, codePanelRawData, apiTreeNode.Id, nodeIdHashed, linesOfTokensPosition);
        }

        private static string GetTokenNodeIdHash(APITreeNodeForAPI apiTreeNode, RowOfTokensPosition linesOfTokensPosition)
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

        private static CodePanelRowData CollectUserCommentsForRow(CodePanelRawData codePanelRawData, HashSet<string> tokenIdsInRow, string nodeId, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition, CodePanelRowData codePanelRowData)
        {
            var commentRowData = new CodePanelRowData();
            var toggleCommentClass = (codePanelRawData.Diagnostics.Any(d => d.TargetId == nodeId)) ? "bi bi-chat-right-text show" : "";

            if (tokenIdsInRow.Any())
            {
                toggleCommentClass = (String.IsNullOrWhiteSpace(toggleCommentClass)) ? "bi bi-chat-right-text can-show" : toggleCommentClass;
                codePanelRowData.ToggleCommentsClasses = toggleCommentClass;

                var commentsForRow = codePanelRawData.Comments.Where(c => tokenIdsInRow.Contains(c.ElementId));
                if (commentsForRow.Any())
                {
                    commentRowData.Type = CodePanelRowDatatype.CommentThread;
                    commentRowData.NodeIdHashed = nodeIdHashed;
                    commentRowData.NodeId = nodeId;
                    commentRowData.RowOfTokensPosition = linesOfTokensPosition;
                    commentRowData.RowClasses.Add("user-comment-thread");
                    commentRowData.Comments = commentsForRow.ToList();
                    toggleCommentClass = toggleCommentClass.Replace("can-show", "show");
                    codePanelRowData.ToggleCommentsClasses = toggleCommentClass;
                }
            }
            else
            {
                toggleCommentClass = (!String.IsNullOrWhiteSpace(toggleCommentClass)) ? toggleCommentClass.Replace("can-show", "show") : "bi bi-chat-right-text hide";
                codePanelRowData.ToggleCommentsClasses = toggleCommentClass;
            }
            return commentRowData;
        }

        private static void InsertCodePanelRowData(CodePanelData codePanelData, CodePanelRawData codePanelRawData, List<StructuredToken> tokensInRow,
            HashSet<string> rowClasses, HashSet<string> tokenIdsInRow, string nodeIdHashed, string nodeId, int indent, RowOfTokensPosition linesOfTokensPosition)
        {
            var rowData = new CodePanelRowData()
            {
                Type = (rowClasses.Contains("documentation")) ? CodePanelRowDatatype.Documentation : CodePanelRowDatatype.CodeLine,
                RowOfTokens = tokensInRow,
                NodeIdHashed = nodeIdHashed,
                NodeId = nodeId,
                RowOfTokensPosition = linesOfTokensPosition,
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

            if (commentsForRow.Type == CodePanelRowDatatype.CommentThread && commentsForRow.Comments.Any())
            {
                codePanelData.NodeMetaData[nodeIdHashed].CommentThread.Add(commentsForRow);
            }
        }

        private static void AddDiagnoasticRow(CodePanelData codePanelData, CodePanelRawData codePanelRawData, string nodeId, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition)
        {
            if (codePanelRawData.Diagnostics.Any(d => d.TargetId == nodeId) && linesOfTokensPosition != RowOfTokensPosition.Bottom)
            {
                var diagnosticsForRow = codePanelRawData.Diagnostics.Where(d => d.TargetId == nodeId);
                foreach (var diagnostic in diagnosticsForRow)
                {

                    var rowData = new CodePanelRowData()
                    {
                        Type = CodePanelRowDatatype.Diagnostics,
                        NodeIdHashed = nodeIdHashed,
                        NodeId = nodeId,
                        RowOfTokensPosition = linesOfTokensPosition,
                        Diagnostics = diagnostic,
                        RowClasses = new HashSet<string>() { "diagnostic", diagnostic.Level.ToString().ToLower() }
                    };
                    codePanelData.NodeMetaData[nodeIdHashed].Diagnostics.Add(rowData);
                }
            }
        }
    }
}
