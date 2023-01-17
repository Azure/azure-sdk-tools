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

        public async Task<ReviewCommentsModel> GetReviewCommentsAsync(string reviewId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId);

            return new ReviewCommentsModel(reviewId, comments);
        }

        public async Task<ReviewCommentsModel> GetUsageSampleCommentsAsync(string reviewId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId);
            return new ReviewCommentsModel(reviewId, comments.Where((e) => e.IsUsageSampleComment));
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

        public async Task<CommentModel> UpdateCommentAsync(ClaimsPrincipal user, string reviewId, string commentId, string commentText, string[] taggedUsers)
        {
            var comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
            await AssertOwnerAsync(user, comment);
            comment.EditedTimeStamp = DateTime.Now;
            comment.Comment = commentText;
            comment.Username = user.GetGitHubLogin();

            var newTaggedUsers = new HashSet<string>();
            foreach (var taggedUser in taggedUsers)
            {
                if (!comment.TaggedUsers.Contains(taggedUser))
                {
                    await _notificationManager.NotifyUserOnCommentTag(taggedUser, comment);
                }
                newTaggedUsers.Add(taggedUser);
            }
            comment.TaggedUsers = newTaggedUsers;

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

        public HashSet<GithubUser> GetTaggableUsers() => TaggableUsers;
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
