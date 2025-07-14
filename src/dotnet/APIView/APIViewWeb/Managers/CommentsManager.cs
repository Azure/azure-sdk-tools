// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.DTOs;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<CommentsManager> _logger;

        public readonly UserProfileCache _userProfileCache;
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
            UserProfileCache userProfileCache,
            IConfiguration configuration,
            IOptions<OrganizationOptions> options,
            ILogger<CommentsManager> logger)
        {
            _apiRevisionsManager = apiRevisionsManager;
            _authorizationService = authorizationService;
            _commentsRepository = commentsRepository;
            _reviewRepository = reviewRepository;
            _notificationManager = notificationManager;
            _codeFileRepository = codeFileRepository;
            _signalRHubContext = signalRHubContext;
            _httpClientFactory = httpClientFactory;
            _userProfileCache = userProfileCache;
            _configuration = configuration;
            _Options = options.Value;
            _logger = logger;

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
            if (!IsUserAllowedToChatWithAgent(user, review))
            {
                await _signalRHubContext.Clients.Group(user.GetGitHubLogin()).SendAsync("ReceiveNotification",
                    new SiteNotificationDto()
                    {
                        ReviewId = comment.ReviewId,
                        RevisionId = comment.APIRevisionId,
                        Title = "Agent Interaction Restricted",
                        Summary = "You are not authorized to interact with the agent.",
                        Message =
                            "Interaction with the agent is restricted. Only designated language architects are authorized to use this feature",
                        Status = SiteNotificationStatus.Error
                    });

                return;
            }

            try
            {
                IEnumerable<CommentItemModel> threadComments =
                    await _commentsRepository.GetCommentsAsync(reviewId: comment.ReviewId, lineId: comment.ElementId);

                var activeApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId: activeRevisionId);
                var activeCodeFile = await _codeFileRepository.GetCodeFileAsync(activeApiRevision, false);
                List<ApiViewAgentComment> commentsForAgent =
                    AgentHelpers.BuildCommentsForAgent(threadComments, activeCodeFile);
                MentionRequest mentionRequest = new()
                {
                    Language = review.Language,
                    PackageName = activeApiRevision.PackageName,
                    Code = AgentHelpers.GetCodeLineForElement(activeCodeFile, comment.ElementId),
                    Comments = commentsForAgent
                };

                string agentMentionEndPoint = $"{_configuration["CopilotServiceEndpoint"]}/api-review/mention";
                var request = new HttpRequestMessage(HttpMethod.Post, agentMentionEndPoint)
                {
                    Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(mentionRequest),
                        Encoding.UTF8,
                        "application/json")
                };

                //TODO: See: https://github.com/Azure/azure-sdk-tools/issues/11128
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "dummy_token_value");

                var client = _httpClientFactory.CreateClient();
                var clientResponse = await client.SendAsync(request);
                clientResponse.EnsureSuccessStatusCode();
                string clientResponseContent = await clientResponse.Content.ReadAsStringAsync();
                AgentChatResponse agentChatResponse = JsonConvert.DeserializeObject<AgentChatResponse>(clientResponseContent);

                await AddAgentComment(comment, agentChatResponse?.Response);
            }
            catch (Exception ex)            
            {
                await _signalRHubContext.Clients.Group(user.GetGitHubLogin())
                    .SendAsync("ReceiveNotification",
                        new SiteNotificationDto()
                        {
                            ReviewId = comment.ReviewId,
                            RevisionId = comment.APIRevisionId,
                            Title = "Agent Service Unavailable",
                            Summary = "The agent service could not be reached.",
                            Message =
                                "We were unable to connect to the agent service at this time. Please try again later.",
                            Status = SiteNotificationStatus.Error
                        });

                _logger.LogError(ex, "Error while requesting agent reply for comment {CommentId} in review {ReviewId}", comment.Id, comment.ReviewId);
            }
        }

        private async Task AddAgentComment (CommentItemModel comment, string response)
        {
            var commentResult = new CommentItemModel
            {
                ReviewId = comment.ReviewId,
                APIRevisionId = comment.APIRevisionId,
                SampleRevisionId = comment.SampleRevisionId,
                ElementId = comment.ElementId,
                CommentText = response,
                ResolutionLocked = false,
                CreatedBy = ApiViewConstants.AzureSdkBotName,
                CreatedOn = DateTime.UtcNow,
                CommentType = CommentType.APIRevision
            };

            await _commentsRepository.UpsertCommentAsync(commentResult);
            await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                new CommentUpdatesDto()
                {
                    CommentThreadUpdateAction = CommentThreadUpdateAction.CommentCreated,
                    NodeId = commentResult.ElementId,
                    CommentId = commentResult.Id,
                    RevisionId = commentResult.APIRevisionId,
                    ReviewId = commentResult.ReviewId,
                    CommentText = commentResult.CommentText,
                    ElementId = commentResult.ElementId,
                    Comment = commentResult
                });
        }

        private bool IsUserAllowedToChatWithAgent(ClaimsPrincipal user, ReviewListItemModel review)
        {
            return IsUserLanguageArchitect(user, review);
        }

        private bool IsUserLanguageArchitect(ClaimsPrincipal user, ReviewListItemModel review)
        {
            if (user == null || review == null)
                return false;

            string githubUser = user.GetGitHubLogin();
            HashSet<string> approvers = PageModelHelpers.GetPreferredApprovers(_configuration, _userProfileCache, user, review);
            return approvers.Contains(githubUser);
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
