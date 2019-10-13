﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class CommentsManager
    {
        private readonly IAuthorizationService _authorizationService;

        private readonly CosmosCommentsRepository _commentsRepository;

        public CommentsManager(
            IAuthorizationService authorizationService,
            CosmosCommentsRepository commentsRepository)
        {
            _authorizationService = authorizationService;
            _commentsRepository = commentsRepository;
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