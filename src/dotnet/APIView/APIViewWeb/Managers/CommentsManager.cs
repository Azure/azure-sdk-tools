// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.DTOs;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APIViewWeb.Managers
{
    public class CommentsManager : ICommentsManager
    {
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IDiagnosticCommentService _diagnosticCommentService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICosmosCommentsRepository _commentsRepository;
        private readonly ICosmosReviewRepository _reviewRepository;
        private readonly INotificationManager _notificationManager;
        private readonly IConfiguration _configuration;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CommentsManager> _logger;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;
        private readonly ICopilotAuthenticationService _copilotAuthService;
        private readonly IPermissionsManager _permissionsManager;

        public readonly UserProfileCache _userProfileCache;
        private readonly OrganizationOptions _Options;

        public HashSet<GithubUser> TaggableUsers;

        public CommentsManager(IAPIRevisionsManager apiRevisionsManager,
            IDiagnosticCommentService diagnosticCommentService,
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
            IBackgroundTaskQueue backgroundTaskQueue,
            IPermissionsManager permissionsManager,
            ICopilotAuthenticationService copilotAuthService,
            ILogger<CommentsManager> logger)
        {
            _apiRevisionsManager = apiRevisionsManager;
            _diagnosticCommentService = diagnosticCommentService;
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
            _backgroundTaskQueue = backgroundTaskQueue;
            _copilotAuthService = copilotAuthService;
            _permissionsManager = permissionsManager;
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
                var users = JsonSerializer.Deserialize<GithubUser[]>(body) ?? [];
                foreach (var user in users)
                {
                    TaggableUsers.Add(user);
                }
            }
            // Order users alphabetically
            TaggableUsers = new HashSet<GithubUser>(TaggableUsers.OrderBy(g => g.Login));
        }
        
        public async Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId, bool isDeleted = false, CommentType? commentType = null, bool excludeDiagnostics = false)
        {
            IEnumerable<CommentItemModel> comments = await _commentsRepository.GetCommentsAsync(reviewId, isDeleted, commentType);
            
            if (excludeDiagnostics)
            {
                comments = comments.Where(c => c.CommentSource != CommentSource.Diagnostic);
            }
            
            return comments;
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
                await _notificationManager.NotifyUserOnCommentTagAsync(comment);
                await _notificationManager.NotifySubscribersOnCommentAsync(user, comment);
            }

            await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                new CommentUpdatesDto()
                {
                    CommentThreadUpdateAction = CommentThreadUpdateAction.CommentCreated,
                    CommentId = comment.Id,
                    ReviewId = comment.ReviewId,
                    ElementId = comment.ElementId,
                    NodeId = comment.ElementId,
                    CommentText = comment.CommentText,
                    Comment = comment,
                    ThreadId = comment.ThreadId
                });
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
            await _notificationManager.NotifyUserOnCommentTagAsync(comment);
            await _notificationManager.NotifySubscribersOnCommentAsync(user, comment);

            await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                new CommentUpdatesDto()
                {
                    CommentThreadUpdateAction = CommentThreadUpdateAction.CommentTextUpdate,
                    CommentId = comment.Id,
                    ReviewId = comment.ReviewId,
                    ElementId = comment.ElementId,
                    NodeId = comment.ElementId,
                    ThreadId = comment.ThreadId,
                    CommentText = comment.CommentText,
                    Severity = comment.Severity,
                });

            return comment;
        }

        public async Task<CommentItemModel> UpdateCommentSeverityAsync(ClaimsPrincipal user, string reviewId, string commentId, CommentSeverity? severity)
        {
            CommentItemModel comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);

            await AssertOwnerAsync(user, comment);
            
            comment.ChangeHistory.Add(
                new CommentChangeHistoryModel()
                {
                    ChangeAction = CommentChangeAction.Edited,
                    ChangedBy = user.GetGitHubLogin(),
                    ChangedOn = DateTime.Now,
                });
            comment.LastEditedOn = DateTime.Now;
            comment.Severity = severity;

            await _commentsRepository.UpsertCommentAsync(comment);
            return comment;
        }

        public async Task RequestAgentReply(ClaimsPrincipal user, CommentItemModel comment, string activeRevisionId)
        {
            ReviewListItemModel review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
            if (!await IsUserAllowedToChatWithAgentAsync(user, review))
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

            _backgroundTaskQueue.QueueBackgroundWorkItem(async cancellationToken =>
            {
                await ProcessAgentReplyRequest(user, comment, activeRevisionId, cancellationToken);
            });
        }

        private async Task ProcessAgentReplyRequest(ClaimsPrincipal user, CommentItemModel comment, string activeRevisionId, CancellationToken cancellationToken)
        {
            try
            {
                ReviewListItemModel review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
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

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _copilotAuthService.GetAccessTokenAsync(cancellationToken));

                var client = _httpClientFactory.CreateClient();
                var clientResponse = await client.SendAsync(request);
                clientResponse.EnsureSuccessStatusCode();
                string clientResponseContent = await clientResponse.Content.ReadAsStringAsync();
                AgentChatResponse agentChatResponse = JsonSerializer.Deserialize<AgentChatResponse>(clientResponseContent);
                if (agentChatResponse != null)
                {
                    await AddAgentComment(comment, agentChatResponse.Response);
                }
                
                _logger.LogInformation("Agent reply processed successfully for comment {CommentId} in review {ReviewId}", comment.Id, comment.ReviewId);
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

        private async Task AddAgentComment(CommentItemModel comment, string response)
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
                CommentSource = CommentSource.AIGenerated,
                ThreadId = comment.ThreadId,
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
                    Comment = commentResult,
                    ThreadId = commentResult.ThreadId
                });
        }

        private async Task<bool> IsUserAllowedToChatWithAgentAsync(ClaimsPrincipal user, ReviewListItemModel review)
        {
            string userId = user.GetGitHubLogin();
            EffectivePermissions permissions = await _permissionsManager.GetEffectivePermissionsAsync(userId);
            return permissions != null && permissions.IsApproverFor(review.Language);
        }

        /// <summary>
        /// Soft-delete all comments for a review (cascade delete).
        /// Skips per-comment owner checks — the caller is responsible for
        /// verifying that the user has permission to delete the parent review.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <returns></returns>
        public async Task SoftDeleteCommentsAsync(ClaimsPrincipal user, string reviewId)
        {
            var comments = await _commentsRepository.GetCommentsAsync(reviewId);
            var userName = user.GetGitHubLogin();

            foreach (var comment in comments)
            {
                var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(comment.ChangeHistory, CommentChangeAction.Deleted, userName);
                comment.ChangeHistory = changeUpdate.ChangeHistory;
                comment.IsDeleted = changeUpdate.ChangeStatus;
                await _commentsRepository.UpsertCommentAsync(comment);
            }
        }

        public async Task SoftDeleteAutoGeneratedCommentsAsync(ClaimsPrincipal user, string apiRevisionId)
        {
            var autoGeneratedComments = await GetAPIRevisionCommentsAsync(apiRevisionId: apiRevisionId, createdBy: "azure-sdk");
            var apiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId);

            // Bulk soft-delete: mark each comment as deleted in DB without individual SignalR notifications
            foreach (var comment in autoGeneratedComments)
            {
                await AssertOwnerAsync(user, comment);
                var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(comment.ChangeHistory, CommentChangeAction.Deleted, user.GetGitHubLogin());
                comment.ChangeHistory = changeUpdate.ChangeHistory;
                comment.IsDeleted = changeUpdate.ChangeStatus;
                await _commentsRepository.UpsertCommentAsync(comment);
            }

            apiRevision.HasAutoGeneratedComments = false;
            await _apiRevisionsManager.UpdateAPIRevisionAsync(apiRevision);

            // Send a single SignalR notification for the bulk delete
            if (autoGeneratedComments.Any())
            {
                await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                    new CommentUpdatesDto()
                    {
                        CommentThreadUpdateAction = CommentThreadUpdateAction.AutoGeneratedCommentsDeleted,
                        ReviewId = apiRevision.ReviewId
                    });
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

            await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                new CommentUpdatesDto()
                {
                    CommentThreadUpdateAction = CommentThreadUpdateAction.CommentDeleted,
                    CommentId = comment.Id,
                    ReviewId = comment.ReviewId,
                    ElementId = comment.ElementId,
                    NodeId = comment.ElementId,
                    ThreadId = comment.ThreadId
                });
        }

        public async Task ResolveConversation(ClaimsPrincipal user, string reviewId, string lineId, string threadId = null)
        {
            IEnumerable<CommentItemModel> comments = await _commentsRepository.GetCommentsAsync(reviewId, lineId);
            comments = comments.Where(c => c.ThreadId == threadId);
            
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

            if (comments.Any())
            {
                await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                    new CommentUpdatesDto()
                    {
                        CommentThreadUpdateAction = CommentThreadUpdateAction.CommentResolved,
                        ReviewId = reviewId,
                        ElementId = lineId,
                        NodeId = lineId,
                        ThreadId = threadId,
                        ResolvedBy = user.GetGitHubLogin()
                    });
            }
        }

        public async Task UnresolveConversation(ClaimsPrincipal user, string reviewId, string lineId, string threadId = null)
        {
            IEnumerable<CommentItemModel> comments = await _commentsRepository.GetCommentsAsync(reviewId, lineId);
            comments = comments.Where(c => c.ThreadId == threadId);
            
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

            if (comments.Any())
            {
                await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                    new CommentUpdatesDto()
                    {
                        CommentThreadUpdateAction = CommentThreadUpdateAction.CommentUnResolved,
                        ReviewId = reviewId,
                        ElementId = lineId,
                        NodeId = lineId,
                        ThreadId = threadId
                    });
            }
        }

        public async Task<List<CommentItemModel>> CommentsBatchOperationAsync(ClaimsPrincipal user, string reviewId, BatchConversationRequest request)
        {
            var response = new List<CommentItemModel>();
            
            foreach (string commentId in request.CommentIds)
            {
                CommentItemModel comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
                
                if (request.Feedback != null)
                {
                    await AddCommentFeedbackAsync(user, reviewId, commentId, request.Feedback);
                }
                
                if (request.Vote != FeedbackVote.None)
                {
                    await SetVoteAsync(user, reviewId, commentId, request.Vote);
                }

                if (!string.IsNullOrEmpty(request.CommentReply))
                {
                    var commentUpdate = new CommentItemModel
                    {
                        ReviewId = reviewId,
                        APIRevisionId = comment.APIRevisionId,
                        SampleRevisionId = comment.SampleRevisionId,
                        ElementId = comment.ElementId,
                        CommentText = request.CommentReply,
                        CreatedBy = user.GetGitHubLogin(),
                        CreatedOn = DateTime.UtcNow,
                        CommentType = comment.CommentType,
                        ThreadId = comment.ThreadId,
                        IsResolved = request.Disposition == ConversationDisposition.Resolve
                    };
                    await AddCommentAsync(user, commentUpdate);
                    response.Add(commentUpdate);
                }

                if (request.Severity.HasValue && request.Severity != comment.Severity)
                {
                    await UpdateCommentSeverityAsync(user, reviewId, commentId, request.Severity);
                    comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
                }

                switch (request.Disposition)
                {
                    case ConversationDisposition.Delete:
                        await SoftDeleteCommentAsync(user, reviewId, commentId);
                        break;
                    case ConversationDisposition.Resolve:
                        await ResolveConversation(user, reviewId, comment.ElementId, comment.ThreadId);
                        break;
                    case ConversationDisposition.KeepOpen:
                    default:
                        break;
                }
            }
            
            return response;
        }

        public async Task ToggleUpvoteAsync(ClaimsPrincipal user, string reviewId, string commentId)
        {
            CommentItemModel comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
            await ToggleVoteAsync(user, comment, FeedbackVote.Up);
        }

        public async Task ToggleDownvoteAsync(ClaimsPrincipal user, string reviewId, string commentId)
        {
            CommentItemModel comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);
            await ToggleVoteAsync(user, comment, FeedbackVote.Down);
        }

        public async Task AddCommentFeedbackAsync(ClaimsPrincipal user, string reviewId, string commentId, CommentFeedbackRequest feedback)
        {
            CommentItemModel comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);

            if (comment == null)
            {
                _logger.LogWarning($"Comment {commentId} not found for feedback submission");
                return;
            }

            string userName = user.GetGitHubLogin();
            comment.Feedback.Add(new CommentFeedback
            {
                Reasons = feedback.Reasons?.Select(r => r.ToString()).ToList() ?? [],
                Comment = feedback.Comment ?? string.Empty,
                IsDelete = feedback.IsDelete,
                SubmittedBy = userName,
                SubmittedOn = DateTime.UtcNow
            });

            await _commentsRepository.UpsertCommentAsync(comment);

            // Send feedback to Copilot if this is an AI-generated comment
            if (comment.CommentSource == CommentSource.AIGenerated && (feedback.Reasons?.Count > 0 || feedback.IsDelete))
            {
                _backgroundTaskQueue.QueueBackgroundWorkItem(async cancellationToken =>
                {
                    await SendFeedbackToCopilotAsync(user, comment, feedback, cancellationToken);
                });
            }
        }

        private async Task SendFeedbackToCopilotAsync(ClaimsPrincipal user, CommentItemModel comment, CommentFeedbackRequest feedback, CancellationToken cancellationToken)
        {
            try
            {
                ReviewListItemModel review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
                var activeApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId: comment.APIRevisionId);
                var activeCodeFile = await _codeFileRepository.GetCodeFileAsync(activeApiRevision, false);

                // Build feedback message from reasons
                var feedbackMessages = new List<string>();
                if (feedback.Reasons != null)
                {
                    feedbackMessages.AddRange(feedback.Reasons.Select(r => r.ToFeedbackMessage()));
                }

                if (feedback.IsDelete)
                {
                    feedbackMessages.Insert(0, "This comment was flagged for deletion by the user, which means it was so egregiously bad that they didn't even want the service team to see it.");
                }

                if (!string.IsNullOrEmpty(feedback.Comment))
                {
                    feedbackMessages.Add($"Additional feedback: {feedback.Comment}");
                }

                string feedbackText = $"@azure-sdk user '{user.GetGitHubLogin()}' has provided the following feedback on your previous comment:\n\n" +
                    string.Join("\n", feedbackMessages.Select(m => $"- {m}"));

                string codeLine = AgentHelpers.GetCodeLineForElement(activeCodeFile, comment.ElementId);

                // Create a synthetic comment representing the feedback
                var feedbackComment = new ApiViewAgentComment
                {
                    LineNumber = 0,
                    LineId = comment.ElementId,
                    LineText = codeLine,
                    CreatedOn = DateTimeOffset.UtcNow,
                    Upvotes = 0,
                    Downvotes = 0,
                    CreatedBy = user.GetGitHubLogin(),
                    CommentText = feedbackText,
                    IsResolved = false,
                    ThreadId = comment.ThreadId
                };

                // Include the original AI comment for context
                var originalAIComment = new ApiViewAgentComment
                {
                    LineNumber = 0,
                    LineId = comment.ElementId,
                    LineText = codeLine,
                    CreatedOn = comment.CreatedOn,
                    Upvotes = comment.Upvotes?.Count ?? 0,
                    Downvotes = comment.Downvotes?.Count ?? 0,
                    CreatedBy = comment.CreatedBy,
                    CommentText = comment.CommentText,
                    IsResolved = comment.IsResolved,
                    ThreadId = comment.ThreadId
                };

                MentionRequest mentionRequest = new()
                {
                    Language = review.Language,
                    PackageName = activeApiRevision.PackageName,
                    Code = codeLine,
                    Comments = new List<ApiViewAgentComment> { originalAIComment, feedbackComment }
                };

                string agentMentionEndPoint = $"{_configuration["CopilotServiceEndpoint"]}/api-review/mention";
                var request = new HttpRequestMessage(HttpMethod.Post, agentMentionEndPoint)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(mentionRequest),
                        Encoding.UTF8,
                        "application/json")
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _copilotAuthService.GetAccessTokenAsync(cancellationToken));

                var client = _httpClientFactory.CreateClient();
                var clientResponse = await client.SendAsync(request, cancellationToken);
                clientResponse.EnsureSuccessStatusCode();

                _logger.LogInformation("Feedback sent to Copilot for AI comment {CommentId} in review {ReviewId}", comment.Id, comment.ReviewId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending feedback to Copilot for AI comment {CommentId} in review {ReviewId}", comment.Id, comment.ReviewId);
            }
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

        private async Task ToggleVoteAsync(ClaimsPrincipal user, CommentItemModel comment, FeedbackVote voteType)
        {
            string userName = user.GetGitHubLogin();
            switch (voteType)
            {
                case FeedbackVote.Up:
                    if (comment.Upvotes.RemoveAll(u => u == userName) == 0)
                    {
                        comment.Upvotes.Add(userName);
                        comment.Downvotes.RemoveAll(u => u == userName);
                    }
                    break;
                case FeedbackVote.Down:
                    if (comment.Downvotes.RemoveAll(u => u == userName) == 0)
                    {
                        comment.Downvotes.Add(userName);
                        comment.Upvotes.RemoveAll(u => u == userName);
                    }
                    break;
                case FeedbackVote.None:
                default:
                    return;
            }

            await _commentsRepository.UpsertCommentAsync(comment);

            await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                new CommentUpdatesDto()
                {
                    CommentThreadUpdateAction = voteType == FeedbackVote.Up 
                        ? CommentThreadUpdateAction.CommentUpVoteToggled 
                        : CommentThreadUpdateAction.CommentDownVoteToggled,
                    CommentId = comment.Id,
                    ReviewId = comment.ReviewId,
                    ElementId = comment.ElementId,
                    NodeId = comment.ElementId,
                    Comment = comment,
                    ThreadId = comment.ThreadId
                });
        }

        private async Task SetVoteAsync(ClaimsPrincipal user, string reviewId, string commentId, FeedbackVote voteType)
        {
            CommentItemModel comment = await _commentsRepository.GetCommentAsync(reviewId, commentId);

            string userName = user.GetGitHubLogin();
            bool voteChanged = false;

            switch (voteType)
            {
                case FeedbackVote.Up:
                    if (!comment.Upvotes.Contains(userName))
                    {
                        comment.Upvotes.Add(userName);
                        comment.Downvotes.RemoveAll(u => u == userName); 
                        voteChanged = true;
                    }
                    break;
                case FeedbackVote.Down:
                    if (!comment.Downvotes.Contains(userName))
                    {
                        comment.Downvotes.Add(userName);
                        comment.Upvotes.RemoveAll(u => u == userName); 
                        voteChanged = true;
                    }
                    break;
                case FeedbackVote.None:
                default:
                    return;
            }

            if (voteChanged)
            {
                await _commentsRepository.UpsertCommentAsync(comment);
                await _signalRHubContext.Clients.All.SendAsync("ReceiveCommentUpdates",
                    new CommentUpdatesDto()
                    {
                        CommentThreadUpdateAction = voteType == FeedbackVote.Up 
                            ? CommentThreadUpdateAction.CommentUpVoteToggled 
                            : CommentThreadUpdateAction.CommentDownVoteToggled,
                        CommentId = comment.Id,
                        ReviewId = comment.ReviewId,
                        ElementId = comment.ElementId,
                        NodeId = comment.ElementId,
                        Comment = comment
                    });
            }
        }

        /// <summary>
        /// Synchronizes diagnostic comments for an API revision based on the current set of diagnostics.
        /// Creates new comments for new diagnostics, resolves comments for removed diagnostics,
        /// and updates existing comments when severity or help link changes.
        /// Uses hash-based caching to skip synchronization when diagnostics haven't changed.
        /// </summary>
        /// <param name="apiRevision">The API revision to sync diagnostics for.</param>
        /// <param name="diagnostics">The current set of diagnostics from the code file.</param>
        /// <param name="existingComments">Pre-fetched comments to avoid additional database calls. </param>
        /// <returns>A list of diagnostic comments for the API revision after synchronization.</returns>
        public async Task<List<CommentItemModel>> SyncDiagnosticCommentsAsync(
            APIRevisionListItemModel apiRevision,
            CodeDiagnostic[] diagnostics,
            IEnumerable<CommentItemModel> existingComments)
        {
            DiagnosticSyncResult result = await _diagnosticCommentService.SyncDiagnosticCommentsAsync(
                apiRevision.ReviewId,
                apiRevision.Id,
                apiRevision.DiagnosticsHash,
                diagnostics,
                existingComments);

            // Update the revision's hash if sync occurred
            if (result.WasSynced)
            {
                apiRevision.DiagnosticsHash = result.DiagnosticsHash;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(apiRevision);
            }

            return result.Comments;
        }
    }
}
