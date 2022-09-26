using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIView.DIff;
using APIViewWeb.Helpers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewPageModel : PageModel
    {
        private static int REVIEW_DIFF_CONTEXT_SIZE = 3;
        private const string DIFF_CONTEXT_SEPERATOR = "<br><span>.....</span><br>";

        private readonly ReviewManager _manager;

        private readonly BlobCodeFileRepository _codeFileRepository;

        private readonly CommentsManager _commentsManager;

        private readonly NotificationManager _notificationManager;

        public readonly UserPreferenceCache _preferenceCache;

        private readonly IConfiguration _configuration;

        public ReviewPageModel(
            ReviewManager manager,
            BlobCodeFileRepository codeFileRepository,
            CommentsManager commentsManager,
            NotificationManager notificationManager,
            UserPreferenceCache preferenceCache,
            IConfiguration configuration)
        {
            _manager = manager;
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
            _notificationManager = notificationManager;
            _preferenceCache = preferenceCache;
            _configuration = configuration;

        }

        public ReviewModel Review { get; set; }
        public ReviewRevisionModel Revision { get; set; }
        public ReviewRevisionModel DiffRevision { get; set; }
        public ReviewRevisionModel[] PreviousRevisions {get; set; }

        public CodeFile CodeFile { get; set; }

        public CodeLineModel[] Lines { get; set; }
        public InlineDiffLine<CodeLine>[] DiffLines { get; set; }
        public ReviewCommentsModel Comments { get; set; }

        /// <summary>
        /// The number of active conversations for this iteration
        /// </summary>
        public int ActiveConversations { get; set; }

        public int TotalActiveConversations { get; set; }

        public int UsageSampleConversations { get; set; }

        [BindProperty(SupportsGet = true)]
        public string DiffRevisionId { get; set; }

        [BindProperty(Name = "diffOnly", SupportsGet = true)]
        public bool ShowDiffOnly { get; set; }

        public IEnumerable<ReviewModel> ReviewsForPackage { get; set; } = new List<ReviewModel>();

        public readonly HashSet<string> approvers = new HashSet<string>();

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "api";

            Review = await _manager.GetReviewAsync(User, id);

            if (!Review.Revisions.Any())
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }

            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            Revision = GetReviewRevision(revisionId);
            PreviousRevisions = Review.Revisions.TakeWhile(r => r != Revision).ToArray();

            var renderedCodeFile = await _codeFileRepository.GetCodeFileAsync(Revision);
            CodeFile = renderedCodeFile.CodeFile;

            var fileDiagnostics = CodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            var fileHtmlLines = renderedCodeFile.Render();

            if (DiffRevisionId != null)
            {
                DiffRevision = PreviousRevisions.Single(r=>r.RevisionId == DiffRevisionId);

                var previousRevisionFile = await _codeFileRepository.GetCodeFileAsync(DiffRevision);

                var previousHtmlLines = previousRevisionFile.RenderReadOnly();
                var previousRevisionTextLines = previousRevisionFile.RenderText();
                var fileTextLines = renderedCodeFile.RenderText();

                var diffLines = InlineDiff.Compute(
                    previousRevisionTextLines,
                    fileTextLines,
                    previousHtmlLines,
                    fileHtmlLines);

                Lines = CreateLines(fileDiagnostics, diffLines, Comments);
            }
            else
            {
                Lines = CreateLines(fileDiagnostics, fileHtmlLines, Comments);
            }

            ActiveConversations = ComputeActiveConversations(fileHtmlLines, Comments);
            TotalActiveConversations = Comments.Threads.Count(t => !t.IsResolved);
            UsageSampleConversations = Comments.Threads.Count(t => t.Comments.First().IsUsageSampleComment);
            var filterPreference = _preferenceCache.GetFilterType(User.GetGitHubLogin(), Review.FilterType);
            ReviewsForPackage = await _manager.GetReviewsAsync(Review.ServiceName, Review.PackageDisplayName, filterPreference);

            var approverConfig = _configuration["approvers"];
            if (!string.IsNullOrEmpty(approverConfig))
            {
                foreach (var username in approverConfig.Split(","))
                {
                    approvers.Add(username);
                }
            }

            return Page();
        }

        public async Task<PartialViewResult> OnGetCodeLineSectionAsync(string id, int sectionId, string revisionId = null)
        {
            Review = await _manager.GetReviewAsync(User, id);
            Revision = GetReviewRevision(revisionId);
            var renderedCodeFile = await _codeFileRepository.GetCodeFileAsync(Revision);
            var htmlLines = renderedCodeFile.GetCodeLineSection(sectionId);
            var fileDiagnostics = renderedCodeFile.CodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            Lines = CreateLines(fileDiagnostics, htmlLines, Comments, true);
            TempData["CodeLineSection"] = Lines;
            TempData["UserPreference"] = PageModelHelpers.GetUserPreference(_preferenceCache, User.GetGitHubLogin()) ?? new UserPreferenceModel();
            return Partial("_CodeLinePartial", sectionId);
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
            // TODO: Email Notifications for those requested
            _manager.RequestApproversAsync(User, id, reviewers);
            return RedirectToPage(new { id = id });
        }

        public IActionResult OnGetUpdatePageSettings(bool hideLineNumbers = false, bool hideLeftNavigation = false)
        {
            _preferenceCache.UpdateUserPreference(new UserPreferenceModel()
            {
                UserName = User.GetGitHubLogin(),
                HideLeftNavigation = hideLeftNavigation,
                HideLineNumbers = hideLineNumbers
            });
            return new EmptyResult();
        }

        public Dictionary<string, string> GetRoutingData(string diffRevisionId = null, bool? showDiffOnly = null, string revisionId = null)
        {
            var routingData = new Dictionary<string, string>();
            routingData["revisionId"] = revisionId;
            routingData["diffRevisionId"] = diffRevisionId;
            routingData["diffOnly"] = (showDiffOnly ?? false).ToString();
            return routingData;
        }

        public UserPreferenceModel GetUserPreference()
        {
            return _preferenceCache.GetUserPreferences(User.GetGitHubLogin());
        }

        private ReviewRevisionModel GetReviewRevision(string revisionId = null)
        {
            return revisionId != null ?
                Review.Revisions.Single(r => r.RevisionId == revisionId) :
                Review.Revisions.Last();
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

        private CodeLineModel[] CreateLines(CodeDiagnostic[] diagnostics, InlineDiffLine<CodeLine>[] lines, ReviewCommentsModel comments)
        {
            if (ShowDiffOnly)
            {
                lines = CreateDiffOnlyLines(lines);
            }

            return lines.Select(
                (diffLine, index) => new CodeLineModel(
                    diffLine.Kind,
                    diffLine.Line,
                    diffLine.Kind != DiffLineKind.Removed &&
                    comments.TryGetThreadForLine(diffLine.Line.ElementId, out var thread) ?
                        thread :
                        null,

                    diffLine.Kind != DiffLineKind.Removed ?
                        diagnostics.Where(d => d.TargetId == diffLine.Line.ElementId).ToArray() :
                        Array.Empty<CodeDiagnostic>(),
                    diffLine.Line.LineNumber ?? ++index,
                    new int[] { }
                )).ToArray();
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
