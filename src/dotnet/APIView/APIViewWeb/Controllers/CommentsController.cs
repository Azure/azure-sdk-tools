using System;
using System.Threading.Tasks;
using APIViewWeb.DTO;
using APIViewWeb.Hubs;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Azure;
using Octokit;

namespace APIViewWeb.Controllers
{
    [Authorize("RequireOrganization")]
    public class CommentsController: Controller
    {
        private readonly ICommentsManager _commentsManager;
        private readonly IReviewManager _reviewManager;
        private readonly IHubContext<SignalRHub> _notificationHubContext;
        private readonly INotificationManager _notificationManager;

        public CommentsController(ICommentsManager commentsManager, IReviewManager reviewManager, INotificationManager notificationManager, IHubContext<SignalRHub> notificationHub)
        {
            _notificationHubContext = notificationHub;
            _commentsManager = commentsManager;
            _reviewManager = reviewManager;
            _notificationManager = notificationManager;
        }

        [HttpPost]
        public async Task<ActionResult> Add(string reviewId, string revisionId, string elementId, string commentText, string sectionClass, string groupNo, string[] taggedUsers, string resolutionLock = "off", bool usageSampleComment = false, string signalRConnectionId = null)
        {
            if (string.IsNullOrEmpty(commentText))
            {
                var notifcation = new NotificationModel() { Message = "Comment Text cannot be empty. Please type your comment entry and try again.", Level = NotificatonLevel.Error };
                await _notificationHubContext.Clients.Group(User.Identity.Name).SendAsync("RecieveNotification", notifcation);
                return new BadRequestResult();
            }

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
            }

            await _commentsManager.AddCommentAsync(User, comment);
            var review = await _reviewManager.GetReviewAsync(User, reviewId);
            if (review != null)
            {
                await _notificationManager.SubscribeAsync(review,User);
            }

            var commentDto = new CommentDto(); 
            commentDto.TimeStamp = DateTime.UtcNow;
            commentDto.ReviewId = reviewId;
            commentDto.RevisionId = revisionId;
            commentDto.ElementId = elementId;
            commentDto.Username = comment.Username;
            commentDto.Comment = commentText;
            commentDto.CommentId = comment.CommentId;

<<<<<<< HEAD
            //await _notificationHubContext.Clients.AllExcept(signalRConnectionId).SendAsync("ReceiveComment", commentDto); // TODO: need to check if valid signalR connection id 
            await _notificationHubContext.Clients.All.SendAsync("ReceiveComment", commentDto); // TODO: for debugging. remove for PR 
=======
            await _notificationHubContext.Clients.AllExcept(signalRConnectionId).SendAsync("ReceiveComment", commentDto);
            await _notificationHubContext.Clients.User(signalRConnectionId).SendAsync("ReceiveCommentTest", commentDto); // NOTE: for debugging purposes only
>>>>>>> dc4d3ab8b2a65db64c1bbddf40ba119ef4c49d40

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
