using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

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

        [BindProperty]
        public CommentModel Comment { get; set; }

        public async Task<ActionResult> OnPostDeleteAsync(string id, string commentId, string elementId)
        {
            await _commentsManager.DeleteCommentAsync(User, id, commentId);

            return await CommentPartialAsync(id, elementId);
        }

        private async Task<ActionResult> CommentPartialAsync(string id, string elementId)
        {
            var comments = await _commentsManager.GetReviewCommentsAsync(id);
            comments.TryGetThreadForLine(elementId, out var partialModel);
            return new PartialViewResult
            {
                ViewName = "_CommentThreadPartial",
                ViewData = new ViewDataDictionary<CommentThreadModel>(ViewData, partialModel)
            };
        }

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "api";

            Review = await _manager.GetReviewAsync(User, id);

            Revision = revisionId != null ?
                Review.Revisions.Single(r => r.RevisionId == revisionId) :
                Review.Revisions.Last();

            var reviewFile = Revision.Files.SingleOrDefault();
            if (reviewFile != null)
            {
                CodeFile = await _codeFileRepository.GetCodeFileAsync(Revision.RevisionId, reviewFile.ReviewFileId);
            }
            else
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }

            Lines = new CodeFileHtmlRenderer().Render(CodeFile).ToArray();
            Comments = await _commentsManager.GetReviewCommentsAsync(id);

            return Page();
        }

        public async Task<ActionResult> OnPostAsync(string id, string revisionId)
        {
            Comment.TimeStamp = DateTime.UtcNow;
            Comment.ReviewId = id;
            Comment.RevisionId = revisionId;

            await _commentsManager.AddCommentAsync(User, Comment);

            return await CommentPartialAsync(id, Comment.ElementId);
        }


        public async Task<ActionResult> OnPostResolveAsync(string id, string lineId)
        {
            await _commentsManager.ResolveConversation(User, id, lineId);

            return await CommentPartialAsync(id, lineId);
        }

        public async Task<ActionResult> OnPostUnresolveAsync(string id, string lineId)
        {
            await _commentsManager.UnresolveConversation(User, id, lineId);

            return await CommentPartialAsync(id, lineId);
        }

        public async Task<ActionResult> OnPostRefreshModelAsync(string id)
        {
            await _manager.UpdateReviewAsync(User, id);

            return RedirectToPage(new { id = id });
        }
    }
}
