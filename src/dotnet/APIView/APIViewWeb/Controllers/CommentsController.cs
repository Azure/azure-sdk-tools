using System;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Controllers
{
    [Authorize("RequireOrganization")]
    public class CommentsController: Controller
    {
        private readonly CommentsManager _commentsManager;

        public CommentsController(CommentsManager commentsManager)
        {
            _commentsManager = commentsManager;
        }

        [HttpPost]
        public async Task<ActionResult> Add(string reviewId, string revisionId, string elementId, string commentText, string language)
        {
            var comment = new CommentModel();
            comment.TimeStamp = DateTime.UtcNow;
            comment.ReviewId = reviewId;
            comment.RevisionId = revisionId;
            comment.ElementId = elementId;
            comment.Comment = commentText;

            await _commentsManager.AddCommentAsync(User, comment);

            return await CommentPartialAsync(reviewId, comment.ElementId, language);
        }

        [HttpPost]
        public async Task<ActionResult> Update(string reviewId, string commentId, string commentText, string language)
        {
            var comment =  await _commentsManager.UpdateCommentAsync(User, reviewId, commentId, commentText);

            return await CommentPartialAsync(reviewId, comment.ElementId, language);
        }


        [HttpPost]
        public async Task<ActionResult> Resolve(string reviewId, string elementId, string language)
        {
            await _commentsManager.ResolveConversation(User, reviewId, elementId);

            return await CommentPartialAsync(reviewId, elementId, language);
        }

        [HttpPost]
        public async Task<ActionResult> Unresolve(string reviewId, string elementId, string language)
        {
            await _commentsManager.UnresolveConversation(User, reviewId, elementId);

            return await CommentPartialAsync(reviewId, elementId, language);
        }

        [HttpPost]
        public async Task<ActionResult> Delete(string reviewId, string commentId, string elementId, string language)
        {
            await _commentsManager.DeleteCommentAsync(User, reviewId, commentId);

            return await CommentPartialAsync(reviewId, elementId, language);
        }

        [HttpPost]
        public async Task<ActionResult> ToggleUpvote(string reviewId, string commentId, string elementId, string language)
        {
            await _commentsManager.ToggleUpvoteAsync(User, reviewId, commentId);

            return await CommentPartialAsync(reviewId, elementId, language);
        }

        private async Task<ActionResult> CommentPartialAsync(string reviewId, string elementId, string language)
        {
            var comments = await _commentsManager.GetReviewCommentsAsync(reviewId, language);
            comments.TryGetThreadForLine(elementId, out var partialModel);
            return PartialView("_CommentThreadPartial", partialModel);
        }
    }
}
