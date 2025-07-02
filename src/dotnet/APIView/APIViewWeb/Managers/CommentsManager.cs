// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace APIViewWeb.Managers
{
    public class CommentsManager : ICommentsManager
    {
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICosmosCommentsRepository _commentsRepository;
        private readonly ICosmosReviewRepository _reviewRepository;
        private readonly INotificationManager _notificationManager;
        private readonly IConfiguration _configuration;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly OrganizationOptions _Options;

        public HashSet<GithubUser> TaggableUsers;

        public CommentsManager(IAPIRevisionsManager apiRevisionsManager,
            IAuthorizationService authorizationService,
            ICosmosCommentsRepository commentsRepository,
            ICosmosReviewRepository reviewRepository,
            INotificationManager notificationManager,
            IBlobCodeFileRepository codeFileRepository,
            IHubContext<SignalRHub> signalRHubContext,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IOptions<OrganizationOptions> options)
        {
            _apiRevisionsManager = apiRevisionsManager;
            _authorizationService = authorizationService;
            _commentsRepository = commentsRepository;
            _reviewRepository = reviewRepository;
            _notificationManager = notificationManager;
            _codeFileRepository = codeFileRepository;
            _signalRHubContext = signalRHubContext;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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

        private static readonly Regex azureSdkAgentTag =
            new Regex($@"(^|\s)@{Regex.Escape(ApiViewConstants.BotName)}\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public bool IsApiViewAgentTagged(CommentItemModel comment, out string commentTextWithIdentifiedTags)
        {
            bool isTagged = azureSdkAgentTag.IsMatch(comment.CommentText);

            commentTextWithIdentifiedTags = azureSdkAgentTag.Replace(
                comment.CommentText,
                m => $"{m.Groups[1].Value}**@{ApiViewConstants.BotName}**"
            );

            return isTagged;
        }
        
        public async Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId, bool isDeleted = false, CommentType? commentType = null)
        {
            return await _commentsRepository.GetCommentsAsync(reviewId, isDeleted, commentType);
        }

        public async Task<ReviewCommentsModel> GetReviewCommentsAsync(string reviewId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId);

            return new ReviewCommentsModel(reviewId, comments);
        }

        public async Task<IEnumerable<CommentItemModel>> GetAPIRevisionCommentsAsync(string apiRevisionId, string createdBy = null)
        {
            return await _commentsRepository.GetCommentsForAPIRevisionAsync(apiRevisionId: apiRevisionId, createdBy: createdBy);
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

        public async Task RequestAgentReply(ClaimsPrincipal user, CommentItemModel comment, string activeRevisionId)
        {
            ReviewListItemModel review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
            if (!IsUserAllowedToChatWithAgent(user, review.Language))
            {
                 var notification = new NotificationModel
                 {
                     Message =
                         "Interaction with the agent is restricted. Only designated language architects are authorized to use this feature",
                     Level = NotificatonLevel.Error
                 };

                 await _signalRHubContext.Clients.Group(user.GetGitHubLogin())
                     .SendAsync("RecieveNotification", notification);

                 return;
            }

            string response;
            try
            {
                IEnumerable<CommentItemModel> threadComments =
                    await _commentsRepository.GetCommentsAsync(reviewId: comment.ReviewId, lineId: comment.ElementId);

                var activeApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId: activeRevisionId);
                var activeCodeFile = await _codeFileRepository.GetCodeFileAsync(activeApiRevision, false);
                var activeCodeLines = activeCodeFile.CodeFile.GetApiLines(skipDocs: true);

                Dictionary<string, int> elementIdToLineNumber = activeCodeLines
                    .Select((elementId, lineNumber) => new { elementId.lineId, lineNumber })
                    .Where(x => !string.IsNullOrEmpty(x.lineId))
                    .ToDictionary(x => x.lineId, x => x.lineNumber + 1);

                List<ApiViewComment> commentsForAgent = threadComments
                    .Select(threadComment => new ApiViewComment
                    {
                        LineNumber = elementIdToLineNumber.TryGetValue(threadComment.ElementId, out int id) ? id : -1,
                        CreatedOn = threadComment.CreatedOn,
                        Upvotes = threadComment.Upvotes.Count,
                        Downvotes = threadComment.Downvotes.Count,
                        CreatedBy = threadComment.CreatedBy,
                        CommentText = threadComment.CommentText,
                        IsResolved = threadComment.IsResolved
                    })
                    .ToList();

                var client = _httpClientFactory.CreateClient();
                var payload = new Dictionary<string, object> { { "comments", commentsForAgent } };
                string agentMentionEndPoint = $"{_configuration["CopilotServiceEndpoint"]}/api-review/mention";
                var request = new HttpRequestMessage(HttpMethod.Post, agentMentionEndPoint)
                {
                    Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json")
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

                //var clientResponse = await client.SendAsync(request);
                //clientResponse.EnsureSuccessStatusCode();
                

                response = "Agent integration is not currently available.";
            }
            catch
            {
                var notification = new NotificationModel
                {
                    Message = "An error occurred while attempting to contact the agent service",
                    Level = NotificatonLevel.Error
                };

                await _signalRHubContext.Clients.Group(user.GetGitHubLogin())
                    .SendAsync("RecieveNotification", notification);
                return;
            }

            var commentResult = new CommentItemModel
            {
                ReviewId = comment.ReviewId,
                APIRevisionId = comment.APIRevisionId,
                SampleRevisionId = comment.SampleRevisionId,
                ElementId = comment.ElementId,
                CommentText = response,
                ResolutionLocked = false,
                CreatedBy = ApiViewConstants.BotName,
                CreatedOn = DateTime.UtcNow,
                CommentType = CommentType.SampleRevision
            };

            await _commentsRepository.UpsertCommentAsync(commentResult);
        }

        private bool IsUserAllowedToChatWithAgent(ClaimsPrincipal user, string language)
        {
            if (user == null || string.IsNullOrEmpty(language))
            {
                return false;
            }

            return IsUserLanguageArchitect(user, language);
        }

        private bool IsUserLanguageArchitect(ClaimsPrincipal user, string language)
        {
            string githubUser = user.GetGitHubLogin();

            if (string.IsNullOrEmpty(githubUser) || string.IsNullOrEmpty(language))
                return false;

            string architects = _configuration[$"Architects:{language}"] ?? "";
            HashSet<string> architectsSet = new(architects.Split(','), StringComparer.OrdinalIgnoreCase);

            return architectsSet.Contains(githubUser);
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

        public async Task SoftDeleteAutoGeneratedCommentsAsync(ClaimsPrincipal user, string apiRevisionId)
        {
            var autGeneratedComments = await GetAPIRevisionCommentsAsync(apiRevisionId: apiRevisionId, createdBy: "azure-sdk");
            var apiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId);
            foreach (var comment in autGeneratedComments)
            {
                await SoftDeleteCommentAsync(user, comment);
            }
            apiRevision.HasAutoGeneratedComments = false;
            await _apiRevisionsManager.UpdateAPIRevisionAsync(apiRevision);
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
                comment.Downvotes.RemoveAll(u => u == user.GetGitHubLogin());
            }

            await _commentsRepository.UpsertCommentAsync(comment);
        }

        public async Task ToggleDownvoteAsync(ClaimsPrincipal user, string reviewId, string commentId)
        {
            var comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);

            if (comment.Downvotes.RemoveAll(u => u == user.GetGitHubLogin()) == 0)
            {
                comment.Downvotes.Add(user.GetGitHubLogin());
                comment.Upvotes.RemoveAll(u => u == user.GetGitHubLogin());
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
