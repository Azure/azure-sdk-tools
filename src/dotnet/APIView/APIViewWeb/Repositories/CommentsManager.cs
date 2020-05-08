// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class CommentsManager
    {
        private readonly IAuthorizationService _authorizationService;

        private readonly CosmosCommentsRepository _commentsRepository;

        private readonly NotificationManager _notificationManager;

        public CommentsManager(
            IAuthorizationService authorizationService,
            CosmosCommentsRepository commentsRepository,
            NotificationManager notificationManager)
        {
            _authorizationService = authorizationService;
            _commentsRepository = commentsRepository;
            _notificationManager = notificationManager;
        }

        public async Task<ReviewCommentsModel> GetReviewCommentsAsync(string reviewId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId);

            return new ReviewCommentsModel(reviewId, comments);
        }

        public async Task AddCommentAsync(ClaimsPrincipal user, CommentModel comment)
        {
            comment.Username = user.GetGitHubLogin();
            comment.TimeStamp = DateTime.Now;

            await _commentsRepository.UpsertCommentAsync(comment);
            if (!comment.IsResolve)
            {
                await _notificationManager.NotifySubscribersOnComment(user, comment);
            }
        }

        public async Task<CommentModel> UpdateCommentAsync(ClaimsPrincipal user, string reviewId, string commentId, string commentText)
        {
            var comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
            await AssertOwnerAsync(user, comment);
            comment.EditedTimeStamp = DateTime.Now;
            comment.Comment = commentText;
            await _commentsRepository.UpsertCommentAsync(comment);
            await _notificationManager.NotifySubscribersOnComment(user, comment);
            return comment;
        }

        public async Task DeleteCommentAsync(ClaimsPrincipal user, string reviewId, string commentId)
        {
            var comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
            await AssertOwnerAsync(user, comment);
            await _commentsRepository.DeleteCommentAsync(comment);
        }

        public async Task ResolveConversation(ClaimsPrincipal user, string reviewId, string lineId)
        {
            await AddCommentAsync(user, new CommentModel()
            {
                IsResolve = true,
                ReviewId = reviewId,
                ElementId = lineId
            });
        }

        public async Task UnresolveConversation(ClaimsPrincipal user, string reviewId, string lineId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId, lineId);
            foreach (var comment in comments)
            {
                if (comment.IsResolve)
                {
                    await _commentsRepository.DeleteCommentAsync(comment);
                }
            }
        }

        public async Task ToggleUpvoteAsync(ClaimsPrincipal user, string reviewId, string commentId)
        {
            var comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);

            if (comment.Upvotes.RemoveAll(u => u == user.GetGitHubLogin()) == 0)
            {
                comment.Upvotes.Add(user.GetGitHubLogin());
            }

            await _commentsRepository.UpsertCommentAsync(comment);
        }
        private async Task AssertOwnerAsync(ClaimsPrincipal user, CommentModel commentModel)
        {
            var result = await _authorizationService.AuthorizeAsync(user, commentModel, new[] { CommentOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }
    }
}