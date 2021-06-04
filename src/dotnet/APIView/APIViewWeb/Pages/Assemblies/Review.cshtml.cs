using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIView.DIff;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

        public ReviewPageModel(
            ReviewManager manager,
            BlobCodeFileRepository codeFileRepository,
            CommentsManager commentsManager,
            NotificationManager notificationManager)
        {
            _manager = manager;
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
            _notificationManager = notificationManager;
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

        [BindProperty(SupportsGet = true)]
        public string DiffRevisionId { get; set; }

        // Flag to decide whether to  include documentation
        [BindProperty(Name = "doc", SupportsGet = true)]
        public bool ShowDocumentation { get; set; }

        [BindProperty(Name = "diffOnly", SupportsGet = true)]
        public bool ShowDiffOnly { get; set; }

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "api";

            Review = await _manager.GetReviewAsync(User, id);

            if (!Review.Revisions.Any())
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }

            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            Revision = revisionId != null ?
                Review.Revisions.Single(r => r.RevisionId == revisionId) :
                Review.Revisions.Last();
            PreviousRevisions = Review.Revisions.TakeWhile(r => r != Revision).ToArray();

            var renderedCodeFile = await _codeFileRepository.GetCodeFileAsync(Revision);
            CodeFile = renderedCodeFile.CodeFile;

            var fileDiagnostics = CodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            var fileHtmlLines = renderedCodeFile.Render(ShowDocumentation);

            if (DiffRevisionId != null)
            {
                DiffRevision = PreviousRevisions.Single(r=>r.RevisionId == DiffRevisionId);

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
            }
            else
            {
                Lines = CreateLines(fileDiagnostics, fileHtmlLines, Comments);
            }

            ActiveConversations = ComputeActiveConversations(fileHtmlLines, Comments);
            TotalActiveConversations = Comments.Threads.Count(t => !t.IsResolved);

            return Page();
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
                            filteredLines.Add(new InlineDiffLine<CodeLine>(new CodeLine(DIFF_CONTEXT_SEPERATOR, null), DiffLineKind.Unchanged));
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
                diffLine => new CodeLineModel(
                    diffLine.Kind,
                    diffLine.Line,
                    diffLine.Kind != DiffLineKind.Removed &&
                    comments.TryGetThreadForLine(diffLine.Line.ElementId, out var thread) ?
                        thread :
                        null,

                    diffLine.Kind != DiffLineKind.Removed ?
                        diagnostics.Where(d => d.TargetId == diffLine.Line.ElementId).ToArray() :
                        Array.Empty<CodeDiagnostic>()
                )).ToArray();
        }

        private CodeLineModel[] CreateLines(CodeDiagnostic[] diagnostics, CodeLine[] lines, ReviewCommentsModel comments)
        {
            return lines.Select(
                line => new CodeLineModel(
                    DiffLineKind.Unchanged,
                    line,
                    comments.TryGetThreadForLine(line.ElementId, out var thread) ? thread : null,
                    diagnostics.Where(d => d.TargetId == line.ElementId).ToArray()
                )).ToArray();
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
                if (comments.TryGetThreadForLine(line.ElementId, out CommentThreadModel thread) && !thread.IsResolved)
                {
                    activeThreads++;
                }
            }
            return activeThreads;
        }

        public async Task<ActionResult> OnPostRefreshModelAsync(string id)
        {
            await _manager.UpdateReviewAsync(User, id);

            return RedirectToPage(new { id = id });
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
        public Dictionary<string, string> GetRoutingData(string diffRevisionId = null, bool? showDocumentation = null, bool? showDiffOnly = null)
        {
            var routingData = new Dictionary<string, string>();
            routingData["diffRevisionId"] = diffRevisionId;
            routingData["doc"] = (showDocumentation ?? false).ToString();
            routingData["diffOnly"] = (showDiffOnly ?? false).ToString();
            return routingData;
        }
    }
}
