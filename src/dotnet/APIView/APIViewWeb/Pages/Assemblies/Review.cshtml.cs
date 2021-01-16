using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIView.DIff;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Respositories;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewPageModel : PageModel
    {
        private const int REVIEW_DIFF_CONTEXT_SIZE = 3;
        private const string DIFF_CONTEXT_LINEBREAK = "<br>";
        private const string DIFF_CONTEXT_SEPERATOR = "<span class='code-diff-context'>.....</span>";
        private const string DIFF_CONTEXT_ENCLOSING_START = " <span class='code-diff-context'>";
        private const string DIFF_CONTEXT_ENCLOSING_END = "</span> ";

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

            // Keep track of any enclosing context and the level of indent the
            // deepest corresponds to.  This allows us to include the enclosing
            // classes, namespaces, etc. when viewing only the diff.  This is
            // best effort that presumes significant indentation.
            var enclosingContext = new List<(int, string)>();
            int enclosingContextIndent = 0;
            var enclosingMarkup = new StringBuilder(capacity: 256);

            for (int i = 0; i < lines.Count(); i++)
            {
                // Compare the current line's indent to the enclosing context
                (int indentSize, _) = GetIndentAndCode(lines[i].Line.DisplayString);
                if (indentSize > enclosingContextIndent && i > 0)
                {
                    // If this line is indented further, then the previous line
                    // should be part of the enclosing context
                    (_, string code) = GetIndentAndCode(lines[i - 1].Line.DisplayString);
                    enclosingContext.Add((i - 1, code.Trim()));
                    enclosingContextIndent = indentSize;
                }
                else if (indentSize < enclosingContextIndent)
                {
                    // If this line was an outdent, remove the enclosing context
                    // (but don't assume there was any context in case some
                    // languages have weird whitespace indentation habits)
                    if (enclosingContext.Count > 0)
                    {
                        enclosingContext.RemoveAt(enclosingContext.Count - 1);
                        enclosingContextIndent = indentSize;
                    }
                    else
                    {
                        enclosingContextIndent = 0;
                    }
                }

                if (lines[i].Kind != DiffLineKind.Unchanged)
                {
                    // Find starting index for pre context
                    int preContextIndx = Math.Max(lastAddedLine + 1, i - REVIEW_DIFF_CONTEXT_SIZE);
                    if (preContextIndx < i)
                    {
                        // Don't place any "....." if we're at the very top
                        if (i > REVIEW_DIFF_CONTEXT_SIZE)
                        {
                            // Add a "....." no matter what for diffs that have
                            // no enclosing context but aren't contiguous
                            enclosingMarkup.Clear();
                            enclosingMarkup.Append(DIFF_CONTEXT_LINEBREAK);
                            enclosingMarkup.Append(DIFF_CONTEXT_SEPERATOR);

                            // Add any enclosing namespaces, classes, etc.
                            foreach ((int parentLine, string parentCode) in enclosingContext)
                            {
                                // Ignore any enclosing scopes included in the
                                // +/-3 lines of diff context still shown
                                if (parentLine >= preContextIndx) { continue; }

                                enclosingMarkup.Append(DIFF_CONTEXT_ENCLOSING_START);
                                enclosingMarkup.Append(parentCode);
                                enclosingMarkup.Append(DIFF_CONTEXT_ENCLOSING_END);
                                enclosingMarkup.Append(DIFF_CONTEXT_SEPERATOR);
                            }
                            enclosingMarkup.Append(DIFF_CONTEXT_LINEBREAK);
                            filteredLines.Add(new InlineDiffLine<CodeLine>(new CodeLine(enclosingMarkup.ToString(), null), DiffLineKind.Unchanged));
                        }

                        while (preContextIndx < i)
                        {
                            filteredLines.Add(lines[preContextIndx]);
                            preContextIndx++;
                        }
                    }

                    // Add changed line
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

            // The lines of code are already marked up with HTML which makes
            // them a little difficult to process.  This strips out all of the
            // markup and computes the size of the whitespace indent at the
            // beginning of the line.
            static (int indentSize, string code) GetIndentAndCode(string line)
            {
                if (string.IsNullOrEmpty(line)) { return (0, null); }

                // Parse the line as HTML
                HtmlDocument html = new HtmlDocument();
                html.LoadHtml(line);

                // Get the line's code
                string code = html.DocumentNode.InnerText ?? "";

                // Find the last whitespace character at the start of the line
                int last = -1;
                for (; (last + 1) < line.Length && char.IsWhiteSpace(code, last + 1); last++) { }

                // Return the size of the indent and the line of code
                return (last + 1, code);
            }
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
