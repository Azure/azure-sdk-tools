// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Common;
using APIViewWeb.LeanModels;
using APIViewWeb.Helpers;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace APIViewWeb.Managers
{
    public class CommentsManager : ICommentsManager
    {
        private readonly IAuthorizationService _authorizationService;

        private readonly ICosmosCommentsRepository _commentsRepository;

        private readonly INotificationManager _notificationManager;

        private readonly OrganizationOptions _Options;

        public HashSet<GithubUser> TaggableUsers;

        public CommentsManager(
            IAuthorizationService authorizationService,
            ICosmosCommentsRepository commentsRepository,
            INotificationManager notificationManager,
            IOptions<OrganizationOptions> options)
        {
            _authorizationService = authorizationService;
            _commentsRepository = commentsRepository;
            _notificationManager = notificationManager;
            _Options = options.Value;

            TaggableUsers = new HashSet<GithubUser>();

            //Disable this to avoid exception when loading reviews for now.
            // Fetch users as a background task and populate it in cache.
            //LoadTaggableUsers();
        }

        public async void LoadTaggableUsers()
        {
            var c = new HttpClient();

            // UserAgent is required
            var userAgent = new ProductInfoHeaderValue("APIView", Startup.VersionHash);

            foreach (var requiredOrg in _Options.RequiredOrganization)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, string.Format("https://api.github.com/orgs/{0}/public_members?page={1}&per_page=100", requiredOrg, 1));
                req.Headers.UserAgent.Add(userAgent);

                var res = await c.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();
                var users = JsonConvert.DeserializeObject<GithubUser[]>(body);
                foreach (var user in users)
                {
                    TaggableUsers.Add(user);
                }
            }
            // Order users alphabetically
            TaggableUsers = new HashSet<GithubUser>(TaggableUsers.OrderBy(g => g.Login));
        }

        public async Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId)
        {
            return await _commentsRepository.GetCommentsAsync(reviewId);
        }

        public async Task<ReviewCommentsModel> GetReviewCommentsAsync(string reviewId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId);

            return new ReviewCommentsModel(reviewId, comments);
        }

        public async Task<ReviewCommentsModel> GetUsageSampleCommentsAsync(string reviewId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId);
            return new ReviewCommentsModel(reviewId, comments.Where(c => c.CommentType == LeanModels.CommentType.SampleRevision));
        }

        public async Task AddCommentAsync(ClaimsPrincipal user, CommentItemModel comment)
        {
            comment.ChangeHistory.Add(
                new CommentChangeHistoryModel()
                {
                    ChangeAction = CommentChangeAction.Created,
                    ChangedBy = user.GetGitHubLogin(),
                    ChangedOn = DateTime.Now,
                });
            comment.CreatedBy = user.GetGitHubLogin();
            comment.CreatedOn = DateTime.Now;

            await _commentsRepository.UpsertCommentAsync(comment);

            if (!comment.IsResolved)
            {
                await _notificationManager.NotifyUserOnCommentTag(comment);
                await _notificationManager.NotifySubscribersOnComment(user, comment);
            }
        }

        public async Task<CommentItemModel> UpdateCommentAsync(ClaimsPrincipal user, string reviewId, string commentId, string commentText, string[] taggedUsers)
        {
            var comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
            await AssertOwnerAsync(user, comment);
            comment.ChangeHistory.Add(
               new CommentChangeHistoryModel()
               {
                   ChangeAction = CommentChangeAction.Edited,
                   ChangedBy = user.GetGitHubLogin(),
                   ChangedOn = DateTime.Now,
               });
            comment.LastEditedOn = DateTime.Now;
            comment.CommentText = commentText;

            foreach (var taggedUser in taggedUsers)
            {
                if (!string.IsNullOrEmpty(taggedUser))
                {
                    comment.TaggedUsers.Add(taggedUser);
                }
            }

            await _commentsRepository.UpsertCommentAsync(comment);
            await _notificationManager.NotifyUserOnCommentTag(comment);
            await _notificationManager.NotifySubscribersOnComment(user, comment);
            return comment;
        }

        /// <summary>
        /// Delete Comment
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <returns></returns>
        public async Task SoftDeleteCommentsAsync(ClaimsPrincipal user, string reviewId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId);

            foreach (var  comment in comments)
            {
                await SoftDeleteCommentAsync(user, comment);
            }
        }

        /// <summary>
        /// Delete Comment
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <param name="commentId"></param>
        /// <returns></returns>
        public async Task SoftDeleteCommentAsync(ClaimsPrincipal user, string reviewId, string commentId)
        {
            var comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
            await SoftDeleteCommentAsync(user, comment);
        }

        /// <summary>
        ///  Delete Comment
        /// </summary>
        /// <param name="user"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        public async Task SoftDeleteCommentAsync(ClaimsPrincipal user, CommentItemModel comment)
        {
            await AssertOwnerAsync(user, comment);
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(comment.ChangeHistory, CommentChangeAction.Deleted, user.GetGitHubLogin());
            comment.ChangeHistory = changeUpdate.ChangeHistory;
            comment.IsDeleted = changeUpdate.ChangeStatus;
            await _commentsRepository.UpsertCommentAsync(comment);
        }

        public async Task ResolveConversation(ClaimsPrincipal user, string reviewId, string lineId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId, lineId);
            foreach (var comment in comments)
            {
                comment.ChangeHistory.Add(
                    new CommentChangeHistoryModel()
                    {
                        ChangeAction = CommentChangeAction.Resolved,
                        ChangedBy = user.GetGitHubLogin(),
                        ChangedOn = DateTime.Now,
                    });
                comment.IsResolved = true;
                await _commentsRepository.UpsertCommentAsync(comment);
            }
        }

        public async Task UnresolveConversation(ClaimsPrincipal user, string reviewId, string lineId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId, lineId);
            foreach (var comment in comments)
            {
                comment.ChangeHistory.Add(
                    new CommentChangeHistoryModel()
                    {
                        ChangeAction = CommentChangeAction.UnResolved,
                        ChangedBy = user.GetGitHubLogin(),
                        ChangedOn = DateTime.Now,
                    });
                comment.IsResolved = false;
                await _commentsRepository.UpsertCommentAsync(comment);
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

        public HashSet<GithubUser> GetTaggableUsers() => TaggableUsers;
        private async Task AssertOwnerAsync(ClaimsPrincipal user, CommentItemModel commentModel)
        {
            var result = await _authorizationService.AuthorizeAsync(user, commentModel, new[] { CommentOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }
    }
}
