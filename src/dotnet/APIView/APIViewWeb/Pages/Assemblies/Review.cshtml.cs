using System;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewPageModel : PageModel
    {
        private readonly ReviewManager _manager;

        private readonly BlobCodeFileRepository _codeFileRepository;

        private readonly CommentsManager _commentsManager;

        public ReviewPageModel(
            ReviewManager manager,
            BlobCodeFileRepository codeFileRepository,
            CommentsManager commentsManager)
        {
            _manager = manager;
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
        }

        public ReviewModel Review { get; set; }
        public ReviewRevisionModel Revision { get; set; }
        public CodeFile CodeFile { get; set; }
        public LineApiView[] Lines { get; set; }
        public ReviewCommentsModel Comments { get; set; }

        /// <summary>
        /// The number of active conversations for this iteration
        /// </summary>
        public int ActiveConversations { get; set; }

        public int TotalActiveConversations { get; set; }

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "api";

            Review = await _manager.GetReviewAsync(User, id);

            if (!Review.Revisions.Any())
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }

            Revision = revisionId != null ?
                Review.Revisions.Single(r => r.RevisionId == revisionId) :
                Review.Revisions.Last();

            var reviewFile = Revision.Files.Single();
            CodeFile = await _codeFileRepository.GetCodeFileAsync(Revision.RevisionId, reviewFile.ReviewFileId);

            Lines = new CodeFileHtmlRenderer().Render(CodeFile).ToArray();
            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            ActiveConversations = ComputeActiveConversations(Lines, Comments);
            TotalActiveConversations = Comments.Threads.Count(t => !t.IsResolved);

            return Page();
        }

        private int ComputeActiveConversations(LineApiView[] lines, ReviewCommentsModel comments)
        {
            int activeThreads = 0;
            foreach (LineApiView line in lines)
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
    }
}
