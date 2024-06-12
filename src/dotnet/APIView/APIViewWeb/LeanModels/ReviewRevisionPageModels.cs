using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using APIView;
using APIView.Model;
using APIViewWeb.Models;

namespace APIViewWeb.LeanModels
{
    public enum ReviewContentModelDirective
    {
        ProceedWithPageLoad = 0,
        TryGetlegacyReview,
        ErrorDueToInvalidAPIRevisonProceedWithPageLoad,
        ErrorDueToInvalidAPIRevisonRedirectToIndexPage,
        RedirectToSPAUI
    }

    public enum CodePanelRowDatatype
    {
        CodeLine,
        Documentation,
        Diagnostics,
        CommentThread
    }

    public enum RowOfTokensPosition
    {
        Top,
        Bottom
    }

    public class ReviewContentModel
    {
        public ReviewListItemModel Review { get; set; }
        public NavigationItem[] Navigation { get; set; }
        public CodeLineModel[] codeLines { get; set; }
        public IEnumerable<APIRevisionListItemModel> APIRevisions { get; set; }
        public APIRevisionListItemModel ActiveAPIRevision { get; set; }
        public APIRevisionListItemModel DiffAPIRevision { get; set; }
        public int TotalActiveConversations { get; set; }
        public int ActiveConversationsInActiveAPIRevision { get; set; }
        public int ActiveConversationsInSampleRevisions { get; set; }
        public HashSet<string> PreferredApprovers = new HashSet<string>();
        public HashSet<GithubUser> TaggableUsers { get; set; }
        public bool PageHasLoadableSections { get; set; }
        public string NotificationMessage { get; set; }
        public ReviewContentModelDirective Directive { get; set; }
        public Dictionary<string, ReviewContentModel> CrossLanguageViewContent { get; set; } = new Dictionary<string, ReviewContentModel>();
        public bool HasFatalDiagnostics { get; set; }
    }

    public class ReviewBadgeModel
    {
        public ReviewListItemModel Review { get; set; }
        public IEnumerable<APIRevisionListItemModel> APIRevisions { get; set; }
        public APIRevisionListItemModel ActiveAPIRevision { get; set; }
        public APIRevisionListItemModel DiffAPIRevision { get; set; }
        public UserPreferenceModel UserPreference { get; set; }
        public bool? ShowDocumentation { get; set; }
        public bool? ShowDiffOnly { get; set; }
    }

    public class CodePanelRawData
    {
        public IEnumerable<CommentItemModel> Comments { get; set; }
        public List<APITreeNode> APIForest { get; set; } = new List<APITreeNode>();
        public CodeDiagnostic[] Diagnostics { get; set; }
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
        public RowOfTokensPosition RowOfTokensPosition { get; set; }
        [JsonIgnore]
        public HashSet<string> RowClassesObj { get; set; } = new HashSet<string>();
        public HashSet<string> RowClasses => RowClassesObj.Count > 0 ? RowClassesObj : null;
        public int? Indent { get; set; }
        public DiffKind DiffKind { get; set; }
        public int RowSize { get; set; }
        public string ToggleDocumentationClasses { get; set; }
        public string ToggleCommentsClasses { get; set; }
        public CodeDiagnostic Diagnostics { get; set; }
        [JsonIgnore]
        public List<CommentItemModel> CommentsObj { get; set; } = new List<CommentItemModel>();
        public List<CommentItemModel> Comments => CommentsObj.Count > 0 ? CommentsObj : null;

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
        public List<CodePanelRowData> CommentThreadObj { get; set; } = new List<CodePanelRowData>();
        public List<CodePanelRowData> CommentThread => CommentThreadObj.Count > 0 ? CommentThreadObj : null;
        public NavigationTreeNode NavigationTreeNode { get; set; }
        public string ParentNodeIdHashed { get; set; }
        [JsonIgnore]
        public ConcurrentDictionary<int, string> ChildrenNodeIdsInOrderObj { get; set; } = new ConcurrentDictionary<int, string>();
        public ConcurrentDictionary<int, string> ChildrenNodeIdsInOrder => ChildrenNodeIdsInOrderObj.Count > 0 ? ChildrenNodeIdsInOrderObj : null;
        public bool IsNodeWithDiff { get; set; }
        public bool IsNodeWithDiffInDescendants { get; set; }
        public string BottomTokenNodeIdHash { get; set; }
    }

    public class CodePanelData
    {
        [JsonIgnore]
        public ConcurrentDictionary<string, CodePanelNodeMetaData> NodeMetaDataObj { get; set; } = new ConcurrentDictionary<string, CodePanelNodeMetaData>();
        public ConcurrentDictionary<string, CodePanelNodeMetaData> NodeMetaData => NodeMetaDataObj.Count > 0 ? NodeMetaDataObj : null;
    }

    public class NavigationTreeNodeData
    {
        public string Kind { get; set; }
        public string Icon { get; set; }
        public string NodeIdHashed { get; set; }
    }

    public class NavigationTreeNode
    {
        public string Label { get; set; }
        public NavigationTreeNodeData Data { get; set; }
        public bool Expanded { get; set; }
        [JsonIgnore]
        public List<NavigationTreeNode> ChildrenObj { get; set; } = new List<NavigationTreeNode>();
        public List<NavigationTreeNode> Children => ChildrenObj.Count > 0 ? ChildrenObj : null;
    }

    public class DiffLineInProcess
    {
        public string GroupId { get; set; }
        public List<StructuredToken> RowOfTokens { get; set; } = new List<StructuredToken>();
        public HashSet<string> TokenIdsInRow { get; set; } = new HashSet<string>();
    }
}
