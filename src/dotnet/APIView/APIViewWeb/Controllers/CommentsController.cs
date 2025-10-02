using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace APIViewWeb.Controllers
{
    [Authorize("RequireOrganization")]
    public class CommentsController: Controller
    {
        private readonly ICommentsManager _commentsManager;
        private readonly IReviewManager _reviewManager;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly INotificationManager _notificationManager;

        public CommentsController(ICommentsManager commentsManager, IReviewManager reviewManager, INotificationManager notificationManager, IHubContext<SignalRHub> signalRHub)
        {
            _signalRHubContext = signalRHub;
            _commentsManager = commentsManager;
            _reviewManager = reviewManager;
            _notificationManager = notificationManager;
        }

        [HttpPost]
        public async Task<ActionResult> Add(string reviewId, string revisionId, string elementId, string commentText, string sectionClass, string[] taggedUsers, string resolutionLock = "off", bool usageSampleComment = false, string crossLangId = null, CommentSeverity? severity = null)
        {
            if (string.IsNullOrEmpty(commentText))
            {
                var notifcation = new NotificationModel() { Message = "Comment Text cannot be empty. Please type your comment entry and try again.", Level = NotificatonLevel.Error };
                await _signalRHubContext.Clients.Group(User.GetGitHubLogin()).SendAsync("RecieveNotification", notifcation);
                return new BadRequestResult();
            }

            var comment = new CommentItemModel();
            comment.CreatedOn = DateTime.UtcNow;
            comment.ReviewId = reviewId;
            comment.APIRevisionId = revisionId;
            comment.ElementId = elementId;
            comment.SectionClass = sectionClass;
            comment.CommentText = commentText;
            comment.CommentType = (usageSampleComment) ? CommentType.SampleRevision : CommentType.APIRevision;
            comment.ResolutionLocked = !resolutionLock.Equals("on");
            comment.CreatedBy = User.GetGitHubLogin();
            comment.CrossLanguageId = crossLangId;
            comment.Severity = severity;

            foreach(string user in taggedUsers)
            {
                comment.TaggedUsers.Add(user);
            }

            await _commentsManager.AddCommentAsync(User, comment);
            var review = await _reviewManager.GetReviewAsync(User, reviewId);
            if (review != null)
            {
                await _notificationManager.SubscribeAsync(review,User);
            }
            
            // Query comments for the specific element instead of all review comments
            // This is more efficient and avoids potential consistency issues
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
            await _commentsManager.SoftDeleteCommentAsync(User, reviewId, commentId);

            return await CommentPartialAsync(reviewId, elementId);
        }

        [HttpPost]
        public async Task<ActionResult> ToggleUpvote(string reviewId, string commentId, string elementId)
        {
            await _commentsManager.ToggleUpvoteAsync(User, reviewId, commentId);

            return await CommentPartialAsync(reviewId, elementId);
        }

        private async Task<ActionResult> CommentPartialAsync(string reviewId, string elementId, IEnumerable<CommentItemModel> knownComments = null)
        {
            CommentThreadModel partialModel = null;
            
            if (knownComments != null)
            {
                // Use the provided comments directly to build the thread model
                // This avoids CosmosDB eventual consistency issues when comments are just added
                partialModel = new CommentThreadModel(reviewId, elementId, knownComments);
            }
            else
            {
                // Query comments for the specific element to avoid loading all review comments
                var elementComments = await _commentsManager.GetCommentsAsync(reviewId, elementId);
                if (elementComments != null && elementComments.Any())
                {
                    partialModel = new CommentThreadModel(reviewId, elementId, elementComments);
                }
            }
            
            return PartialView("_CommentThreadPartial", partialModel);
        }
    }
}
