using System;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Controllers
{
    [Authorize("RequireOrganization")]
    public class CommentsController: Controller
    {
        private readonly CommentsManager _commentsManager;
        private readonly ReviewManager _reviewManager;
        private readonly NotificationManager _notificationManager;

        public CommentsController(CommentsManager commentsManager, ReviewManager reviewManager, NotificationManager notificationManager)
        {
            _commentsManager = commentsManager;
            _reviewManager = reviewManager;
            _notificationManager = notificationManager;
        }

        [HttpPost]
        public async Task<ActionResult> Add(string reviewId, string revisionId, string elementId, string commentText, string sectionClass, string groupNo, string[] taggedUsers, string resolutionLock = "off", bool usageSampleComment = false)
        {
            var comment = new CommentModel();
            comment.TimeStamp = DateTime.UtcNow;
            comment.ReviewId = reviewId;
            comment.RevisionId = revisionId;
            comment.ElementId = elementId;
            comment.SectionClass = sectionClass;
            comment.Comment = commentText;
            comment.GroupNo = groupNo;
            comment.IsUsageSampleComment = usageSampleComment;
            comment.ResolutionLocked = !resolutionLock.Equals("on");
            comment.Username = User.GetGitHubLogin();

            foreach(string user in taggedUsers)
            {
                comment.TaggedUsers.Add(user);
                await _notificationManager.NotifyUserOnCommentTag(user, comment);
            }

            await _commentsManager.AddCommentAsync(User, comment);
            var review = await _reviewManager.GetReviewAsync(User, reviewId);
            if (review != null)
            {
                await _notificationManager.SubscribeAsync(review,User);
            }
            return await CommentPartialAsync(reviewId, comment.ElementId);
        }

        [HttpPost]
        public async Task<ActionResult> Update(string reviewId, string commentId, string commentText, string[] taggedUsers)
        {
            var comment =  await _commentsManager.UpdateCommentAsync(User, reviewId, commentId, commentText, taggedUsers);

            return await CommentPartialAsync(reviewId, comment.ElementId);
        }


        [HttpPost]
        public async Task<ActionResult> Resolve(string reviewId, string elementId)
        {
            await _commentsManager.ResolveConversation(User, reviewId, elementId);

            return await CommentPartialAsync(reviewId, elementId);
        }

        [HttpPost]
        public async Task<ActionResult> Unresolve(string reviewId, string elementId)
        {
            await _commentsManager.UnresolveConversation(User, reviewId, elementId);

            return await CommentPartialAsync(reviewId, elementId);
        }

        [HttpPost]
        public async Task<ActionResult> Delete(string reviewId, string commentId, string elementId)
        {
            await _commentsManager.DeleteCommentAsync(User, reviewId, commentId);

            return await CommentPartialAsync(reviewId, elementId);
        }

        [HttpPost]
        public async Task<ActionResult> ToggleUpvote(string reviewId, string commentId, string elementId)
        {
            await _commentsManager.ToggleUpvoteAsync(User, reviewId, commentId);

            return await CommentPartialAsync(reviewId, elementId);
        }

        private async Task<ActionResult> CommentPartialAsync(string reviewId, string elementId)
        {
            var comments = await _commentsManager.GetReviewCommentsAsync(reviewId);
            comments.TryGetThreadForLine(elementId, out var partialModel);
            return PartialView("_CommentThreadPartial", partialModel);
        }
    }
}
