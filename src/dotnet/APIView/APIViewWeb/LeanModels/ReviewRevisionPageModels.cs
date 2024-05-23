using System.Collections.Generic;
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

    public enum LinesOfTokensPosition
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

    public class ReviewCodePanelData
    {
        public IEnumerable<CommentItemModel> Comments { get; set; }
        public List<APITreeNodeForAPI> APIForest { get; set; } = new List<APITreeNodeForAPI>();
        public CodeDiagnostic[] Diagnostics { get; set; }
    }

    public class CodePanelRowData
    {
        public CodePanelRowDatatype RowType { get; set; }
        public int? LineNumber { get; set; }
        public List<StructuredToken> RowsOfTokens { get; set; } = new List<StructuredToken>();
        public string NodeId { get; set; }
        public string NodeIdHashed { get; set; }
        public LinesOfTokensPosition linesOfTokensPosition { get; set; }
        public HashSet<string> RowClasses { get; set; } = new HashSet<string>();
        public int? Indent { get; set; }
        public DiffNodeKind DiffKind { get; set; }
        public int RowSize { get; set; }
        public string ToggleDocumentationClasses { get; set; }
        public string ToggleCommentsClasses { get; set; }
        public CodeDiagnostic Diagnostics { get; set; }
        public List<CommentItemModel> Comments { get; set; } = new List<CommentItemModel>();
    }

    public class CodePanelNodeMetaData
    {
        public List<CodePanelRowData> Documentation { get; set; } = new List<CodePanelRowData>();
        public List<CodePanelRowData> Diagnostics { get; set; } = new List<CodePanelRowData>();
        public List<CommentItemModel> Comments { get; set; } = new List<CommentItemModel>();
        public string ParentNodeId { get; set; }
        public string ChildrenNodeIds { get; set; }
        public bool IsDiffNode { get; set; }
        public bool IsDiffInDescendants { get; set; }
        public bool IsClosingNode { get; set; }
    }

    public class CodePanelData
    {
        public List<CodePanelRowData> Rows { get; set; } = new List<CodePanelRowData>();
        public Dictionary<string, CodePanelNodeMetaData> NodeMetaData { get; set; } = new Dictionary<string, CodePanelNodeMetaData>();
    }
}
