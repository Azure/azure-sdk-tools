using System.Collections.Concurrent;
using System.Collections.Generic;
using ApiView;
using APIView;
using APIView.Model;
using APIViewWeb.Models;
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

    public class CodePanelRowData
    {
        public CodePanelRowDatatype Type { get; set; }
        public int? LineNumber { get; set; }
        public List<StructuredToken> RowOfTokens { get; set; } = new List<StructuredToken>();
        public string NodeId { get; set; }
        public string NodeIdHashed { get; set; }
        public RowOfTokensPosition RowOfTokensPosition { get; set; }
        public HashSet<string> RowClasses { get; set; } = new HashSet<string>();
        public int? Indent { get; set; }
        public DiffKind DiffKind { get; set; }
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
        public List<CodePanelRowData> CodeLines { get; set; } = new List<CodePanelRowData>();
        public List<CodePanelRowData> CommentThread { get; set; } = new List<CodePanelRowData>();
        public NavigationTreeNode NavigationTreeNode { get; set; }
        public string ParentNodeIdHashed { get; set; }
        public ConcurrentDictionary<int, string> ChildrenNodeIdsInOrder { get; set; } = new ConcurrentDictionary<int, string>();
        public bool IsNodeWithDiff { get; set; }
        public bool IsNodeWithDiffInDescendants { get; set; }
        public string BottomTokenNodeIdHash { get; set; }
    }

    public class CodePanelData
    {
        public ConcurrentDictionary<string, CodePanelNodeMetaData> NodeMetaData { get; set; } = new ConcurrentDictionary<string, CodePanelNodeMetaData>();
    }

    public class NavigationTreeNodeData
    {
        public string Kind { get; set; }
        public string Icon { get; set; }
    }

    public class NavigationTreeNode
    {
        public string Label { get; set; }
        public NavigationTreeNodeData Data { get; set; }
        public bool Expanded { get; set; }
        public List<NavigationTreeNode> Children { get; set; } = new List<NavigationTreeNode>();
    }

    public class DiffLineInProcess
    {
        public string GroupId { get; set; }
        public List<StructuredToken> RowOfTokens { get; set; } = new List<StructuredToken>();
        public HashSet<string> TokenIdsInRow { get; set; } = new HashSet<string>();
    }
}
