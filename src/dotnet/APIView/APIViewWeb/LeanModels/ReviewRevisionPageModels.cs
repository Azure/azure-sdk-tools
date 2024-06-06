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
        [JsonPropertyName("t")]
        public CodePanelRowDatatype Type { get; set; }
        [JsonPropertyName("ln")]
        public int? LineNumber { get; set; }
        [JsonIgnore]
        public List<StructuredToken> RowOfTokens { get; set; } = new List<StructuredToken>();
        [JsonPropertyName("rot")]
        public List<StructuredToken> RowOfTokensSerialized => RowOfTokens.Count > 0 ? RowOfTokens : null;
        [JsonPropertyName("ni")]

        public string NodeId { get; set; }
        [JsonPropertyName("nih")]

        public string NodeIdHashed { get; set; }
        [JsonPropertyName("rotp")]

        public RowOfTokensPosition RowOfTokensPosition { get; set; }
        [JsonIgnore]
        public HashSet<string> RowClasses { get; set; } = new HashSet<string>();
        [JsonPropertyName("rc")]
        public HashSet<string> RowClassesSerialized => RowClasses.Count > 0 ? RowClasses : null;
        [JsonPropertyName("i")]

        public int? Indent { get; set; }
        [JsonPropertyName("dk")]

        public DiffKind DiffKind { get; set; }
        [JsonPropertyName("rs")]

        public int RowSize { get; set; }
        [JsonPropertyName("tdc")]

        public string ToggleDocumentationClasses { get; set; }
        [JsonPropertyName("tcc")]

        public string ToggleCommentsClasses { get; set; }
        [JsonPropertyName("d")]

        public CodeDiagnostic Diagnostics { get; set; }
        [JsonIgnore]
        public List<CommentItemModel> Comments { get; set; } = new List<CommentItemModel>();
        [JsonPropertyName("c")]
        public List<CommentItemModel> CommentsSerialized => Comments.Count > 0 ? Comments : null;

    }

    public class CodePanelNodeMetaData
    {
        [JsonIgnore]
        public List<CodePanelRowData> Documentation { get; set; } = new List<CodePanelRowData>();
        [JsonPropertyName("doc")]
        public List<CodePanelRowData> DocumentationSerialized => Documentation.Count > 0 ? Documentation : null;
        [JsonIgnore]
        public List<CodePanelRowData> Diagnostics { get; set; } = new List<CodePanelRowData>();
        [JsonPropertyName("d")]
        public List<CodePanelRowData> DiagnosticsSerialized => Diagnostics.Count > 0 ? Diagnostics : null;
        [JsonIgnore]
        public List<CodePanelRowData> CodeLines { get; set; } = new List<CodePanelRowData>();
        [JsonPropertyName("cl")]
        public List<CodePanelRowData> CodeLinesSerialized => CodeLines.Count > 0 ? CodeLines : null;
        [JsonIgnore]
        public List<CodePanelRowData> CommentThread { get; set; } = new List<CodePanelRowData>();
        [JsonPropertyName("ct")]
        public List<CodePanelRowData> CommentThreadSerialized => CommentThread.Count > 0 ? CommentThread : null;
        [JsonPropertyName("ntn")]
        public NavigationTreeNode NavigationTreeNode { get; set; }
        [JsonPropertyName("pnih")]
        public string ParentNodeIdHashed { get; set; }
        [JsonIgnore]
        public ConcurrentDictionary<int, string> ChildrenNodeIdsInOrder { get; set; } = new ConcurrentDictionary<int, string>();
        [JsonPropertyName("cniio")]
        public ConcurrentDictionary<int, string> ChildrenNodeIdsInOrderSerialized => ChildrenNodeIdsInOrder.Count > 0 ? ChildrenNodeIdsInOrder : null;
        [JsonPropertyName("inwd")]
        public bool IsNodeWithDiff { get; set; }
        [JsonPropertyName("inwdid")]
        public bool IsNodeWithDiffInDescendants { get; set; }
        [JsonPropertyName("btnih")]
        public string BottomTokenNodeIdHash { get; set; }
    }

    public class CodePanelData
    {
        [JsonIgnore]
        public ConcurrentDictionary<string, CodePanelNodeMetaData> NodeMetaData { get; set; } = new ConcurrentDictionary<string, CodePanelNodeMetaData>();
        [JsonPropertyName("nodeMetaData")]
        public ConcurrentDictionary<string, CodePanelNodeMetaData> NodeMetaDataSerialized => NodeMetaData.Count > 0 ? NodeMetaData : null;
    }

    public class NavigationTreeNodeData
    {
        [JsonPropertyName("k")]
        public string Kind { get; set; }
        [JsonPropertyName("i")]
        public string Icon { get; set; }
        [JsonPropertyName("nih")]
        public string NodeIdHashed { get; set; }
    }

    public class NavigationTreeNode
    {
        [JsonPropertyName("l")]
        public string Label { get; set; }
        [JsonPropertyName("d")]
        public NavigationTreeNodeData Data { get; set; }
        [JsonPropertyName("e")]
        public bool Expanded { get; set; }
        [JsonIgnore]
        public List<NavigationTreeNode> Children { get; set; } = new List<NavigationTreeNode>();
        [JsonPropertyName("c")]
        public List<NavigationTreeNode> ChildrenSerialized => Children.Count > 0 ? Children : null;
    }

    public class DiffLineInProcess
    {
        public string GroupId { get; set; }
        public List<StructuredToken> RowOfTokens { get; set; } = new List<StructuredToken>();
        public HashSet<string> TokenIdsInRow { get; set; } = new HashSet<string>();
    }
}
