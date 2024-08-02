
using APIView.TreeToken;
using APIViewWeb.Extensions;
using APIViewWeb.LeanModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APIViewWeb.Helpers
{
    public class CodeFileHelpers
    {
        private static int _processorCount = Environment.ProcessorCount;
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(_processorCount);
        private static ConcurrentBag<Task> _tasks = new ConcurrentBag<Task>();


        public static async Task<CodePanelData> GenerateCodePanelDataAsync(CodePanelRawData codePanelRawData)
        {
            var codePanelData = new CodePanelData();

            for (int idx = 0; idx < codePanelRawData.APIForest.Count; idx++)
            {
                var node = codePanelRawData.APIForest[idx];
                await BuildAPITree(codePanelData: codePanelData, codePanelRawData: codePanelRawData, apiTreeNode: node, 
                    parentNodeIdHashed: "root", nodePositionAtLevel: idx);
            };

            if (_processorCount > 1)
            {
                await Task.WhenAll(_tasks);
            }

            return codePanelData;
        }

        public static List<APITreeNode> ComputeAPIForestDiff(List<APITreeNode> activeAPIRevisionAPIForest, List<APITreeNode> diffAPIRevisionAPIForest)
        {
            List<APITreeNode> diffAPITree = new List<APITreeNode>();
            ComputeAPITreeDiff(activeAPIRevisionAPIForest, diffAPIRevisionAPIForest, diffAPITree);
            return diffAPITree;
        }

        private static void ComputeAPITreeDiff(List<APITreeNode> activeAPIRevisionAPIForest, List<APITreeNode> diffAPIRevisionAPIForest, List<APITreeNode> diffAPITree)
        {           
            var activeAPIRevisionTreeNodesAtLevel = new HashSet<APITreeNode>(activeAPIRevisionAPIForest);
            var diffAPIRevisionTreeNodesAtLevel = new HashSet<APITreeNode>(diffAPIRevisionAPIForest);

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

                diffResultNode.TopTokensObj = activeAPIRevisionNode.TopTokensObj;
                diffResultNode.BottomTokensObj = activeAPIRevisionNode.BottomTokensObj;
                diffResultNode.TopDiffTokens = diffAPIRevisionNode.TopTokensObj;
                diffResultNode.BottomDiffTokens = diffAPIRevisionNode.BottomTokensObj;

                var childrenResult = new List<APITreeNode>();
                ComputeAPITreeDiff(activeAPIRevisionNode.ChildrenObj, diffAPIRevisionNode.ChildrenObj, childrenResult);
                diffResultNode.ChildrenObj.AddRange(childrenResult);
            };
        }
        public static (List<StructuredToken> Before, List<StructuredToken> After, bool HasDiff) ComputeTokenDiff(List<StructuredToken> beforeTokens, List<StructuredToken> afterTokens)
        {
            var diffResultA = new List<StructuredToken>();
            var diffResultB = new List<StructuredToken>();
            bool hasDiff = false;

            var beforeTokensMap = beforeTokens.Select((token, index) => new { Key = $"{token.Id}{token.Value}{index}", Value = token })
                                              .ToDictionary(x => x.Key, x => x.Value);

            var afterTokensMap = afterTokens.Select((token, index) => new { Key = $"{token.Id}{token.Value}{index}", Value = token })
                                            .ToDictionary(x => x.Key, x => x.Value);

            foreach (var pair in beforeTokensMap)
            {
                if (afterTokensMap.ContainsKey(pair.Key))
                {
                    diffResultA.Add(new StructuredToken(pair.Value));
                }
                else
                {
                    if (afterTokens.Count > 0)
                    {
                        var token = new StructuredToken(pair.Value);
                        token.RenderClassesObj.Add("diff-change");
                        diffResultA.Add(token);
                    }
                    else
                    {
                        diffResultA.Add(new StructuredToken(pair.Value));
                    }
                    hasDiff = true;
                }
            }

            foreach (var pair in afterTokensMap)
            {
                if (beforeTokensMap.ContainsKey(pair.Key))
                {
                    diffResultB.Add(new StructuredToken(pair.Value));
                }
                else
                {
                    if (beforeTokens.Count > 0)
                    {
                        var token = new StructuredToken(pair.Value);
                        token.RenderClassesObj.Add("diff-change");
                        diffResultB.Add(token);
                    }
                    else
                    {
                        diffResultB.Add(new StructuredToken(pair.Value));
                    }
                    hasDiff = true;
                }
            }

            return (diffResultA, diffResultB, hasDiff);
        }
        private static APITreeNode CreateAPITreeDiffNode(APITreeNode node, DiffKind diffKind)
        {
            var result = new APITreeNode
            {
                Name = node.Name,
                Id = node.Id,
                Kind    = node.Kind,
                TagsObj = node.TagsObj,
                PropertiesObj = node.PropertiesObj,
                DiffKind = diffKind
            };

            if (diffKind == DiffKind.Added || diffKind == DiffKind.Removed)
            {
                result.TopTokensObj = node.TopTokensObj;
                result.BottomTokensObj = node.BottomTokensObj;
                result.ChildrenObj = node.ChildrenObj;
            }

            return result;
        }

        private static async Task BuildAPITree(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNode apiTreeNode, string parentNodeIdHashed, int nodePositionAtLevel, int indent = 0)
        {
            var nodeIdHashed = GetTokenNodeIdHash(apiTreeNode, RowOfTokensPosition.Top, parentNodeIdHashed);

            if (codePanelData.NodeMetaDataObj.ContainsKey(nodeIdHashed))
            {
                codePanelData.NodeMetaDataObj[nodeIdHashed].ParentNodeIdHashed = parentNodeIdHashed;
            }
            else
            {
                codePanelData.NodeMetaDataObj.TryAdd(nodeIdHashed, new CodePanelNodeMetaData());
                codePanelData.NodeMetaDataObj[nodeIdHashed].ParentNodeIdHashed = parentNodeIdHashed;
            }

            if (codePanelData.NodeMetaDataObj.ContainsKey(parentNodeIdHashed))
            {
                codePanelData.NodeMetaDataObj[parentNodeIdHashed].ChildrenNodeIdsInOrderObj.TryAdd(nodePositionAtLevel, nodeIdHashed);
            }
            else
            {
                codePanelData.NodeMetaDataObj[parentNodeIdHashed] = new CodePanelNodeMetaData();
                codePanelData.NodeMetaDataObj[parentNodeIdHashed].ChildrenNodeIdsInOrderObj.TryAdd(nodePositionAtLevel, nodeIdHashed);
            }

            if (_processorCount > 1) // Take advantage of multi-core processors
            {
                await _semaphore.WaitAsync();
                var task = Task.Run(() =>
                {
                    try
                    {
                        BuildNodeTokens(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, RowOfTokensPosition.Top, indent);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });
                _tasks.Add(task);
            }
            else
            {
                BuildNodeTokens(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, RowOfTokensPosition.Top, indent);
            }


            if (!apiTreeNode.TagsObj.Contains("HideFromNav"))
            {
                var navIcon = apiTreeNode.Kind.ToLower();
                if (apiTreeNode.PropertiesObj.ContainsKey("SubKind"))
                {
                    navIcon = apiTreeNode.PropertiesObj["SubKind"].ToLower();
                }

                if (apiTreeNode.PropertiesObj.ContainsKey("IconName"))
                {
                    navIcon = apiTreeNode.PropertiesObj["IconName"].ToLower();
                }

                var navTreeNode = new NavigationTreeNode()
                {
                    Label = apiTreeNode.Name,
                    Data = new NavigationTreeNodeData()
                    {
                        NodeIdHashed = nodeIdHashed,
                        Kind = apiTreeNode.PropertiesObj.ContainsKey("SubKind") ? apiTreeNode.PropertiesObj["SubKind"] : apiTreeNode.Kind.ToLower(),
                        Icon = navIcon,
                    },
                    Expanded = true,
                };

                if (codePanelData.NodeMetaDataObj.ContainsKey(nodeIdHashed))
                {
                    codePanelData.NodeMetaDataObj[nodeIdHashed].NavigationTreeNode = navTreeNode;
                }
                else 
                {
                    codePanelData.NodeMetaDataObj.TryAdd(nodeIdHashed, new CodePanelNodeMetaData());
                    codePanelData.NodeMetaDataObj[nodeIdHashed].NavigationTreeNode = navTreeNode;
                }
            }

            for (int idx = 0; idx < apiTreeNode.ChildrenObj.Count; idx++)
            {
                var node = apiTreeNode.ChildrenObj[idx];
                if (apiTreeNode.DiffKind == DiffKind.Added || apiTreeNode.DiffKind == DiffKind.Removed)
                {
                    node.DiffKind = apiTreeNode.DiffKind; // Propagate the diff kind to the children
                }

                await BuildAPITree(codePanelData: codePanelData, codePanelRawData: codePanelRawData, apiTreeNode: node,
                    parentNodeIdHashed: nodeIdHashed, nodePositionAtLevel: idx, indent: indent + 1);       
            };

            if (apiTreeNode.BottomTokensObj.Any())
            {
                var bottomNodeIdHashed = GetTokenNodeIdHash(apiTreeNode, RowOfTokensPosition.Bottom, parentNodeIdHashed);
                codePanelData.NodeMetaDataObj[nodeIdHashed].BottomTokenNodeIdHash = bottomNodeIdHashed;
                if (codePanelData.NodeMetaDataObj.ContainsKey(bottomNodeIdHashed))
                {
                    codePanelData.NodeMetaDataObj[bottomNodeIdHashed].ParentNodeIdHashed = parentNodeIdHashed;
                }
                else
                {
                    codePanelData.NodeMetaDataObj.TryAdd(bottomNodeIdHashed, new CodePanelNodeMetaData());
                    codePanelData.NodeMetaDataObj[bottomNodeIdHashed].ParentNodeIdHashed = parentNodeIdHashed;
                }

                BuildNodeTokens(codePanelData, codePanelRawData, apiTreeNode, bottomNodeIdHashed, RowOfTokensPosition.Bottom, indent);
            }
        }

        private static void BuildNodeTokens(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNode apiTreeNode, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition, int indent)
        {
            if (apiTreeNode.DiffKind == DiffKind.NoneDiff)
            {
                BuildTokensForNonDiffNodes(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, linesOfTokensPosition, indent);
            }
            else
            {
                BuildTokensForDiffNodes(codePanelData, codePanelRawData, apiTreeNode, nodeIdHashed, linesOfTokensPosition, indent);
            }
        }

        private static void BuildTokensForNonDiffNodes(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNode apiTreeNode, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition, int indent)
        {
            var tokensInNode = (linesOfTokensPosition == RowOfTokensPosition.Top) ? apiTreeNode.TopTokensObj : apiTreeNode.BottomTokensObj;
            var addDeprecatedTagToTokens = apiTreeNode.TagsObj.Contains("Deprecated");

            var tokensInRow = new List<StructuredToken>();
            var rowClasses = new HashSet<string>();
            var tokenIdsInRow = new HashSet<string>();

            foreach (var token in tokensInNode)
            {
                if (token.PropertiesObj.ContainsKey("GroupId"))
                {
                    rowClasses.Add(token.PropertiesObj["GroupId"]);
                }

                if (addDeprecatedTagToTokens)
                {
                    token.TagsObj.Add("Deprecated");
                }

                if (ShouldBreakLineOnToken(token, codePanelRawData.Language))
                {
                    InsertNonDiffCodePanelRowData(codePanelData: codePanelData, codePanelRawData: codePanelRawData, tokensInRow: new List<StructuredToken>(tokensInRow),
                        rowClasses: new HashSet<string>(rowClasses), tokenIdsInRow: new HashSet<string>(tokenIdsInRow), nodeIdHashed: nodeIdHashed, nodeId: apiTreeNode.Id,
                        indent: indent, linesOfTokensPosition: linesOfTokensPosition, apiTreeNode: apiTreeNode);

                    tokensInRow.Clear();
                    rowClasses.Clear();
                    tokenIdsInRow.Clear();

                    if (token.Kind == StructuredTokenKind.ParameterSeparator)
                    {
                        tokensInRow.Add(
                            new StructuredToken()
                            {
                                Kind = StructuredTokenKind.Content,
                                Value = "\u00A0\u00A0\u00A0\u00A0"
                            });
                    }
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
                InsertNonDiffCodePanelRowData(codePanelData: codePanelData, codePanelRawData: codePanelRawData, tokensInRow: new List<StructuredToken>(tokensInRow),
                    rowClasses: new HashSet<string>(rowClasses), tokenIdsInRow: new HashSet<string>(tokenIdsInRow), nodeIdHashed: nodeIdHashed, nodeId: apiTreeNode.Id,
                    indent: indent, linesOfTokensPosition: linesOfTokensPosition, apiTreeNode: apiTreeNode);

                tokensInRow.Clear();
                rowClasses.Clear();
                tokenIdsInRow.Clear();
            }

            AddDiagnosticRow(codePanelData, codePanelRawData, apiTreeNode.Id, nodeIdHashed, linesOfTokensPosition);
        }

        private static void BuildTokensForDiffNodes(CodePanelData codePanelData, CodePanelRawData codePanelRawData, APITreeNode apiTreeNode, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition, int indent)
        {
            var lineGroupBuildOrder = new List<string>() { "doc" };
            var beforeTokens = (linesOfTokensPosition == RowOfTokensPosition.Top) ? apiTreeNode.TopTokensObj : apiTreeNode.BottomTokensObj;
            var afterTokens = (linesOfTokensPosition == RowOfTokensPosition.Top) ? apiTreeNode.TopDiffTokens : apiTreeNode.BottomDiffTokens;

            if (apiTreeNode.DiffKind == DiffKind.Added)
            {
                afterTokens = (linesOfTokensPosition == RowOfTokensPosition.Top) ? apiTreeNode.TopTokensObj : apiTreeNode.BottomTokensObj;
                beforeTokens = new List<StructuredToken>();
            }

            var beforeTokensInProcess = new Queue<DiffLineInProcess>();
            var afterTokensInProcess = new Queue<DiffLineInProcess>();

            int beforeIndex = 0;
            int afterIndex = 0;

            while (beforeIndex < beforeTokens.Count || afterIndex < afterTokens.Count || beforeTokensInProcess.Count > 0 || afterTokensInProcess.Count > 0)
            {
                var beforeTokenRow = new List<StructuredToken>();
                var afterTokenRow = new List<StructuredToken>();

                var beforeRowClasses = new HashSet<string>();
                var afterRowClasses = new HashSet<string>();

                var beforeTokenIdsInRow = new HashSet<string>();
                var afterTokenIdsInRow = new HashSet<string>();

                var beforeRowGroupId = string.Empty;
                var afterRowGroupId = string.Empty;

                while (beforeIndex < beforeTokens.Count)
                {
                    var token = beforeTokens[beforeIndex++];

                    if (codePanelRawData.ApplySkipDiff && token.TagsObj.Contains(StructuredToken.SKIPP_DIFF))
                    {
                        continue;
                    }

                    if (token.Kind == StructuredTokenKind.LineBreak)
                    {
                        break;
                    }

                    if (token.PropertiesObj.ContainsKey("GroupId"))
                    {
                        beforeRowGroupId = token.PropertiesObj["GroupId"];
                    }
                    else
                    {
                        beforeRowGroupId = string.Empty;
                    }
                    beforeTokenRow.Add(token);
                    if (!String.IsNullOrWhiteSpace(token.Id))
                    {
                        beforeTokenIdsInRow.Add(token.Id);
                    }
                }

                if (beforeTokenRow.Count > 0)
                {
                    if (!(codePanelRawData.SkipDocsWhenDiffing && beforeRowGroupId == StructuredToken.DOCUMENTATION))
                    {
                        beforeTokensInProcess.Enqueue(new DiffLineInProcess()
                        {
                            GroupId = beforeRowGroupId,
                            RowOfTokens = beforeTokenRow,
                            TokenIdsInRow = new HashSet<string>(beforeTokenIdsInRow)
                        });
                    }
                    beforeTokenIdsInRow.Clear();
                }

                while (afterIndex < afterTokens.Count)
                {
                    var token = afterTokens[afterIndex++];

                    if (codePanelRawData.ApplySkipDiff && token.TagsObj.Contains(StructuredToken.SKIPP_DIFF))
                    {
                        continue;
                    }

                    if (token.Kind == StructuredTokenKind.LineBreak)
                    {
                        break;
                    }

                    if (token.PropertiesObj.ContainsKey("GroupId"))
                    {
                        afterRowGroupId = token.PropertiesObj["GroupId"];
                    }
                    else
                    {
                        afterRowGroupId = string.Empty;
                    }
                    afterTokenRow.Add(token);
                    if (!String.IsNullOrWhiteSpace(token.Id))
                    {
                        afterTokenIdsInRow.Add(token.Id);
                    }   
                }

                if (afterTokenRow.Count > 0)
                {
                    if (!(codePanelRawData.SkipDocsWhenDiffing && afterRowGroupId == StructuredToken.DOCUMENTATION)) 
                    {
                        afterTokensInProcess.Enqueue(new DiffLineInProcess()
                        {
                            GroupId = afterRowGroupId,
                            RowOfTokens = afterTokenRow,
                            TokenIdsInRow = new HashSet<string>(afterTokenIdsInRow)
                        });
                    }
                    afterTokenIdsInRow.Clear();
                }

                if (beforeTokensInProcess.Count > 0 || afterTokensInProcess.Count > 0)
                {
                    var beforeDiffTokens = new List<StructuredToken>();
                    var afterDiffTokens = new List<StructuredToken>();

                    var beforeTokenIdsInDiffRow = new HashSet<string>();
                    var afterTokenIdsInDiffRow = new HashSet<string>();

                    if (beforeTokensInProcess.Count > 0 && afterTokensInProcess.Count > 0)
                    {
                        if (beforeTokensInProcess.Peek().GroupId == afterTokensInProcess.Peek().GroupId)
                        {
                            if (!String.IsNullOrWhiteSpace(beforeTokensInProcess.Peek().GroupId))
                            {
                                beforeRowClasses.Add(beforeTokensInProcess.Peek().GroupId);
                            }

                            if (!String.IsNullOrWhiteSpace(afterTokensInProcess.Peek().GroupId))
                            {
                                afterRowClasses.Add(afterTokensInProcess.Peek().GroupId);
                            }

                            beforeTokenIdsInDiffRow = beforeTokensInProcess.Peek().TokenIdsInRow;
                            afterTokenIdsInDiffRow = afterTokensInProcess.Peek().TokenIdsInRow;
                            beforeDiffTokens = beforeTokensInProcess.Dequeue().RowOfTokens;
                            afterDiffTokens = afterTokensInProcess.Dequeue().RowOfTokens;
                        }
                        else
                        {
                            var beforeTokenRowBuildOrder = lineGroupBuildOrder.IndexOf(beforeTokensInProcess.Peek().GroupId);
                            var afterTokenRowBuildOrder = lineGroupBuildOrder.IndexOf(afterTokensInProcess.Peek().GroupId);
                            if ((afterTokenRowBuildOrder < 0) || (beforeTokenRowBuildOrder >= 0 && beforeTokenRowBuildOrder < afterTokenRowBuildOrder))
                            {
                                if (!String.IsNullOrWhiteSpace(beforeTokensInProcess.Peek().GroupId))
                                {
                                    beforeRowClasses.Add(beforeTokensInProcess.Peek().GroupId);
                                }
                                beforeTokenIdsInDiffRow = beforeTokensInProcess.Peek().TokenIdsInRow;
                                beforeDiffTokens = beforeTokensInProcess.Dequeue().RowOfTokens;
                            }
                            else
                            {
                                if (!String.IsNullOrWhiteSpace(afterTokensInProcess.Peek().GroupId))
                                {
                                    afterRowClasses.Add(afterTokensInProcess.Peek().GroupId);
                                }
                                afterTokenIdsInDiffRow = afterTokensInProcess.Peek().TokenIdsInRow;
                                afterDiffTokens = afterTokensInProcess.Dequeue().RowOfTokens;
                            }
                        }
                    }
                    else if (beforeTokensInProcess.Count > 0)
                    {
                        if (!String.IsNullOrWhiteSpace(beforeTokensInProcess.Peek().GroupId))
                        {
                            beforeRowClasses.Add(beforeTokensInProcess.Peek().GroupId);
                        }
                        beforeTokenIdsInDiffRow = beforeTokensInProcess.Peek().TokenIdsInRow;
                        beforeDiffTokens = beforeTokensInProcess.Dequeue().RowOfTokens;
                    }
                    else
                    {
                        if (!String.IsNullOrWhiteSpace(afterTokensInProcess.Peek().GroupId))
                        {
                            afterRowClasses.Add(afterTokensInProcess.Peek().GroupId);
                        }
                        afterTokenIdsInDiffRow = afterTokensInProcess.Peek().TokenIdsInRow;
                        afterDiffTokens = afterTokensInProcess.Dequeue().RowOfTokens;
                    }

                    var diffTokenRowResult = ComputeTokenDiff(beforeDiffTokens, afterDiffTokens);

                    if (diffTokenRowResult.HasDiff)
                    {
                        codePanelData.HasDiff = true;
                        codePanelData.NodeMetaDataObj[nodeIdHashed].IsNodeWithDiff = true;
                        var parentNodeIdHashed = codePanelData.NodeMetaDataObj[nodeIdHashed].ParentNodeIdHashed;
                        while (parentNodeIdHashed != "root" && codePanelData.NodeMetaDataObj.ContainsKey(parentNodeIdHashed))
                        {
                            var parentNode = codePanelData.NodeMetaDataObj[parentNodeIdHashed];
                            parentNode.IsNodeWithDiffInDescendants = true;

                            if ((diffTokenRowResult.Before.Any() && !beforeRowClasses.Contains("doc")) || 
                                (diffTokenRowResult.After.Any() && !afterRowClasses.Contains("doc")))
                            {
                                parentNode.IsNodeWithNoneDocDiffInDescendants = true;
                            }

                            parentNodeIdHashed = parentNode.ParentNodeIdHashed;
                        }

                        if (diffTokenRowResult.Before.Count > 0)
                        {
                            beforeRowClasses.Add("removed");
                            var rowData = new CodePanelRowData()
                            {
                                Type = (beforeRowClasses.Contains("doc")) ? CodePanelRowDatatype.Documentation : CodePanelRowDatatype.CodeLine,
                                RowOfTokensObj = diffTokenRowResult.Before,
                                NodeIdHashed = nodeIdHashed,
                                NodeId = apiTreeNode.Id,
                                RowClassesObj = new HashSet<string>(beforeRowClasses),
                                RowOfTokensPosition = linesOfTokensPosition,
                                Indent = indent,
                                DiffKind = DiffKind.Removed
                            };

                            // Collects comments for the line, needed to set correct icons
                            var commentsForRow = CollectUserCommentsForRow(codePanelRawData, new HashSet<string>(), apiTreeNode.Id, nodeIdHashed, linesOfTokensPosition, rowData);

                            InsertCodePanelRowData(codePanelData: codePanelData, rowData: rowData, nodeIdHashed: nodeIdHashed);
                            beforeRowClasses.Clear();
                        }

                        if (diffTokenRowResult.After.Count > 0)
                        {
                            afterRowClasses.Add("added");
                            var rowData = new CodePanelRowData()
                            {
                                Type = (afterRowClasses.Contains("doc")) ? CodePanelRowDatatype.Documentation : CodePanelRowDatatype.CodeLine,
                                RowOfTokensObj = diffTokenRowResult.After,
                                NodeIdHashed = nodeIdHashed,
                                NodeId = apiTreeNode.Id,
                                RowClassesObj = new HashSet<string>(afterRowClasses),
                                RowOfTokensPosition = linesOfTokensPosition,
                                Indent = indent,
                                DiffKind = DiffKind.Added
                            };

                            var commentsForRow = CollectUserCommentsForRow(codePanelRawData, afterTokenIdsInDiffRow, apiTreeNode.Id, nodeIdHashed, linesOfTokensPosition, rowData);
                            InsertCodePanelRowData(codePanelData: codePanelData, rowData: rowData, nodeIdHashed: nodeIdHashed, commentsForRow: commentsForRow);
                            afterRowClasses.Clear();
                        }
                    }
                    else
                    {
                        if (diffTokenRowResult.Before.Count > 0) 
                        {
                            var rowData = new CodePanelRowData()
                            {
                                Type = (beforeRowClasses.Contains("doc")) ? CodePanelRowDatatype.Documentation : CodePanelRowDatatype.CodeLine,
                                RowOfTokensObj = diffTokenRowResult.Before,
                                NodeIdHashed = nodeIdHashed,
                                NodeId = apiTreeNode.Id,
                                RowClassesObj = new HashSet<string>(beforeRowClasses),
                                RowOfTokensPosition = linesOfTokensPosition,
                                Indent = indent,
                                DiffKind = DiffKind.Unchanged
                            };
                            var commentsForRow = CollectUserCommentsForRow(codePanelRawData, beforeTokenIdsInDiffRow, apiTreeNode.Id, nodeIdHashed, linesOfTokensPosition, rowData);
                            InsertCodePanelRowData(codePanelData: codePanelData, rowData: rowData, nodeIdHashed: nodeIdHashed, commentsForRow: commentsForRow);
                            beforeRowClasses.Clear();
                        }
                    }
                }
            }

            AddDiagnosticRow(codePanelData: codePanelData, codePanelRawData: codePanelRawData, nodeId: apiTreeNode.Id, nodeIdHashed: nodeIdHashed, linesOfTokensPosition: linesOfTokensPosition);
        }
        
        private static string GetTokenNodeIdHash(APITreeNode apiTreeNode, RowOfTokensPosition linesOfTokensPosition, string parentNodeIdHash)
        {
            var idPart = apiTreeNode.Kind;

            if (apiTreeNode.PropertiesObj.ContainsKey("SubKind"))
            {
                idPart = $"{idPart}-{apiTreeNode.PropertiesObj["SubKind"]}";
            }
            idPart = $"{idPart}-{apiTreeNode.Id}";
            idPart = $"{idPart}-{apiTreeNode.DiffKind}";
            idPart = $"{idPart}-{linesOfTokensPosition.ToString()}";
            var hash = CreateHashFromString(idPart);
            return hash + parentNodeIdHash.Replace("nId", "").Replace("root", ""); // Apend the parent node Id to ensure uniqueness
        }

        private static string CreateHashFromString(string inputString)
        {
            int hash = HashCode.Combine(inputString);
            string nodeIdhashed = "nId" + hash.ToString();
            return nodeIdhashed;
        }

        private static CodePanelRowData CollectUserCommentsForRow(CodePanelRawData codePanelRawData, HashSet<string> tokenIdsInRow, string nodeId, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition, CodePanelRowData codePanelRowData)
        {
            var commentRowData = new CodePanelRowData();
            //var toggleCommentClass = (codePanelRawData.Diagnostics.Any(d => d.TargetId == nodeId) && codePanelRowData.Type == CodePanelRowDatatype.CodeLine) ? "bi bi-chat-right-text show" : "";

            if (tokenIdsInRow.Any())
            {
                codePanelRowData.ToggleCommentsClasses = "bi bi-chat-right-text can-show";

                var commentsForRow = codePanelRawData.Comments.Where(c => tokenIdsInRow.Contains(c.ElementId));
                if (commentsForRow.Any())
                {
                    commentRowData.Type = CodePanelRowDatatype.CommentThread;
                    commentRowData.NodeIdHashed = nodeIdHashed;
                    commentRowData.NodeId = nodeId;
                    commentRowData.RowOfTokensPosition = linesOfTokensPosition;
                    commentRowData.RowClassesObj.Add("user-comment-thread");
                    commentRowData.CommentsObj = commentsForRow.ToList();
                    codePanelRowData.ToggleCommentsClasses = codePanelRowData.ToggleCommentsClasses.Replace("can-show", "show");
                    commentRowData.IsResolvedCommentThread = commentsForRow.Any(c => c.IsResolved);
                }
            }
            else
            {
                codePanelRowData.ToggleCommentsClasses = "bi bi-chat-right-text hide";
            }
            return commentRowData;
        }

        private static void InsertNonDiffCodePanelRowData(CodePanelData codePanelData, CodePanelRawData codePanelRawData, List<StructuredToken> tokensInRow,
            HashSet<string> rowClasses, HashSet<string> tokenIdsInRow, string nodeIdHashed, string nodeId, int indent, RowOfTokensPosition linesOfTokensPosition, APITreeNode apiTreeNode)
        {
            var rowData = new CodePanelRowData()
            {
                Type = (rowClasses.Contains("doc")) ? CodePanelRowDatatype.Documentation : CodePanelRowDatatype.CodeLine,
                RowOfTokensObj = tokensInRow,
                NodeIdHashed = nodeIdHashed,
                RowClassesObj = new HashSet<string>(rowClasses),
                NodeId = nodeId,
                RowOfTokensPosition = linesOfTokensPosition,
                Indent = indent,
                DiffKind = DiffKind.NoneDiff,
                IsHiddenAPI = apiTreeNode.TagsObj.Contains("Hidden")
            };

            // Need to collect comments before adding the row to the codePanelData
            var commentsForRow = CollectUserCommentsForRow(codePanelRawData, tokenIdsInRow, nodeId, nodeIdHashed, linesOfTokensPosition, rowData);

            InsertCodePanelRowData(codePanelData: codePanelData, rowData: rowData, nodeIdHashed: nodeIdHashed, commentsForRow: commentsForRow);
        }

        private static void InsertCodePanelRowData(CodePanelData codePanelData, CodePanelRowData rowData, string nodeIdHashed, CodePanelRowData commentsForRow = null)
        {
            if (rowData.Type == CodePanelRowDatatype.Documentation)
            {
                if (codePanelData.NodeMetaDataObj.ContainsKey(nodeIdHashed))
                {
                    rowData.RowPositionInGroup = codePanelData.NodeMetaDataObj[nodeIdHashed].DocumentationObj.Count();
                    codePanelData.NodeMetaDataObj[nodeIdHashed].DocumentationObj.Add(rowData);
                }
                else
                {
                    codePanelData.NodeMetaDataObj.TryAdd(nodeIdHashed, new CodePanelNodeMetaData());
                    rowData.RowPositionInGroup = codePanelData.NodeMetaDataObj[nodeIdHashed].DocumentationObj.Count();
                    codePanelData.NodeMetaDataObj[nodeIdHashed].DocumentationObj.Add(rowData);
                }
            }

            if (rowData.Type == CodePanelRowDatatype.CodeLine)
            {
                if (codePanelData.NodeMetaDataObj.ContainsKey(nodeIdHashed))
                {
                    rowData.RowPositionInGroup = codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Count();
                    codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Add(rowData);
                }
                else
                {
                    codePanelData.NodeMetaDataObj.TryAdd(nodeIdHashed, new CodePanelNodeMetaData());
                    rowData.RowPositionInGroup = codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Count();
                    codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Add(rowData);
                }
            }

            if (commentsForRow != null && commentsForRow.Type == CodePanelRowDatatype.CommentThread && commentsForRow.CommentsObj.Any())
            {
                commentsForRow.AssociatedRowPositionInGroup = rowData.RowPositionInGroup;
                if (codePanelData.NodeMetaDataObj.ContainsKey(nodeIdHashed))
                {
                    codePanelData.NodeMetaDataObj[nodeIdHashed].CommentThreadObj.TryAdd(codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Count() - 1, commentsForRow); //Map comment to the index of associated codeLine
                }
                else
                {
                    codePanelData.NodeMetaDataObj.TryAdd(nodeIdHashed, new CodePanelNodeMetaData());
                    codePanelData.NodeMetaDataObj[nodeIdHashed].CommentThreadObj.TryAdd(codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Count() - 1, commentsForRow); //Map comment to the index of associated codeLine
                }
            }
        }

        private static void AddDiagnosticRow(CodePanelData codePanelData, CodePanelRawData codePanelRawData, string nodeId, string nodeIdHashed, RowOfTokensPosition linesOfTokensPosition)
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
                        RowClassesObj = new HashSet<string>() { "diagnostics", diagnostic.Level.ToString().ToLower() }
                    };
                    codePanelData.NodeMetaDataObj[nodeIdHashed].DiagnosticsObj.Add(rowData);
                }
            }
        }

        private static bool ShouldBreakLineOnToken(StructuredToken token, string language)
        {
            if (token.Kind == StructuredTokenKind.LineBreak ||
                (token.Kind == StructuredTokenKind.ParameterSeparator && LanguageServiceHelpers.UseLineBreakForParameterSeparator(language)))
            {
                return true;
            }
            return false;
        }
    }
}
