
using ApiView;
using APIView;
using APIView.Model.V2;
using APIView.TreeToken;
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
        public static async Task<CodePanelData> GenerateCodePanelDataAsync(CodePanelRawData codePanelRawData)
        {
            var codePanelData = new CodePanelData();
            var reviewLines = codePanelRawData.activeRevisionCodeFile.ReviewLines;

            // Create root node
            var rootNodeId = "root";
            codePanelData.NodeMetaDataObj[rootNodeId] = new CodePanelNodeMetaData();
            var codeFile = codePanelRawData.activeRevisionCodeFile;
            codePanelData.AddNavigation(rootNodeId, CreateRootNode($"{codeFile.PackageName} {codeFile.PackageVersion}", rootNodeId));

            //Collect documentation lines from active revision
            CollectDocumentationLines(codePanelData, codeFile.ReviewLines, codePanelData.ActiveDocumentationMap, 1, "root");

            //Calculate the diff if diff revision code file is present
            if (codePanelRawData.diffRevisionCodeFile != null)
            {
                var diffLines = codePanelRawData.diffRevisionCodeFile.ReviewLines;
                CollectDocumentationLines(codePanelData, diffLines, codePanelData.DiffDocumentationMap, 1, "root", true);
                // Check if diff is required for active revision and diff revision to avoid unnecessary diff calculation
                bool hasSameApis = AreCodeFilesSame(codePanelRawData.activeRevisionCodeFile, codePanelRawData.diffRevisionCodeFile);
                if (!hasSameApis)
                {
                    reviewLines = FindDiff(reviewLines, codePanelRawData.diffRevisionCodeFile.ReviewLines);
                    // Remap nodeIdHashed for documentation to diff adjusted nodeIdHashed so that documentation is correctly listed on review.
                    RemapDocumentationLines(reviewLines, codePanelData.ActiveDocumentationMap);
                    // Remap documentation is diff revision using node hash ID for active revision. We don't need to show documentation if it's node itself is not present in active revision.
                    RemapDocumentationLines(reviewLines, codePanelData.DiffDocumentationMap);
                    codePanelData.HasDiff = true;
                }
                else
                {
                    codePanelData.HasDiff = false;
                }
            }

            int idx = 0;
            string nodeHashId = "";
            Dictionary<string, string> relatedLineMap = new Dictionary<string, string>();
            foreach (var reviewLine in reviewLines)
            {
                if (reviewLine.IsDocumentation) continue;
                nodeHashId = await BuildAPITree(codePanelData: codePanelData, codePanelRawData: codePanelRawData, reviewLine: reviewLine,
                    parentNodeIdHashed: rootNodeId, nodePositionAtLevel: idx, prevNodeHashId: nodeHashId, relatedLineMap: relatedLineMap);
                idx++;
            }

            //Set related line's node ID hashed in tree metadata
            foreach (var key in relatedLineMap.Keys)
            {
                codePanelData.SetLineAsRelated(key, relatedLineMap[key]);
            }

            // Create navigation tree using information is code file for backward compatibility when existing code file object is converted to new model.
            CreateNavigationTree(codePanelData, codePanelRawData.activeRevisionCodeFile);
            return codePanelData;
        }


        private static async Task<string> BuildAPITree(CodePanelData codePanelData, CodePanelRawData codePanelRawData, ReviewLine reviewLine, string parentNodeIdHashed, int nodePositionAtLevel,
            string prevNodeHashId, Dictionary<string, string> relatedLineMap, int indent = 1)
        {
            //Create hashed node ID for current review line(node)
            var nodeIdHashed = reviewLine.GetTokenNodeIdHash(parentNodeIdHashed, nodePositionAtLevel);
            codePanelData.AddLineIdNodeHashMapping(reviewLine.LineId, nodeIdHashed);
            //Create parent and child tree reference map
            codePanelData.ConnectNodeToParent(nodeIdHashed, parentNodeIdHashed, nodePositionAtLevel);

            //Populate the map of nodeHashId to it's related line ID
            // This is later used to set related line's node ID hashed in tree metadata since related tree node is built after current node.
            if (!string.IsNullOrEmpty(reviewLine.RelatedToLine))
            {
                relatedLineMap[nodeIdHashed] = reviewLine.RelatedToLine;
            }

            // Build current code line node
            BuildNodeTokens(codePanelData, codePanelRawData, reviewLine, nodeIdHashed, indent);

            //Create navigation node for current line
            var navTreeNode = CreateNavigationNode(reviewLine, nodeIdHashed);
            if (navTreeNode != null)
            {
                codePanelData.AddNavigation(nodeIdHashed, navTreeNode);
            }

            // Process all child lines
            int idx = 0;
            string childNodeHashId = "";
            foreach (var childLine in reviewLine.Children)
            {
                if (childLine.IsDocumentation) continue;

                childNodeHashId = await BuildAPITree(codePanelData: codePanelData, codePanelRawData: codePanelRawData, reviewLine: childLine,
                    parentNodeIdHashed: nodeIdHashed, nodePositionAtLevel: idx, prevNodeHashId: childNodeHashId, relatedLineMap: relatedLineMap, indent: indent + 1);
                idx++;
            };

            // Set current node as bottom node if it is end of context line.
            if (reviewLine.IsContextEndLine == true && !string.IsNullOrEmpty(prevNodeHashId))
            {
                //Set current line as bottom token if it is end of context line.
                codePanelData.NodeMetaDataObj[prevNodeHashId].BottomTokenNodeIdHash = nodeIdHashed;
                codePanelData.NodeMetaDataObj[nodeIdHashed].RelatedNodeIdHash = prevNodeHashId;
                if (reviewLine.DiffKind != DiffKind.NoneDiff)
                {
                    codePanelData.NodeMetaDataObj[prevNodeHashId].IsNodeWithDiffInDescendants = true;
                    codePanelData.NodeMetaDataObj[prevNodeHashId].IsNodeWithNoneDocDiffInDescendants = true;
                }
                //Copy added removed classes from parent node to bottom node.
                var classes = codePanelData.NodeMetaDataObj[prevNodeHashId].CodeLinesObj.LastOrDefault()?.RowClassesObj;
                if (classes != null && reviewLine.DiffKind == DiffKind.NoneDiff)
                {
                    classes = classes.Where(c => c == "added" || c == "removed").ToHashSet();
                    codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.LastOrDefault()?.RowClassesObj.UnionWith(classes);
                }
            }

            //Set previous node as related if current line is empty and if parser didn't set a related line ID for empty line.
            if (reviewLine.Tokens.Count == 0 && string.IsNullOrEmpty(reviewLine.RelatedToLine))
            {
                codePanelData.NodeMetaDataObj[nodeIdHashed].RelatedNodeIdHash = prevNodeHashId;
            }

            return nodeIdHashed;
        }

        // Create navigation node for current line if applicable
        private static NavigationTreeNode CreateNavigationNode(ReviewLine reviewLine, string nodeIdHashed)
        {
            NavigationTreeNode navTreeNode = null;
            //Generate navigation node only from active revision
            if (!reviewLine.IsActiveRevisionLine)
                return navTreeNode;
            var navToken = reviewLine.Tokens.FirstOrDefault(t => !string.IsNullOrEmpty(t.NavigationDisplayName));
            if (navToken != null && reviewLine.IsHidden != true)
            {
                string navIcon = "";
                if (navToken.RenderClasses.Count > 0)
                {
                    navIcon = navToken.RenderClasses.First();
                }
                navTreeNode = new NavigationTreeNode()
                {
                    Label = navToken.NavigationDisplayName,
                    Data = new NavigationTreeNodeData()
                    {
                        NodeIdHashed = nodeIdHashed,
                        Kind = navIcon,
                        Icon = navIcon
                    },
                    Expanded = true,
                };
            }
            return navTreeNode;
        }

        private static NavigationTreeNode CreateRootNode(string rootName, string nodeIdHashed)
        {
            var rootClass = "assembly";
            var navTreeNode = new NavigationTreeNode()
            {
                Label = rootName,
                Data = new NavigationTreeNodeData()
                {
                    NodeIdHashed = nodeIdHashed,
                    Kind = rootClass,
                    Icon = rootClass,
                },
                Expanded = true,
            };
            return navTreeNode;
        }

        private static void BuildNodeTokens(CodePanelData codePanelData, CodePanelRawData codePanelRawData, ReviewLine reviewLine, string nodeIdHashed, int indent)
        {
            // Generate code line row
            var codePanelRow = GetCodePanelRowData(codePanelData, reviewLine, nodeIdHashed, indent);

            // Add documentation rows to code panel data
            if (codePanelData.ActiveDocumentationMap.ContainsKey(nodeIdHashed))
            {
                var activeDocLines = codePanelData.ActiveDocumentationMap[nodeIdHashed];
                var diffDocLines = codePanelData.DiffDocumentationMap.ContainsKey(nodeIdHashed) ? codePanelData.DiffDocumentationMap[nodeIdHashed] : new List<CodePanelRowData>();
                var docLines = activeDocLines.InterleavedUnion(diffDocLines);
                var docsIntersect = new HashSet<CodePanelRowData>(diffDocLines.Intersect(activeDocLines));
                var activeDocs = new HashSet<CodePanelRowData>(activeDocLines);
                bool skipDocDiff = diffDocLines.Count == 0 || activeDocLines.Count == 0;
                foreach (var docRow in docLines)
                {
                    if (!skipDocDiff && !docsIntersect.Contains(docRow))
                    {
                        if (activeDocs.Contains(docRow))
                        {
                            docRow.DiffKind = DiffKind.Added;
                            docRow.RowClassesObj.Add("added");
                        }
                        else
                        {
                            docRow.DiffKind = DiffKind.Removed;
                            docRow.RowClassesObj.Add("removed");
                        }
                    }
                    docRow.NodeId = codePanelRow.NodeId;
                    docRow.NodeIdHashed = codePanelRow.NodeIdHashed;
                    InsertCodePanelRowData(codePanelData: codePanelData, rowData: docRow, nodeIdHashed: nodeIdHashed);
                }
            }

            // Get comment for current row
            var commentsForRow = CollectUserCommentsForRow(codePanelRawData, reviewLine.LineId, nodeIdHashed, codePanelRow);
            //Add code line and comment to code panel data
            InsertCodePanelRowData(codePanelData: codePanelData, rowData: codePanelRow, nodeIdHashed: nodeIdHashed, commentsForRow: commentsForRow);

            // Add diagnostic row
            AddDiagnosticRow(codePanelData: codePanelData, codeFile: codePanelRawData.activeRevisionCodeFile, nodeId: reviewLine.LineId, nodeIdHashed: nodeIdHashed);
        }

        private static CodePanelRowData GetCodePanelRowData(CodePanelData codePanelData, ReviewLine reviewLine, string nodeIdHashed, int indent)
        {
            CodePanelRowData codePanelRowData = new()
            {
                Type = reviewLine.Tokens.Any(t => t.IsDocumentation == true) ? CodePanelRowDatatype.Documentation : CodePanelRowDatatype.CodeLine,
                NodeIdHashed = nodeIdHashed,
                NodeId = reviewLine.LineId,
                Indent = indent,
                DiffKind = reviewLine.DiffKind,
                IsHiddenAPI = (reviewLine.IsHidden == true)
            };

            var tokensInRow = codePanelRowData.RowOfTokensObj;
            var rowClasses = codePanelRowData.RowClassesObj;

            //Add empty line for review line without tokens
            if (reviewLine.Tokens.Count == 0)
            {
                tokensInRow.Add(StructuredToken.CreateLineBreakToken());
                return codePanelRowData;
            }

            if(reviewLine.DiffKind == DiffKind.Added || reviewLine.DiffKind == DiffKind.Removed)
            {
                rowClasses.Add(reviewLine.DiffKind.ToString().ToLower());
                if (codePanelRowData.IsHiddenAPI) {
                    codePanelData.HasHiddenAPIThatIsDiff = true;
                }
            }

            bool spaceAdded = false;
            // Convert ReviewToken to UI required StructuredToken
            foreach (var token in reviewLine.Tokens)
            {
                if (token.HasPrefixSpace == true && !spaceAdded)
                {
                    var spaceToken = StructuredToken.CreateSpaceToken();
                    spaceToken.Value = " ";
                    tokensInRow.Add(spaceToken);
                }
                var structuredToken = new StructuredToken(token);
                tokensInRow.Add(structuredToken);

                if (token.IsDocumentation == true)
                {
                    rowClasses.Add(StructuredToken.DOCUMENTATION);
                    codePanelRowData.Type = CodePanelRowDatatype.Documentation;
                    codePanelRowData.ToggleCommentsClasses = "bi bi-chat-right-text hide";
                }

                if (token.HasSuffixSpace == true)
                {
                    var spaceToken = StructuredToken.CreateSpaceToken();
                    spaceToken.Value = " ";
                    tokensInRow.Add(spaceToken);
                    spaceAdded = true;
                }
                else
                {
                    spaceAdded = false;
                }
            }
            return codePanelRowData;
        }


        private static CodePanelRowData CollectUserCommentsForRow(CodePanelRawData codePanelRawData, string nodeId, string nodeIdHashed, CodePanelRowData codePanelRowData)
        {
            var commentRowData = new CodePanelRowData();
            if (!string.IsNullOrEmpty(nodeId) && !codePanelRowData.RowClassesObj.Contains("removed"))
            {
                codePanelRowData.ToggleCommentsClasses = "bi bi-chat-right-text can-show";
                var commentsForRow = codePanelRawData.Comments.Where(c => nodeId == c.ElementId);
                if (commentsForRow.Any())
                {
                    commentRowData.Type = CodePanelRowDatatype.CommentThread;
                    commentRowData.NodeIdHashed = nodeIdHashed;
                    commentRowData.NodeId = nodeId;
                    commentRowData.RowClassesObj.Add("user-comment-thread");
                    commentRowData.CommentsObj = commentsForRow.ToList();
                    codePanelRowData.ToggleCommentsClasses = codePanelRowData.ToggleCommentsClasses.Replace("can-show", "show");
                    commentRowData.IsResolvedCommentThread = commentsForRow.Any(c => c.IsResolved);
                    commentRowData.IsHiddenAPI = codePanelRowData.IsHiddenAPI;
                }
            }
            else
            {
                codePanelRowData.ToggleCommentsClasses = "bi bi-chat-right-text hide";
            }
            return commentRowData;
        }

        private static void InsertCodePanelRowData(CodePanelData codePanelData, CodePanelRowData rowData, string nodeIdHashed, CodePanelRowData commentsForRow = null)
        {
            if (!codePanelData.NodeMetaDataObj.ContainsKey(nodeIdHashed))
                codePanelData.NodeMetaDataObj[nodeIdHashed] = new CodePanelNodeMetaData();

            if (rowData.Type == CodePanelRowDatatype.Documentation)
            {
                rowData.RowPositionInGroup = codePanelData.NodeMetaDataObj[nodeIdHashed].DocumentationObj.Count();
                codePanelData.NodeMetaDataObj[nodeIdHashed].DocumentationObj.Add(rowData);
            }

            if (rowData.Type == CodePanelRowDatatype.CodeLine)
            {
                rowData.RowPositionInGroup = codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Count();
                codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Add(rowData);
                if (rowData.DiffKind == DiffKind.Added || rowData.DiffKind == DiffKind.Removed)
                {
                    codePanelData.NodeMetaDataObj[nodeIdHashed].IsNodeWithDiff = true;
                    var parentId = codePanelData.NodeMetaDataObj[nodeIdHashed].ParentNodeIdHashed;
                    while (parentId != null && !parentId.Equals("root") && codePanelData.NodeMetaDataObj.ContainsKey(parentId)
                        && !codePanelData.NodeMetaDataObj[parentId].IsNodeWithDiffInDescendants)
                    {
                        codePanelData.NodeMetaDataObj[parentId].IsNodeWithDiffInDescendants = true;
                        codePanelData.NodeMetaDataObj[parentId].IsNodeWithNoneDocDiffInDescendants = true;
                        parentId = codePanelData.NodeMetaDataObj[parentId].ParentNodeIdHashed;
                    }
                }
            }

            if (commentsForRow != null && commentsForRow.Type == CodePanelRowDatatype.CommentThread && commentsForRow.CommentsObj.Any())
            {
                commentsForRow.AssociatedRowPositionInGroup = rowData.RowPositionInGroup;
                codePanelData.NodeMetaDataObj[nodeIdHashed].CommentThreadObj.TryAdd(codePanelData.NodeMetaDataObj[nodeIdHashed].CodeLinesObj.Count() - 1, commentsForRow); //Map comment to the index of associated codeLine
            }
        }

        private static void AddDiagnosticRow(CodePanelData codePanelData, CodeFile codeFile, string nodeId, string nodeIdHashed)
        {
            if (codeFile.Diagnostics == null || codeFile.Diagnostics.Length == 0)
                return;

            var diagnostics = codeFile.Diagnostics.Where(d => d.TargetId == nodeId);
            foreach (var diagnostic in diagnostics)
            {
                var rowData = new CodePanelRowData()
                {
                    Type = CodePanelRowDatatype.Diagnostics,
                    NodeIdHashed = nodeIdHashed,
                    NodeId = nodeId,
                    Diagnostics = diagnostic,
                    RowClassesObj = new HashSet<string>() { "diagnostics", diagnostic.Level.ToString().ToLower() }
                };
                codePanelData.NodeMetaDataObj[nodeIdHashed].DiagnosticsObj.Add(rowData);
            }
        }

        public static bool AreCodeFilesSame(CodeFile codeFileA, CodeFile codeFileB)
        {
            return AreReviewLinesSame(codeFileA.ReviewLines, codeFileB.ReviewLines);
        }


        private static bool AreReviewLinesSame(List<ReviewLine> reviewLinesA, List<ReviewLine> reviewLinesB)
        {
            var filteredLinesA = reviewLinesA.Where(x => x.Tokens.Count > 0 && !x.IsDocumentation).ToList();
            var filteredLinesB = reviewLinesB.Where(x => x.Tokens.Count > 0 && !x.IsDocumentation).ToList();

            if (filteredLinesA.Count() != filteredLinesB.Count())
                return false;

            //Verify if child lines matches
            for (int i = 0; i < filteredLinesA.Count(); i++)
            {
                if (!filteredLinesA[i].Equals(filteredLinesB[i]) || !AreReviewLinesSame(filteredLinesA[i].Children, filteredLinesB[i].Children))
                    return false;
            }
            return true;
        }

        public static List<ReviewLine> FindDiff(List<ReviewLine> activeLines, List<ReviewLine> diffLines)
        {
            List<ReviewLine> resultLines = [];
            Dictionary<string, int> refCountMap = [];

            //Set lines from diff revision as not from active revision
            foreach (var line in diffLines)
            {
                line.IsActiveRevisionLine = false;
            }

            var intersectLines = diffLines.Intersect(activeLines);
            var interleavedLines = diffLines.InterleavedUnion(activeLines);

            foreach (var line in interleavedLines)
            {
                if (line.IsDocumentation || line.Processed)
                    continue;


                // Current node is not in both revisions. Mark current node as added or removed and then go to next sibling.
                // If a node is diff then no need to check it's children as they will be marked as diff as well.
                if (!intersectLines.Contains(line))
                {
                    //Recursively mark line as added or removed
                    MarkTreeNodeAsModified(line, line.IsActiveRevisionLine ? DiffKind.Added : DiffKind.Removed);

                    //Check if diff revision has a line at same level with same Line Id. This is to handle where a API was removed and added back in different order.
                    // This will also ensure added and modified lines are visible next to each other in the review.
                    var relatedLine = line.IsActiveRevisionLine ? diffLines.FirstOrDefault(l => !string.IsNullOrEmpty(l.LineId) && l.LineId == line.LineId) :
                        activeLines.FirstOrDefault(l => !string.IsNullOrEmpty(l.LineId) && l.LineId == line.LineId);
                    if (relatedLine != null)
                    {
                        relatedLine.Processed = true;
                        MarkTreeNodeAsModified(relatedLine, relatedLine.IsActiveRevisionLine ? DiffKind.Added : DiffKind.Removed);
                        //Identify the tokens within modified lines and highlight them in the UI
                        FindModifiedTokens(line, relatedLine);
                    }

                    if (relatedLine != null)
                    {
                        // First add removed line and then added line
                        resultLines.Add(line.IsActiveRevisionLine ? relatedLine : line);
                        resultLines.Add(line.IsActiveRevisionLine ? line : relatedLine);
                    }
                    else
                    {
                        resultLines.Add(line);
                    }
                    continue;
                }

                var activeLine = activeLines.FirstOrDefault(l => l.LineId == line.LineId && l.Processed == false && l.Equals(line));
                if (activeLine == null)
                    activeLine = line;
                //current node is present in both trees. Compare child nodes recursively
                var diffLine = diffLines.FirstOrDefault(l => l.LineId == line.LineId && l.Processed == false && l.Equals(line));
                var diffLineChildren = diffLine != null ? diffLine.Children : new List<ReviewLine>();
                var resultantSubTree = FindDiff(activeLine.Children, diffLineChildren);
                //Update new resulting subtree as children of current node
                activeLine.Children.Clear();
                activeLine.Children.AddRange(resultantSubTree);
                resultLines.Add(activeLine);
                activeLine.Processed = true;
                if (diffLine != null)
                    diffLine.Processed = true;
            }
            return resultLines;
        }

        private static void MarkTreeNodeAsModified(ReviewLine line, DiffKind diffKind)
        {
            line.DiffKind = diffKind;
            foreach (var child in line.Children)
            {
                if (!child.IsDocumentation)
                    MarkTreeNodeAsModified(child, diffKind);
            }
        }
        /***
         * This method collects all documentation lines from the review line and generate a CodePanelRow object for each documentation line.
         * These documentation rows will be stored in a dictionary so it can be mapped and connected tp code line when processing code lines.
         * */
        private static void CollectDocumentationLines(CodePanelData codePanelData, List<ReviewLine> reviewLines, Dictionary<string,List<CodePanelRowData>> documentationRowMap, int indent, string parentNodeIdHash, bool enableSkipDiff = false)
        {
            if (reviewLines?.Count == 0)
                return;

            List<CodePanelRowData> docRows = [];
            // Collect all documentation line and then add it as related documentation line for the first code line after documentation so it will be correctly liked on review page.
            int idx = 0;
            foreach (var line in reviewLines)
            {
                //Find if current line has at least one token that's not marked as skip from diff check
                bool hasNonSkippedTokens = line.Tokens.Any(t => t.SkipDiff != true);
                if (line.IsDocumentation && (!enableSkipDiff || hasNonSkippedTokens))
                {
                    docRows.Add(GetCodePanelRowData(codePanelData, line, parentNodeIdHash, indent));
                    continue;
                }

                //Create hash id for code line and set node ID and hash Id for documentation rows
                var nodeIdHashed = line.GetTokenNodeIdHash(parentNodeIdHash, idx);
                docRows.ForEach(d =>
                {
                    d.NodeIdHashed = nodeIdHashed;
                    d.NodeId = line.LineId;
                });
                documentationRowMap[nodeIdHashed] = docRows;
                docRows = [];

                idx++;
                // Recursively process child node lines
                if (line.Children.Count > 0)
                    CollectDocumentationLines(codePanelData, line.Children, documentationRowMap, indent + 1, nodeIdHashed, enableSkipDiff);
            }
        }


        // Documentation rows are collected from active revision using node hash ID generated for corresponding code line.
        // Hash ID uses diff kind type to generate the hashed node ID and it will be different after diff calculation for same code line token
        // We need to remap the collected documentation to the new hashed node ID so that documentation is correctly listed on review.
        private static void RemapDocumentationLines(List<ReviewLine> activeLines, Dictionary<string, List<CodePanelRowData>> documentationRowMap, string oldParentID = "root", string newParentId = "root")
        {
            int activeIdx = 0, idx = 0, diffIdx = 0;
            foreach (var line in activeLines)
            {
                // Remapping of node hash ID is required only for code line
                if (line.IsDocumentation)
                    continue;

                var diffKind = line.DiffKind;
                line.DiffKind = DiffKind.NoneDiff;
                var oldHashId = line.GetTokenNodeIdHash(oldParentID, activeIdx);
                line.DiffKind = diffKind;
                var newHashId = line.GetTokenNodeIdHash(newParentId, idx);

                if (documentationRowMap.ContainsKey(oldHashId) && !newHashId.Equals(oldHashId))
                {
                    documentationRowMap[newHashId] = documentationRowMap[oldHashId];
                    documentationRowMap.Remove(oldHashId);
                }

                if (line.Children.Count > 0)
                {
                    RemapDocumentationLines(line.Children, documentationRowMap, oldHashId, newHashId);
                }
                idx++;

                // Move previous review line index before diff calculation based on diff type so we can calculate old HashId correctly.
                if (line.DiffKind == DiffKind.NoneDiff)
                {
                    activeIdx++;
                    diffIdx++;
                }
                else if (line.DiffKind == DiffKind.Added)
                {
                    activeIdx++;
                }
                else
                {
                    diffIdx++;
                }
            }
        }

        private static void FindModifiedTokens(ReviewLine lineA, ReviewLine lineB)
        {
            var lineATokenMap = new Dictionary<string, ReviewToken>();
            foreach (var token in lineA.Tokens)
            {
                lineATokenMap[token.Value] = token;
            }
            var lineBTokenMap = new Dictionary<string, ReviewToken>();
            foreach (var token in lineB.Tokens)
            {
                lineBTokenMap[token.Value] = token;
            }

            foreach (var key in lineBTokenMap.Keys.Except(lineATokenMap.Keys))
            {
                lineBTokenMap[key].RenderClasses.Add("diff-change");
            }
            foreach (var key in lineATokenMap.Keys.Except(lineBTokenMap.Keys))
            {
                lineATokenMap[key].RenderClasses.Add("diff-change");
            }
        }

        private static void CreateNavigationTree(CodePanelData codePanelData, CodeFile codeFile)
        {
            if (codeFile.Navigation != null && codeFile.Navigation.Count() > 0)
            {
                //Use navigation tree generated by the parser
                foreach (var navigation in codeFile.Navigation)
                {
                    codePanelData.NavigationTreeNodesObj.Add(ProcessNavigationNodeFromOldModel(codePanelData.LineIdToNodeIdHashed, navigation));
                }
            }
        }

        private static NavigationTreeNode ProcessNavigationNodeFromOldModel(Dictionary<string, string> nodeIdMap, NavigationItem navItem)
        {
            NavigationTreeNode node = new NavigationTreeNode()
            {
                Label = navItem.Text,
                Expanded = true,
                Data = new NavigationTreeNodeData()
            };

            if(navItem.Tags != null)
            {
                node.Data.Kind = navItem.Tags["TypeKind"];
                node.Data.Icon = node.Data.Kind;
            }

            if (nodeIdMap.ContainsKey(navItem.NavigationId))
                node.Data.NodeIdHashed = nodeIdMap[navItem.NavigationId];

            foreach (var childItem in navItem.ChildItems)
            {
                node.ChildrenObj.Add(ProcessNavigationNodeFromOldModel(nodeIdMap, childItem));
            }
            return node;
        }
    }
}
