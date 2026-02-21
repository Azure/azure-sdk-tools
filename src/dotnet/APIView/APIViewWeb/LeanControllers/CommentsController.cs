using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.LeanControllers
{
    public class CommentsController : BaseApiController
    {
        private readonly ILogger<CommentsController> _logger;
        private readonly ICommentsManager _commentsManager;
        private readonly IReviewManager _reviewManager;
        private readonly INotificationManager _notificationManager;

        public CommentsController(
            ICommentsManager commentManager,
            IReviewManager reviewManager,
            INotificationManager notificationManager, 
            ILogger<CommentsController> logger)
        {
            _logger = logger;
            _commentsManager = commentManager;
            _reviewManager = reviewManager;
            _notificationManager = notificationManager;
        }


        /// <summary>
        /// Retrieve conversation information
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        [HttpGet("{reviewId}/{apiRevisionId}", Name = "GetConversationInfo")]
        public async Task<ActionResult<IEnumerable<CommentItemModel>>> GetConversationInfoAsync(string reviewId, string apiRevisionId)
        {
            var comments = await _commentsManager.GetCommentsAsync(reviewId, false);
            var commentsInAPIRevision = comments.Where(c => c.CommentType == CommentType.APIRevision).ToList();
            var sampleComments = comments.Where(c => c.CommentType == CommentType.SampleRevision).ToList();
            var totalActiveConversiations = 0;

            foreach (var group in comments.GroupBy(c => c.ThreadId ?? c.ElementId))
            {
                if (!group.Any(c => c.IsResolved))
                {
                    totalActiveConversiations++;
                }
            }

            int totalActiveConversationInApiRevisions = sampleComments.GroupBy(c => c.ThreadId ?? c.ElementId)
                .Count(group => !group.Any(c => c.IsResolved));

            int totalActiveConversationInSampleRevisions = commentsInAPIRevision.GroupBy(c => c.ThreadId ?? c.ElementId)
                .Count(group => !group.Any(c => c.IsResolved));

            dynamic conversationInfobject = new ExpandoObject();
            conversationInfobject.TotalActiveConversations = totalActiveConversiations;
            conversationInfobject.ActiveConversationsInActiveAPIRevision = totalActiveConversationInApiRevisions;
            conversationInfobject.ActiveConversationsInSampleRevisions = totalActiveConversationInSampleRevisions;
            return new LeanJsonResult(conversationInfobject, StatusCodes.Status200OK);
        }


        /// <summary>
        ///     Retrieve comments for a review.
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="isDeleted"></param>
        /// <param name="commentType"></param>
        /// <returns></returns>
        [HttpGet("{reviewId}", Name = "GetComments")]
        public async Task<ActionResult<IEnumerable<CommentItemModel>>> GetCommentsAsync(string reviewId,
            bool isDeleted = false, CommentType? commentType = null)
        {
            IEnumerable<CommentItemModel> comments =
                await _commentsManager.GetCommentsAsync(reviewId, isDeleted, commentType);
            return new LeanJsonResult(comments, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Create a new Comment
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="sampleRevisionId"></param>
        /// <param name="elementId"></param>
        /// <param name="commentText"></param>
        /// <param name="commentType"></param>
        /// <param name="severity"></param>
        /// <param name="resolutionLocked"></param>
        /// <param name="threadId"></param>
        /// <returns></returns>
        [HttpPost(Name = "CreateComment")]
        public async Task<ActionResult> CreateCommentAsync(
            [FromForm] string reviewId,
            [FromForm] string elementId,
            [FromForm] string commentText,
            [FromForm] CommentType commentType,
            [FromForm] string apiRevisionId = null,
            [FromForm] string sampleRevisionId = null,
            [FromForm] CommentSeverity? severity = null,
            [FromForm] bool resolutionLocked = false,
            [FromForm] string threadId = null)
        {
            if (string.IsNullOrEmpty(commentText) || (string.IsNullOrEmpty(apiRevisionId) && string.IsNullOrEmpty(sampleRevisionId)))
            {
                return new BadRequestResult();
            }

            // Severity and resolutionLocked values are controlled by the frontend:
            // - New threads send the user-selected severity and resolution lock setting
            // - Replies send severity=null and resolutionLocked=false
            // The backend uses these values as-is without re-deriving reply status.
            var comment = new CommentItemModel
            {
                ReviewId = reviewId,
                APIRevisionId = apiRevisionId,
                SampleRevisionId = sampleRevisionId,
                ElementId = elementId,
                CommentText = commentText,
                ResolutionLocked = resolutionLocked,
                CreatedBy = User.GetGitHubLogin(),
                CreatedOn = DateTime.UtcNow,
                CommentType = commentType,
                Severity = severity,
                ThreadId = threadId 
            };

            bool isApiViewAgentTagged = AgentHelpers.IsApiViewAgentTagged(comment, out string commentTextWithIdentifiedTags);
            comment.CommentText = commentTextWithIdentifiedTags;
            await _commentsManager.AddCommentAsync(User, comment);

            var review = await _reviewManager.GetReviewAsync(User, reviewId);
            if (review != null)
            {
                await _notificationManager.SubscribeAsync(review, User);
            }

            if (isApiViewAgentTagged)
            {
                await _commentsManager.RequestAgentReply(User, comment, apiRevisionId);
            }

            return new LeanJsonResult(comment, StatusCodes.Status201Created, Url.Action("GetComments", "CommentsHybridAuth", new { reviewId = reviewId }));
        }

        /// <summary>
        /// Create a new Comment
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="commentId"></param>
        /// <param name="commentText"></param>
        /// <returns></returns>
        [HttpPatch("{reviewId}/{commentId}/updateCommentText", Name = "UpdateCommentText")]
        public async Task<ActionResult> UpdateCommentTextAsync(string reviewId, string commentId, [FromForm] string commentText)
        {
            await _commentsManager.UpdateCommentAsync(User, reviewId, commentId, commentText, new string[0]);
            return Ok();
        }

        /// <summary>
        /// Update comment severity
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="commentId"></param>
        /// <param name="severity"></param>
        /// <returns></returns>
        [HttpPatch("{reviewId}/{commentId}/updateCommentSeverity", Name = "UpdateCommentSeverity")]
        public async Task<ActionResult> UpdateCommentSeverityAsync(string reviewId, string commentId, [FromForm] CommentSeverity? severity)
        {
            await _commentsManager.UpdateCommentSeverityAsync(User, reviewId, commentId, severity);
            return Ok();
        }

        /// <summary>
        /// UnResolve comments in a comment thread
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="elementId"></param>
        /// <param name="threadId"></param>
        /// <returns></returns>
        [HttpPatch("{reviewId}/unResolveComments", Name = "UnResolveComments")]
        public async Task<ActionResult> UnResolveCommentsAsync(string reviewId, string elementId, string threadId = null)
        {
            await _commentsManager.UnresolveConversation(User, reviewId, elementId, threadId);
            return Ok();
        }

        /// <summary>
        /// Resolve multiple comment threads with optional voting and reply
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{reviewId}/commentsBatchOperation", Name = "CommentsBatchOperation")]
        public async Task<ActionResult> CommentsBatchOperation(string reviewId, [FromBody] BatchConversationRequest request)
        {
            List<CommentItemModel> createdComments = await _commentsManager.CommentsBatchOperationAsync(User, reviewId, request);
            return new LeanJsonResult(createdComments, StatusCodes.Status201Created);
        }

        /// <summary>
        /// Resolve comments in a comment thread
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="elementId"></param>
        /// <param name="threadId"></param>
        /// <returns></returns>
        [HttpPatch("{reviewId}/resolveComments", Name = "ResolveComments")]
        public async Task<ActionResult> ResolveCommentsAsync(string reviewId, string elementId, string threadId = null)
        {
            await _commentsManager.ResolveConversation(User, reviewId, elementId, threadId);
            return Ok();
        }

        /// <summary>
        /// Toggle comment upvote
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="commentId"></param>
        /// <returns></returns>
        [HttpPatch("{reviewId}/{commentId}/toggleCommentUpVote", Name = "ToggleCommentUpVote")]
        public async Task<ActionResult> ToggleCommentUpVoteAsync(string reviewId, string commentId)
        {
            await _commentsManager.ToggleUpvoteAsync(User, reviewId, commentId);
            return Ok();
        }

        /// <summary>
        /// Toggle comment downvote
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="commentId"></param>
        /// <returns></returns>
        [HttpPatch("{reviewId}/{commentId}/toggleCommentDownVote", Name = "ToggleCommentDownVote")]
        public async Task<ActionResult> ToggleDownUpVoteAsync(string reviewId, string commentId)
        {
            await _commentsManager.ToggleDownvoteAsync(User, reviewId, commentId);
            return Ok();
        }

        /// <summary>
        /// Submit feedback for comment
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="commentId"></param>
        /// <param name="feedback"></param>
        /// <returns></returns>
        [HttpPost("{reviewId}/{commentId}/feedback", Name = "SubmitCommentFeedback")]
        public async Task<ActionResult> SubmitCommentFeedbackAsync(string reviewId, string commentId, [FromBody] CommentFeedbackRequest feedback)
        {
            await _commentsManager.AddCommentFeedbackAsync(User, reviewId, commentId, feedback);
            return Ok();
        }


        /// <summary>
        /// Soft Delete a Comment
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="commentId"></param>
        /// <param name="elementId"></param>
        /// <returns></returns>
        [HttpDelete("{reviewId}/{commentId}", Name = "DeleteComments")]
        public async Task<ActionResult> DeleteCommentsAsync(string reviewId, string commentId, string elementId)
        {
            await _commentsManager.SoftDeleteCommentAsync(User, reviewId, commentId);
            return Ok();
        }

        /// <summary>
        /// Soft Delete all auto generated comments
        /// </summary>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        [HttpDelete("{apiRevisionId}/clearAutoComments", Name = "DeleteAutoGeneratedComments")]
        public async Task<ActionResult> DeleteAutoGeneratedComments(string apiRevisionId)
        {
            await _commentsManager.SoftDeleteAutoGeneratedCommentsAsync(User, apiRevisionId);
            return Ok();
        }
    }
}
