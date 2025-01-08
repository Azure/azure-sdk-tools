using APIView.TreeToken;
using APIView;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ApiView;
using System.Text;
using APIView.Model.V2;

namespace APIViewWeb.LeanModels
{
    public class CodePanelRawData
    {
        public IEnumerable<CommentItemModel> Comments { get; set; } = new List<CommentItemModel>();
        public CodeFile activeRevisionCodeFile{ get; set; }
        public CodeFile diffRevisionCodeFile { get; set; }
        public bool ApplySkipDiff { get; set; }
        public bool SkipDocsWhenDiffing { get; set; }
    }

    public class CodePanelRowData
    {
        public CodePanelRowDatatype Type { get; set; }
        public int? LineNumber { get; set; }
        [JsonIgnore]
        public List<StructuredToken> RowOfTokensObj { get; set; } = new List<StructuredToken>();
        public List<StructuredToken> RowOfTokens => RowOfTokensObj.Count > 0 ? RowOfTokensObj : null;
        public string NodeId { get; set; }
        public string NodeIdHashed { get; set; }
        public int RowPositionInGroup { get; set; } // The position / index of the row within the group of similar rows
        public int AssociatedRowPositionInGroup { get; set; } // For comment threads, this is the position of the associated code line within the group of similar rows
        [JsonIgnore]
        public HashSet<string> RowClassesObj { get; set; } = new HashSet<string>();
        public HashSet<string> RowClasses => RowClassesObj.Count > 0 ? RowClassesObj : null;
        public int? Indent { get; set; }
        public DiffKind DiffKind { get; set; }
        public string ToggleDocumentationClasses { get; set; }
        public string ToggleCommentsClasses { get; set; }
        public CodeDiagnostic Diagnostics { get; set; }
        [JsonIgnore]
        public List<CommentItemModel> CommentsObj { get; set; } = new List<CommentItemModel>();
        public List<CommentItemModel> Comments => CommentsObj.Count > 0 ? CommentsObj : null;
        public bool IsResolvedCommentThread { get; set; }
        public bool IsHiddenAPI { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(var token in RowOfTokensObj)
            {
                sb.Append(token.Value);
            }
            return sb.ToString();
        }
        public override bool Equals(object obj)
        {
            if (obj is CodePanelRowData other)
            {
                return ToString() == other.ToString();
            }
            return false;
        }
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }

    public class CodePanelNodeMetaData
    {
        [JsonIgnore]
        public List<CodePanelRowData> DocumentationObj { get; set; } = new List<CodePanelRowData>();
        public List<CodePanelRowData> Documentation => DocumentationObj.Count > 0 ? DocumentationObj : null;
        [JsonIgnore]
        public List<CodePanelRowData> DiagnosticsObj { get; set; } = new List<CodePanelRowData>();
        public List<CodePanelRowData> Diagnostics => DiagnosticsObj.Count > 0 ? DiagnosticsObj : null;
        [JsonIgnore]
        public List<CodePanelRowData> CodeLinesObj { get; set; } = new List<CodePanelRowData>();
        public List<CodePanelRowData> CodeLines => CodeLinesObj.Count > 0 ? CodeLinesObj : null;
        [JsonIgnore]
        public Dictionary<int, CodePanelRowData> CommentThreadObj { get; set; } = []; //Dictionary key map to the index of the code line within this node which the comment thread is mapped to
        public Dictionary<int, CodePanelRowData> CommentThread => CommentThreadObj.Count > 0 ? CommentThreadObj : null;
        public NavigationTreeNode NavigationTreeNode { get; set; }
        public string ParentNodeIdHashed { get; set; }
        [JsonIgnore]
        public Dictionary<int, string> ChildrenNodeIdsInOrderObj { get; set; } = [];
        public Dictionary<int, string> ChildrenNodeIdsInOrder => ChildrenNodeIdsInOrderObj.Count > 0 ? ChildrenNodeIdsInOrderObj : null;
        public bool IsNodeWithDiff { get; set; }
        public bool IsNodeWithDiffInDescendants { get; set; }
        public bool IsNodeWithNoneDocDiffInDescendants { get; set; }
        public string BottomTokenNodeIdHash { get; set; }
        public string RelatedNodeIdHash { get; set; }
    }

    public class CodePanelData
    {
        [JsonIgnore]
        public Dictionary<string, CodePanelNodeMetaData> NodeMetaDataObj { get; set; } = new Dictionary<string, CodePanelNodeMetaData>();
        public Dictionary<string, CodePanelNodeMetaData> NodeMetaData => NodeMetaDataObj.Count > 0 ? NodeMetaDataObj : null;
        public bool HasDiff { get; set; } = false;
        public bool HasHiddenAPIThatIsDiff { get; set; } = false;
        [JsonIgnore]
        public Dictionary<string, string> LineIdToNodeIdHashed { get; set; } = new Dictionary<string, string>();
        [JsonIgnore]
        public Dictionary<string, List<CodePanelRowData>> ActiveDocumentationMap { get; set; } = new Dictionary<string, List<CodePanelRowData>>();
        [JsonIgnore]
        public Dictionary<string, List<CodePanelRowData>> DiffDocumentationMap { get; set; } = new Dictionary<string, List<CodePanelRowData>>();
        [JsonIgnore]
        public List<NavigationTreeNode> NavigationTreeNodesObj { get; set; } = [];
        public NavigationTreeNode[] NavigationTreeNodes => NavigationTreeNodesObj != null ? NavigationTreeNodesObj.ToArray() : null;

        public void AddLineIdNodeHashMapping(string lineId, string nodeId)
        {
            if (!string.IsNullOrEmpty(lineId) && !string.IsNullOrEmpty(nodeId))
            {
                LineIdToNodeIdHashed[lineId] = nodeId;
            }
        }

        public string GetNodeIdHashFromLineId(string lineId)
        {
            if (!string.IsNullOrEmpty(lineId) && LineIdToNodeIdHashed.ContainsKey(lineId))
            {
                return LineIdToNodeIdHashed[lineId];
            }
            return string.Empty;
        }

        public void SetLineAsRelated(string nodeIdHashed, string relatedLine)
        {
            var relatedNodeHashId = GetNodeIdHashFromLineId(relatedLine);
            if (!string.IsNullOrEmpty(relatedNodeHashId) && NodeMetaDataObj.ContainsKey(nodeIdHashed))
            {
                var node = NodeMetaDataObj[nodeIdHashed];
                node.RelatedNodeIdHash = relatedNodeHashId;
                if (node.IsNodeWithDiff)
                {
                    NodeMetaDataObj[relatedNodeHashId].IsNodeWithDiffInDescendants = true;
                }
            }
        }

        public void AddNavigation(string nodeIdHash, NavigationTreeNode node)
        {
            if (!string.IsNullOrEmpty(nodeIdHash) && node != null)
            {
                NodeMetaDataObj[nodeIdHash].NavigationTreeNode = node;
            }
        }

        public void ConnectNodeToParent(string nodeIdHashed, string parentNodeIdHashed, int nodePosition)
        {
            if (!NodeMetaDataObj.ContainsKey(nodeIdHashed))
            {
                NodeMetaDataObj.TryAdd(nodeIdHashed, new CodePanelNodeMetaData());
            }
            NodeMetaDataObj[nodeIdHashed].ParentNodeIdHashed = parentNodeIdHashed;
            NodeMetaDataObj[parentNodeIdHashed].ChildrenNodeIdsInOrderObj.TryAdd(nodePosition, nodeIdHashed);
        }
    }
}
