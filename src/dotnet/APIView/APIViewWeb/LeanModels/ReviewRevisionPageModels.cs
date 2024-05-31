using System.Collections.Concurrent;
using System.Collections.Generic;
using ApiView;
using APIView;
using APIView.Model;
using APIViewWeb.Models;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.SwaggerGen;

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

    [JsonObject("cprd")]
    public class CodePanelRowData
    {
        private List<StructuredToken> _rowOfTokens;
        private HashSet<string> _rowClasses;
        private List<CommentItemModel> _comments;

        [JsonProperty("t")]
        public CodePanelRowDatatype Type { get; set; }
        [JsonProperty("ln")]
        public int? LineNumber { get; set; }
        [JsonProperty("rot")]
        public List<StructuredToken> RowOfTokens
        {
            get
            {
                if (_rowOfTokens == null)
                {
                    _rowOfTokens = new List<StructuredToken>();
                }
                return _rowOfTokens;
            }
            set
            {
                _rowOfTokens = value;
            }
        }
        [JsonProperty("ni")]
        public string NodeId { get; set; }
        [JsonProperty("nih")]
        public string NodeIdHashed { get; set; }
        [JsonProperty("rotp")]
        public RowOfTokensPosition RowOfTokensPosition { get; set; }
        [JsonProperty("rc")]
        public HashSet<string> RowClasses
        {
            get
            {
                if (_rowClasses == null)
                {
                    _rowClasses = new HashSet<string>();
                }
                return _rowClasses;
            }
            set
            {
                _rowClasses = value;
            }
        }
        [JsonProperty("i")]
        public int? Indent { get; set; }
        [JsonProperty("dk")]
        public DiffKind DiffKind { get; set; }
        [JsonProperty("rs")]
        public int RowSize { get; set; }
        [JsonProperty("tdc")]
        public string ToggleDocumentationClasses { get; set; }
        [JsonProperty("tcc")]
        public string ToggleCommentsClasses { get; set; }
        [JsonProperty("d")]
        public CodeDiagnostic Diagnostics { get; set; }
        [JsonProperty("c")]
        public List<CommentItemModel> Comments
        {
            get
            {
                if (_comments == null)
                {
                    _comments = new List<CommentItemModel>();
                }
                return _comments;
            }
            set
            {
                _comments = value;
            }
        }
    }

    [JsonObject("cprmd")]
    public class CodePanelNodeMetaData
    {
        private List<CodePanelRowData> _documentation;
        private List<CodePanelRowData> _diagnostics;
        private List<CodePanelRowData> _codeLines;
        private List<CodePanelRowData> _commentThread;
        private ConcurrentDictionary<int, string> _childrenNodeIdsInOrder;

        [JsonProperty("doc")]
        public List<CodePanelRowData> Documentation
        {
            get
            {
                if (_documentation == null)
                {
                    _documentation = new List<CodePanelRowData>();
                }
                return _documentation;
            }
            set
            {
                _documentation = value;
            }
        }
        [JsonProperty("d")]
        public List<CodePanelRowData> Diagnostics
        {
            get
            {
                if (_diagnostics == null)
                {
                    _diagnostics = new List<CodePanelRowData>();
                }
                return _diagnostics;
            }
            set
            {
                _diagnostics = value;
            }
        }
        [JsonProperty("cl")]
        public List<CodePanelRowData> CodeLines
        {
            get
            {
                if (_codeLines == null)
                {
                    _codeLines = new List<CodePanelRowData>();
                }
                return _codeLines;
            }
            set
            {
                _codeLines = value;
            }
        }
        [JsonProperty("ct")]
        public List<CodePanelRowData> CommentThread
        {
            get
            {
                if (_commentThread == null)
                {
                    _commentThread = new List<CodePanelRowData>();
                }
                return _commentThread;
            }
            set
            {
                _commentThread = value;
            }
        }
        [JsonProperty("ntn")]
        public NavigationTreeNode NavigationTreeNode { get; set; }
        [JsonProperty("pnih")]
        public string ParentNodeIdHashed { get; set; }
        [JsonProperty("cniio")]
        public ConcurrentDictionary<int, string> ChildrenNodeIdsInOrder
        {
            get
            {
                if (_childrenNodeIdsInOrder == null)
                {
                    _childrenNodeIdsInOrder = new ConcurrentDictionary<int, string>();
                }
                return _childrenNodeIdsInOrder;
            }
            set
            {
                _childrenNodeIdsInOrder = value;
            }
        }
        [JsonProperty("inwd")]
        public bool IsNodeWithDiff { get; set; }
        [JsonProperty("inwdid")]
        public bool IsNodeWithDiffInDescendants { get; set; }
        [JsonProperty("btnih")]
        public string BottomTokenNodeIdHash { get; set; }
    }

    [JsonObject("cpd")]
    public class CodePanelData
    {
        [JsonProperty("nmd")]
        public ConcurrentDictionary<string, CodePanelNodeMetaData> NodeMetaData { get; set; } = new ConcurrentDictionary<string, CodePanelNodeMetaData>();
    }

    [JsonObject("ntnd")]
    public class NavigationTreeNodeData
    {
        [JsonProperty("k")]
        public string Kind { get; set; }
        [JsonProperty("i")]
        public string Icon { get; set; }
    }
    [JsonObject("ntn")]

    public class NavigationTreeNode
    {
        private List<NavigationTreeNode> _children;
        [JsonProperty("l")]
        public string Label { get; set; }
        public NavigationTreeNodeData Data { get; set; }
        [JsonProperty("d")]
        public bool Expanded { get; set; }
        [JsonProperty("c")]
        public List<NavigationTreeNode> Children
        {
            get
            {
                if (_children == null)
                {
                    _children = new List<NavigationTreeNode>();
                }
                return _children;
            }
            set
            {
                _children = value;
            }
        }
    }

    public class DiffLineInProcess
    {
        public string GroupId { get; set; }
        public List<StructuredToken> RowOfTokens { get; set; } = new List<StructuredToken>();
        public HashSet<string> TokenIdsInRow { get; set; } = new HashSet<string>();
    }
}
