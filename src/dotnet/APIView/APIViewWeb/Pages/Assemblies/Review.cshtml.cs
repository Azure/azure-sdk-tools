using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIView.DIff;
using APIView.Model;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;


namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewPageModel : PageModel
    {
        private static int REVIEW_DIFF_CONTEXT_SIZE = 3;
        private const string DIFF_CONTEXT_SEPERATOR = "<br><span>.....</span><br>";
        private readonly IReviewManager _manager;
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly ICommentsManager _commentsManager;
        private readonly INotificationManager _notificationManager;
        public readonly UserPreferenceCache _preferenceCache;
        private readonly ICosmosUserProfileRepository _userProfileRepository;
        private readonly IConfiguration _configuration;

        private readonly IHubContext<SignalRHub> _signalRHubContext;

        public ReviewPageModel(
            IReviewManager manager,
            IPullRequestManager pullRequestManager,
            IBlobCodeFileRepository codeFileRepository,
            ICommentsManager commentsManager,
            INotificationManager notificationManager,
            UserPreferenceCache preferenceCache,
            ICosmosUserProfileRepository userProfileRepository,
            IConfiguration configuration,
            IHubContext<SignalRHub> signalRHub)
        {
            _manager = manager;
            _pullRequestManager = pullRequestManager;
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
            _notificationManager = notificationManager;
            _preferenceCache = preferenceCache;
            _userProfileRepository = userProfileRepository;
            _configuration = configuration;
            _signalRHubContext = signalRHub;
        }

        public ReviewModel Review { get; set; }
        public ReviewRevisionModel Revision { get; set; }
        public ReviewRevisionModel DiffRevision { get; set; }
        public ReviewRevisionModel[] PreviousRevisions {get; set; }
        public CodeFile CodeFile { get; set; }
        public CodeLineModel[] Lines { get; set; }
        public InlineDiffLine<CodeLine>[] DiffLines { get; set; }
        public ReviewCommentsModel Comments { get; set; }
        public HashSet<GithubUser> TaggableUsers { get; set; }
        public HashSet<int> HeadingsOfSectionsWithDiff { get; set; } = new HashSet<int>();

        /// <summary>
        /// The number of active conversations for this iteration
        /// </summary>
        public int ActiveConversations { get; set; }

        public int TotalActiveConversations { get; set; }

        public int UsageSampleConversations { get; set; }

        [BindProperty(SupportsGet = true)]
        public string DiffRevisionId { get; set; }

        // Flag to decide whether to  include documentation
        [BindProperty(Name = "doc", SupportsGet = true)]
        public bool ShowDocumentation { get; set; }

        [BindProperty(Name = "diffOnly", SupportsGet = true)]
        public bool ShowDiffOnly { get; set; }

        public IEnumerable<ReviewModel> ReviewsForPackage { get; set; } = new List<ReviewModel>();

        public readonly HashSet<string> PreferredApprovers = new HashSet<string>();

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "api";

            await GetReviewPageModelPropertiesAsync(id, revisionId);

            if (!Review.Revisions.Any())
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }
            var renderedCodeFile = await _codeFileRepository.GetCodeFileAsync(Revision);
            CodeFile = renderedCodeFile.CodeFile;

            var fileDiagnostics = CodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            var fileHtmlLines = renderedCodeFile.Render(ShowDocumentation);

            if (DiffRevision != null)
            {
                var previousRevisionFile = await _codeFileRepository.GetCodeFileAsync(DiffRevision);

                var previousHtmlLines = previousRevisionFile.RenderReadOnly(ShowDocumentation);
                var previousRevisionTextLines = previousRevisionFile.RenderText(ShowDocumentation);
                var fileTextLines = renderedCodeFile.RenderText(ShowDocumentation);

                var diffLines = InlineDiff.Compute(
                    previousRevisionTextLines,
                    fileTextLines,
                    previousHtmlLines,
                    fileHtmlLines);

                Lines = CreateLines(fileDiagnostics, diffLines, Comments);
                if (Lines.Length == 0)
                {
                    var notifcation = new NotificationModel() { Message = "There is no diff between the two revisions.", Level = NotificatonLevel.Info };
                    await _signalRHubContext.Clients.Group(User.Identity.Name).SendAsync("RecieveNotification", notifcation);
                    return Redirect(Request.Headers["referer"]);
                }
            }
            else
            {
                Lines = CreateLines(fileDiagnostics, fileHtmlLines, Comments);
            }

            ActiveConversations = ComputeActiveConversations(fileHtmlLines, Comments);
            TotalActiveConversations = Comments.Threads.Count(t => !t.IsResolved);
            UsageSampleConversations = Comments.Threads.Count(t => t.Comments.FirstOrDefault()?.IsUsageSampleComment == true);
            var filterPreference = _preferenceCache.GetFilterType(User.GetGitHubLogin(), Review.FilterType);
            ReviewsForPackage = await _manager.GetReviewsAsync(Review.ServiceName, Review.PackageDisplayName, filterPreference);

            var approverConfig = _configuration["approvers"];
            if (!string.IsNullOrEmpty(approverConfig))
            {
                foreach (var username in approverConfig.Split(","))
                {
                    if (username.Equals(User.GetGitHubLogin()))
                    {
                        var userCache = _preferenceCache.GetUserPreferences(User).Result;
                        var langs = userCache.ApprovedLanguages.ToHashSet();
                        if (!langs.Any())
                        {
                            UserProfileModel user = await _userProfileRepository.TryGetUserProfileAsync(username);
                            langs = user.Languages;
                            userCache.ApprovedLanguages = langs;
                            _preferenceCache.UpdateUserPreference(userCache, User);
                        }
                        if (langs.Contains(Review.Language) || !langs.Any())
                        {
                            PreferredApprovers.Add(username);
                        }
                    }
                    else
                    {
                        UserProfileModel user = await _userProfileRepository.TryGetUserProfileAsync(username);
                        var langs = user.Languages;
                        if (langs.Contains(Review.Language) || !langs.Any())
                        {
                            PreferredApprovers.Add(username);
                        }
                    }
                }
            }

            return Page();
        }

        public async Task<PartialViewResult> OnGetCodeLineSectionAsync(
            string id, int sectionKey, int? sectionKeyA = null, int? sectionKeyB = null,
            string revisionId = null, string diffRevisionId = null, bool diffOnly = false)
        {
            await GetReviewPageModelPropertiesAsync(id, revisionId, diffRevisionId, diffOnly);
            var renderedCodeFile = await _codeFileRepository.GetCodeFileAsync(Revision);
            var fileDiagnostics = renderedCodeFile.CodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            CodeLine[] currentHtmlLines;
            var userPrefernce = await _preferenceCache.GetUserPreferences(User) ?? new UserPreferenceModel();

            if (DiffRevision != null)
            {
                InlineDiffLine<CodeLine>[] diffLines;
                var previousRevisionFile = await _codeFileRepository.GetCodeFileAsync(DiffRevision);

                if (sectionKeyA != null && sectionKeyB != null)
                {
                    var currentRootNode = renderedCodeFile.GetCodeLineSectionRoot((int)sectionKeyA);
                    var previousRootNode = previousRevisionFile.GetCodeLineSectionRoot((int)sectionKeyB);
                    var diffSectionRoot = _manager.ComputeSectionDiff(previousRootNode, currentRootNode, previousRevisionFile, renderedCodeFile);
                    diffLines = renderedCodeFile.GetDiffCodeLineSection(diffSectionRoot);
                }
                else if (sectionKeyA != null)
                {
                    currentHtmlLines = renderedCodeFile.GetCodeLineSection((int)sectionKeyA);
                    var previousRevisionHtmlLines = new CodeLine[] { };
                    var previousRevisionTextLines = new CodeLine[] { };
                    var currentRevisionTextLines = renderedCodeFile.GetCodeLineSection((int)sectionKeyA, renderType: RenderType.Text);
                    diffLines = InlineDiff.Compute(
                        previousRevisionTextLines,
                        currentRevisionTextLines,
                        previousRevisionHtmlLines,
                        currentHtmlLines);
                }
                else 
                {
                    currentHtmlLines = new CodeLine[] { }; 
                    var previousRevisionHtmlLines = previousRevisionFile.GetCodeLineSection((int)sectionKeyB, RenderType.ReadOnly);
                    var previousRevisionTextLines = previousRevisionFile.GetCodeLineSection((int)sectionKeyB, renderType: RenderType.Text);
                    var currentRevisionTextLines = new CodeLine[] { };
                    diffLines = InlineDiff.Compute(
                        previousRevisionTextLines,
                        currentRevisionTextLines,
                        previousRevisionHtmlLines,
                        currentHtmlLines);
                }
                Lines = CreateLines(fileDiagnostics, diffLines, Comments, true);
            }
            else
            {
                currentHtmlLines = renderedCodeFile.GetCodeLineSection(sectionKey);
                Lines = CreateLines(fileDiagnostics, currentHtmlLines, Comments, true);
            }
            TempData["CodeLineSection"] = Lines;
            TempData["UserPreference"] = userPrefernce;
            return Partial("_CodeLinePartial", sectionKey);
        }

        public async Task<ActionResult> OnPostToggleClosedAsync(string id)
        {
            await _manager.ToggleIsClosedAsync(User, id);

            return RedirectToPage(new { id = id });
        }

        public async Task<ActionResult> OnPostToggleSubscribedAsync(string id)
        {
            await _notificationManager.ToggleSubscribedAsync(User, id);
            return RedirectToPage(new { id = id });
        }

        public async Task<IActionResult> OnPostToggleApprovalAsync(string id, string revisionId)
        {
            await _manager.ToggleApprovalAsync(User, id, revisionId);
            return RedirectToPage(new { id = id });
        }

        public async Task<ActionResult> OnPostRequestReviewersAsync(string id, HashSet<string> reviewers)
        {
            await _manager.RequestApproversAsync(User, id, reviewers);
            await _notificationManager.NotifyApproversOfReview(User, id, reviewers);
            return RedirectToPage(new { id = id });
        }

        public Dictionary<string, string> GetRoutingData(string diffRevisionId = null, bool? showDiffOnly = null, bool? showDocumentation = null, string revisionId = null)
        {
            var routingData = new Dictionary<string, string>();
            routingData["revisionId"] = revisionId;
            routingData["diffRevisionId"] = diffRevisionId;
            routingData["doc"] = (showDocumentation ?? false).ToString();
            routingData["diffOnly"] = (showDiffOnly ?? false).ToString();
            return routingData;
        }

        public async Task<IEnumerable<PullRequestModel>> GetAssociatedPullRequest()
        {
            return await _pullRequestManager.GetPullRequestsModel(Review.ReviewId);
        }

        public async Task<IEnumerable<PullRequestModel>> GetPRsOfAssoicatedReviews()
        {
            var creatingPR = (await _pullRequestManager.GetPullRequestsModel(Review.ReviewId)).FirstOrDefault();
            return await _pullRequestManager.GetPullRequestsModel(creatingPR.PullRequestNumber, creatingPR.RepoName);;
        }

        private async Task GetReviewPageModelPropertiesAsync(string id, string revisionId = null, string diffRevisionId = null, bool diffOnly = false)
        {
            Review = await _manager.GetReviewAsync(User, id);
            TaggableUsers = _commentsManager.GetTaggableUsers();
            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            Revision = Review.Revisions.Last();
            if (revisionId != null) 
            {
                var revision = Review.Revisions.Where(r => r.RevisionId == revisionId);
                if (revision.Count() == 1)
                {
                    Revision = revision.Single();
                }
                else 
                {
                    var notifcation = new NotificationModel() { Message = $"A revision with ID {revisionId} does not exist for this review.", Level = NotificatonLevel.Warning };
                    await _signalRHubContext.Clients.Group(User.Identity.Name).SendAsync("RecieveNotification", notifcation);
                }

            }
                
            PreviousRevisions = Review.Revisions.TakeWhile(r => r != Revision).ToArray();
            DiffRevisionId = (DiffRevisionId == null) ? diffRevisionId : DiffRevisionId;
            ShowDiffOnly = (ShowDiffOnly == false) ? diffOnly : ShowDiffOnly;
            DiffRevision = DiffRevisionId != null ?
                PreviousRevisions.Single(r => r.RevisionId == DiffRevisionId) :
                DiffRevision;
            HeadingsOfSectionsWithDiff = (DiffRevision != null && DiffRevision.HeadingsOfSectionsWithDiff.ContainsKey(Revision.RevisionId)) ? 
                DiffRevision.HeadingsOfSectionsWithDiff[Revision.RevisionId] : new HashSet<int>();
        }

        private InlineDiffLine<CodeLine>[] CreateDiffOnlyLines(InlineDiffLine<CodeLine>[] lines)
        {
            var filteredLines = new List<InlineDiffLine<CodeLine>>();
            int lastAddedLine = -1;
            for (int i = 0; i < lines.Count(); i++)
            {
                if (lines[i].Kind != DiffLineKind.Unchanged)
                {
                    // Find starting index for pre context
                    int preContextIndx = Math.Max(lastAddedLine + 1, i - REVIEW_DIFF_CONTEXT_SIZE);
                    if (preContextIndx < i)
                    {
                        // Add sepearator to show skipping lines. for e.g. .....
                        if (filteredLines.Count > 0)
                        {
                            filteredLines.Add(new InlineDiffLine<CodeLine>(new CodeLine(DIFF_CONTEXT_SEPERATOR, null, null), DiffLineKind.Unchanged));
                        }

                        while (preContextIndx < i)
                        {
                            filteredLines.Add(lines[preContextIndx]);
                            preContextIndx++;
                        }
                    }
                    //Add changed line
                    filteredLines.Add(lines[i]);
                    lastAddedLine = i;

                    // Add post context
                    int contextStart = i +1, contextEnd = i + REVIEW_DIFF_CONTEXT_SIZE;
                    while (contextStart <= contextEnd && contextStart < lines.Count() && lines[contextStart].Kind == DiffLineKind.Unchanged)
                    {
                        filteredLines.Add(lines[contextStart]);
                        lastAddedLine = contextStart;
                        contextStart++;
                    }
                }
            }
            return filteredLines.ToArray();
        }

        private CodeLineModel[] CreateLines(CodeDiagnostic[] diagnostics, InlineDiffLine<CodeLine>[] lines, ReviewCommentsModel comments, bool hideCommentRows = false)
        {
            if (ShowDiffOnly)
            {
                lines = CreateDiffOnlyLines(lines);
                if (lines.Length == 0)
                {
                    return Array.Empty<CodeLineModel>();
                }
            }
            List<int> documentedByLines = new List<int>();
            int lineNumberExcludingDocumentation = 0;
            int diffSectionId = 0;

            return lines.Select(
                (diffLine, index) =>
                {
                    if (diffLine.Line.IsDocumentation)
                    {
                        // documentedByLines must include the index of a line, assuming that documentation lines are counted
                        documentedByLines.Add(++index);
                        return new CodeLineModel(
                            kind: diffLine.Kind,
                            codeLine: diffLine.Line,
                            commentThread: comments.TryGetThreadForLine(diffLine.Line.ElementId, out var thread, hideCommentRows) ?
                                thread :
                                null,
                            diagnostics: diffLine.Kind != DiffLineKind.Removed ?
                                diagnostics.Where(d => d.TargetId == diffLine.Line.ElementId).ToArray() :
                                Array.Empty<CodeDiagnostic>(),
                            lineNumber: lineNumberExcludingDocumentation,
                            documentedByLines: new int[] { },
                            isDiffView: true,
                            diffSectionId: diffLine.Line.SectionKey != null ? ++diffSectionId : null,
                            otherLineSectionKey: diffLine.Kind == DiffLineKind.Unchanged ? diffLine.OtherLine.SectionKey : null,
                            headingsOfSectionsWithDiff: HeadingsOfSectionsWithDiff,
                            isSubHeadingWithDiffInSection: diffLine.IsHeadingWithDiffInSection
                        );
                    }
                    else
                    {
                        CodeLineModel c = new CodeLineModel(
                             kind: diffLine.Kind,
                             codeLine: diffLine.Line,
                             commentThread: diffLine.Kind != DiffLineKind.Removed &&
                                 comments.TryGetThreadForLine(diffLine.Line.ElementId, out var thread, hideCommentRows) ?
                                     thread :
                                     null,
                             diagnostics: diffLine.Kind != DiffLineKind.Removed ?
                                 diagnostics.Where(d => d.TargetId == diffLine.Line.ElementId).ToArray() :
                                 Array.Empty<CodeDiagnostic>(),
                             lineNumber: diffLine.Line.LineNumber ?? ++lineNumberExcludingDocumentation,
                             documentedByLines: documentedByLines.ToArray(),
                             isDiffView: true,
                             diffSectionId: diffLine.Line.SectionKey != null ? ++diffSectionId : null,
                             otherLineSectionKey: diffLine.Kind == DiffLineKind.Unchanged ? diffLine.OtherLine.SectionKey : null,
                             headingsOfSectionsWithDiff: HeadingsOfSectionsWithDiff,
                             isSubHeadingWithDiffInSection: diffLine.IsHeadingWithDiffInSection
                         );
                        documentedByLines.Clear();
                        return c;
                    }
                }).ToArray();
        }

        private CodeLineModel[] CreateLines(CodeDiagnostic[] diagnostics, CodeLine[] lines, ReviewCommentsModel comments, bool hideCommentRows = false)
        {
            List<int> documentedByLines = new List<int>();
            int lineNumberExcludingDocumentation = 0;
            return lines.Select(
                (line, index) =>
                {
                    if (line.IsDocumentation)
                    {
                        // documentedByLines must include the index of a line, assuming that documentation lines are counted
                        documentedByLines.Add(++index);
                        return new CodeLineModel(
                            DiffLineKind.Unchanged,
                            line,
                            comments.TryGetThreadForLine(line.ElementId, out var thread, hideCommentRows) ? thread : null,
                            diagnostics.Where(d => d.TargetId == line.ElementId).ToArray(),
                            lineNumberExcludingDocumentation,
                            new int[] {}
                        );
                    }
                    else
                    {
                        CodeLineModel c = new CodeLineModel(
                            DiffLineKind.Unchanged,
                            line,
                            comments.TryGetThreadForLine(line.ElementId, out var thread, hideCommentRows) ? thread : null,
                            diagnostics.Where(d => d.TargetId == line.ElementId).ToArray(),
                            line.LineNumber ?? ++lineNumberExcludingDocumentation,
                            documentedByLines.ToArray()
                        );
                        documentedByLines.Clear();
                        return c;
                    }
                }).ToArray();
        }

        private int ComputeActiveConversations(CodeLine[] lines, ReviewCommentsModel comments)
        {
            int activeThreads = 0;
            foreach (CodeLine line in lines)
            {
                if (string.IsNullOrEmpty(line.ElementId))
                {
                    continue;
                }

                // if we have comments for this line and the thread has not been resolved.
                // Add "&& !thread.Comments.First().IsUsageSampleComment()" to exclude sample comments from being counted (This also prevents the popup before approval)
                if (comments.TryGetThreadForLine(line.ElementId, out CommentThreadModel thread) && !thread.IsResolved)
                {
                    activeThreads++;
                }
            }
            return activeThreads;
        }
    }
}
