// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Managers
{
    public class ReviewManager : IReviewManager
    {

        private readonly IAuthorizationService _authorizationService;
        private readonly ICosmosReviewRepository _reviewsRepository;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ICommentsManager _commentManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly ICosmosCommentsRepository _commentsRepository;
        private readonly ICosmosAPIRevisionsRepository _apiRevisionsRepository;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly TelemetryClient _telemetryClient;
        private readonly ICodeFileManager _codeFileManager;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPollingJobQueueManager _pollingJobQueueManager;
        private readonly INotificationManager _notificationManager;
        private readonly ICosmosPullRequestsRepository _pullRequestsRepository;

        public ReviewManager (
            IAuthorizationService authorizationService, ICosmosReviewRepository reviewsRepository,
            IAPIRevisionsManager apiRevisionsManager, ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository, ICosmosCommentsRepository commentsRepository, 
            ICosmosAPIRevisionsRepository apiRevisionsRepository,
            IHubContext<SignalRHub> signalRHubContext, IEnumerable<LanguageService> languageServices,
            TelemetryClient telemetryClient, ICodeFileManager codeFileManager, IConfiguration configuration, IHttpClientFactory httpClientFactory, IPollingJobQueueManager pollingJobQueueManager, INotificationManager notificationManager, ICosmosPullRequestsRepository pullRequestsRepository)

        {
            _authorizationService = authorizationService;
            _reviewsRepository = reviewsRepository;
            _apiRevisionsManager = apiRevisionsManager;
            _commentManager = commentManager;
            _codeFileRepository = codeFileRepository;
            _commentsRepository = commentsRepository;
            _apiRevisionsRepository = apiRevisionsRepository;
            _signalRHubContext = signalRHubContext;
            _languageServices = languageServices;
            _telemetryClient = telemetryClient;
            _codeFileManager = codeFileManager;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _pollingJobQueueManager = pollingJobQueueManager;
            _notificationManager = notificationManager;
            _pullRequestsRepository = pullRequestsRepository;
        }

        /// <summary>
        /// Get all Reviews for a language
        /// </summary>
        /// <param name="language"></param>
        /// <param name="isClosed"></param>
        /// <returns></returns>
        public Task<IEnumerable<ReviewListItemModel>> GetReviewsAsync(string language, bool? isClosed = false)
        {
            return _reviewsRepository.GetReviewsAsync(language, isClosed);
        }

        /// <summary>
        /// Get Reviews using language and package name
        /// </summary>
        /// <param name="language"></param>
        /// <param name="packageName"></param>
        /// <param name="isClosed"></param>
        /// <returns></returns>
        public Task<ReviewListItemModel> GetReviewAsync(string language, string packageName, bool? isClosed = false)
        {
            return _reviewsRepository.GetReviewAsync(language, packageName, isClosed);
        }

        /// <summary>
        /// Get List of Reviews for the Review Page
        /// </summary>
        /// <param name="search"></param>
        /// <param name="languages"></param>
        /// <param name="isClosed"></param>
        /// <param name="isApproved"></param>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <param name="orderBy"></param>
        /// <returns></returns>
        public async Task<(IEnumerable<ReviewListItemModel> Reviews, int TotalCount, int TotalPages, int CurrentPage, int? PreviousPage, int? NextPage)> GetPagedReviewListAsync(
            IEnumerable<string> search, IEnumerable<string> languages, bool? isClosed, bool? isApproved, int offset, int limit, string orderBy)
        {
            var result = await _reviewsRepository.GetReviewsAsync(search: search, languages: languages, isClosed: isClosed, isApproved:  isApproved, offset: offset, limit: limit, orderBy: orderBy);

            // Calculate and add Previous and Next and Current page to the returned result
            var totalPages = (int)Math.Ceiling(result.TotalCount / (double)limit);
            var currentPage = offset == 0 ? 1 : offset / limit + 1;

            (IEnumerable<ReviewListItemModel> Reviews, int TotalCount, int TotalPages, int CurrentPage, int? PreviousPage, int? NextPage) resultToReturn = (
                result.Reviews, result.TotalCount, TotalPages: totalPages,
                CurrentPage: currentPage,
                PreviousPage: currentPage == 1 ? null : currentPage - 1,
                NextPage: currentPage >= totalPages ? null : currentPage + 1
            );
            return resultToReturn;
        }

        /// <summary>
        /// Retrieve Reviews from the Reviews container in CosmosDb after applying filter to the query.
        /// Uses lean reviewListModels to reduce the size of the response. Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public async Task<PagedList<ReviewListItemModel>> GetReviewsAsync(PageParams pageParams, FilterAndSortParams filterAndSortParams)
        {
            return await _reviewsRepository.GetReviewsAsync(pageParams, filterAndSortParams);
        }

        /// <summary>
        /// Get Reviews
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public async Task<ReviewListItemModel> GetReviewAsync(ClaimsPrincipal user, string id)
        {
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var review = await _reviewsRepository.GetReviewAsync(id);
            return review;
        }

        /// <summary>
        ///  GEt Reviews using List of ReviewIds
        /// </summary>
        /// <param name="reviewIds"></param>
        /// <param name="isClosed"></param>
        /// <returns></returns>
        public async Task<IEnumerable<ReviewListItemModel>> GetReviewsAsync(IEnumerable<string> reviewIds, bool? isClosed = null)
        {
            if (reviewIds == null || !reviewIds.Any())
            {
                return new List<ReviewListItemModel>();
            }
            return await _reviewsRepository.GetReviewsAsync(reviewIds, isClosed);
        }

        /// <summary>
        /// Get Legacy Reviews from old database
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public async Task<LegacyReviewModel> GetLegacyReviewAsync(ClaimsPrincipal user, string id)
        {
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var review = await _reviewsRepository.GetLegacyReviewAsync(id);
            return review;
        }

        /// <summary>
        /// Get Review if it exist otherwise create it
        /// </summary>
        /// <param name="file"></param>
        /// <param name="filePath"></param>
        /// <param name="language"></param>
        /// <param name="runAnalysis"></param>
        /// <returns></returns>
        public async Task<ReviewListItemModel> GetOrCreateReview(IFormFile file, string filePath, string language, bool runAnalysis = false)
        {
            CodeFile codeFile = null;
            ReviewListItemModel review = null;

            using var memoryStream = new MemoryStream();
            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    codeFile = await _codeFileManager.CreateCodeFileAsync(
                        originalName: file?.FileName, fileStream: openReadStream, runAnalysis: runAnalysis, memoryStream: memoryStream, language: language);
                }
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                codeFile = await _codeFileManager.CreateCodeFileAsync(
                    originalName: filePath, runAnalysis: runAnalysis, memoryStream: memoryStream, language: language);
            }

            if (codeFile != null)
            {
                review = await GetReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language);
                if (review == null)
                {
                    review = await CreateReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language, isClosed: false);
                }
            }
            return review;
        }


        /// <summary>
        /// Create Reviews
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="language"></param>
        /// <param name="isClosed"></param>
        /// <returns></returns>
        public async Task<ReviewListItemModel> CreateReviewAsync(string packageName, string language, bool isClosed=true)
        {
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(language)) 
            {
                throw new ArgumentException("Package Name and Language are required");
            }

            ReviewListItemModel review = new ReviewListItemModel()
            {
                PackageName = packageName,
                Language = language,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = ApiViewConstants.AzureSdkBotName,
                IsClosed = isClosed,
                ChangeHistory = new List<ReviewChangeHistoryModel>()
                {
                    new ReviewChangeHistoryModel()
                    {
                        ChangeAction = ReviewChangeAction.Created,
                        ChangedBy = ApiViewConstants.AzureSdkBotName,
                        ChangedOn = DateTime.UtcNow
                    }
                }
            };

            await _reviewsRepository.UpsertReviewAsync(review);
            return review;
        }

        /// <summary>
        /// SoftDeleteReviewAsync
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task SoftDeleteReviewAsync(ClaimsPrincipal user, string id)
        {
            var review = await _reviewsRepository.GetReviewAsync(id);
            var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(id);
            await ManagerHelpers.AssertReviewOwnerAsync(user, review, _authorizationService);

            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(review.ChangeHistory, ReviewChangeAction.Deleted, user.GetGitHubLogin());
            review.ChangeHistory = changeUpdate.ChangeHistory;
            review.IsDeleted = changeUpdate.ChangeStatus;
            await _reviewsRepository.UpsertReviewAsync(review);

            foreach (var revision in revisions)
            {
                await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(user, revision);
            }
            await _commentManager.SoftDeleteCommentsAsync(user, review.Id);
        }

        /// <summary>
        /// Toggle Review Open/Closed state
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task ToggleReviewIsClosedAsync(ClaimsPrincipal user, string id)
        {
            var review = await _reviewsRepository.GetReviewAsync(id);
            var userId = user.GetGitHubLogin();
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Closed, userId);
            review.ChangeHistory = changeUpdate.ChangeHistory;
            review.IsClosed = changeUpdate.ChangeStatus;
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        /// <summary>
        /// Add new Approval or ApprovalReverted action to the ChangeHistory of a Review. Serves as firstRelease approval
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <param name="revisionId"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public async Task<ReviewListItemModel> ToggleReviewApprovalAsync(ClaimsPrincipal user, string id, string revisionId, string notes = "")
        {
            ReviewListItemModel review = await _reviewsRepository.GetReviewAsync(id);
            var userId = user.GetGitHubLogin();
            var updatedReview = await ToggleReviewApproval(user, review);
            await _signalRHubContext.Clients.Group(userId).SendAsync("ReceiveApprovalSelf", id, revisionId, review.IsApproved);
            await _signalRHubContext.Clients.All.SendAsync("ReceiveApproval", id, revisionId, userId, review.IsApproved);
            return updatedReview;
        }

        /// <summary>
        /// ApproveReviewAsync
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public async Task ApproveReviewAsync(ClaimsPrincipal user, string reviewId, string notes = "")
        {
            ReviewListItemModel review = await _reviewsRepository.GetReviewAsync(reviewId);
            if (review.IsApproved)
            {
                return;
            }
            await ToggleReviewApproval(user, review);
        }

        /// <summary>
        /// Request namespace review for TypeSpec and mark related SDK language reviews as namespace approval requested
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <param name="associatedReviewIds">List of review IDs for associated language reviews to update</param>
        /// <returns></returns>
        public async Task<ReviewListItemModel> RequestNamespaceReviewAsync(ClaimsPrincipal user, string reviewId, List<string> associatedReviewIds)
        {
            var typeSpecReview = await _reviewsRepository.GetReviewAsync(reviewId);
            
            // Only allow for TypeSpec reviews
            if (typeSpecReview.Language != "TypeSpec")
            {
                throw new InvalidOperationException("Namespace review can only be requested for TypeSpec reviews");
            }

            var userId = user.GetGitHubLogin();
            var requestedOn = DateTime.UtcNow;
            
            // Mark the TypeSpec review as namespace review requested
            typeSpecReview.NamespaceReviewStatus = NamespaceReviewStatus.Pending;
            typeSpecReview.NamespaceApprovalRequestedBy = userId;
            typeSpecReview.NamespaceApprovalRequestedOn = requestedOn;
            
            await _reviewsRepository.UpsertReviewAsync(typeSpecReview);

            // Auto-discover related reviews using pull request numbers from TypeSpec review
            var discoveredReviewIds = await FindRelatedReviewsByPullRequestAsync(reviewId);
            
            // Combine provided review IDs with auto-discovered ones
            var languageReviewIds = associatedReviewIds.Union(discoveredReviewIds).Distinct().ToList();
            
            // Update the reviews identified by review IDs with namespace approval fields
            await MarkAssociatedReviewsForNamespaceReview(languageReviewIds, userId, requestedOn);

            // Get the actual review records for email notification (after they've been updated)
            var languageReviews = await GetValidLanguageReviews(languageReviewIds);

            // Send email notifications to preferred approvers with the actual language review data
            await _notificationManager.NotifyApproversOnNamespaceReviewRequest(user, typeSpecReview, languageReviews);

            return typeSpecReview;
        }

        /// <summary>
        /// Get valid language reviews from review IDs
        /// </summary>
        private async Task<List<ReviewListItemModel>> GetValidLanguageReviews(List<string> reviewIds)
        {
            var languageReviews = new List<ReviewListItemModel>();
            
            foreach (var reviewId in reviewIds)
            {
                try
                {
                    var review = await _reviewsRepository.GetReviewAsync(reviewId);
                    if (review != null && IsSDKLanguageOrTypeSpec(review.Language) && review.Language != "TypeSpec")
                    {
                        languageReviews.Add(review);
                    }
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                    // Continue with other reviews
                }
            }
            
            return languageReviews;
        }

        /// <summary>
        /// Update the associated language reviews with namespace approval fields
        /// This method takes review IDs and updates the review records only
        /// </summary>
        /// <param name="associatedReviewIds">List of review IDs to update</param>
        /// <param name="userId">User requesting the namespace review</param>
        /// <param name="requestedOn">When the namespace approval was requested</param>
        /// <returns></returns>
        private async Task MarkAssociatedReviewsForNamespaceReview(List<string> associatedReviewIds, string userId, DateTime requestedOn)
        {
            try
            {
                var updatedCount = 0;

                foreach (var reviewId in associatedReviewIds.Where(id => !string.IsNullOrEmpty(id)))
                {
                    try
                    {
                        var review = await _reviewsRepository.GetReviewAsync(reviewId);
                        if (review != null && IsSDKLanguageOrTypeSpec(review.Language))
                        {
                            review.NamespaceReviewStatus = NamespaceReviewStatus.Pending;
                            review.NamespaceApprovalRequestedBy = userId;
                            review.NamespaceApprovalRequestedOn = requestedOn;
                            
                            await _reviewsRepository.UpsertReviewAsync(review);
                            updatedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackException(ex);
                        // Continue with other reviews
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                // Continue - don't fail the namespace request if this fails
            }
        }

        /// <summary>
        /// Update the provided API revision IDs with namespace approval fields
        /// NOTE: This is legacy - namespace approval should be tracked at review level only
        /// Keeping this for backward compatibility but consider deprecating
        /// </summary>
        /// <param name="activeApiRevisionId">Current API revision ID being viewed</param>
        /// <param name="associatedApiRevisionIds">List of associated API revision IDs from frontend</param>
        /// <param name="userId"></param>
        /// <param name="requestId">Unique ID for this namespace approval request</param>
        /// <param name="requestedOn">When the namespace approval was requested</param>
        /// <returns></returns>
        private async Task MarkProvidedAPIRevisionsForNamespaceReview(string activeApiRevisionId, List<string> associatedApiRevisionIds, string userId, string requestId, DateTime requestedOn)
        {
            // NOTE: Namespace approval is now tracked at review level only
            // This method is kept for backward compatibility but does nothing
            await Task.CompletedTask;
        }

        /// <summary>
        /// Update a single API revision with namespace approval fields
        /// NOTE: This is legacy - namespace approval should be tracked at review level only
        /// </summary>
        private async Task UpdateSingleAPIRevisionWithNamespaceFields(APIRevisionListItemModel revision, string requestId, string userId, DateTime requestedOn)
        {
            // NOTE: Namespace approval is now tracked at review level only
            // This method is kept for backward compatibility but does nothing
            await Task.CompletedTask;
        }

        /// <summary>
        /// Check if the language is TypeSpec or one of the 5 supported SDK languages
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        private bool IsSDKLanguageOrTypeSpec(string language)
        {
            var supportedLanguages = new[] { "TypeSpec", "C#", "Java", "Python", "Go", "JavaScript" };
            return supportedLanguages.Contains(language);
        }

        /// <summary>
        /// Find related language reviews by looking up pull request numbers from TypeSpec review
        /// </summary>
        /// <param name="typeSpecReviewId">The TypeSpec review ID</param>
        /// <returns>List of review IDs for related language reviews</returns>
        private async Task<List<string>> FindRelatedReviewsByPullRequestAsync(string typeSpecReviewId)
        {
            var relatedReviewIds = new List<string>();
            
            try
            {
                // Get all revisions for the TypeSpec review to find pull request numbers
                var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(typeSpecReviewId);
                var pullRequestNumbers = revisions
                    .Where(r => r.PullRequestNo.HasValue && r.PullRequestNo.Value > 0)
                    .Select(r => r.PullRequestNo.Value)
                    .Distinct()
                    .ToList();

                // For each pull request number, find related reviews
                foreach (var prNumber in pullRequestNumbers)
                {
                    try
                    {
                        // Look up pull requests in azure-rest-api-specs repository (most common for TypeSpec)
                        var pullRequests = await _pullRequestsRepository.GetPullRequestsAsync(prNumber, "azure-rest-api-specs");
                        
                        foreach (var pr in pullRequests)
                        {
                            if (!string.IsNullOrEmpty(pr.ReviewId) && pr.ReviewId != typeSpecReviewId)
                            {
                                // Verify this is a language review (not another TypeSpec review)
                                var review = await _reviewsRepository.GetReviewAsync(pr.ReviewId);
                                if (review != null && IsSDKLanguageOrTypeSpec(review.Language) && review.Language != "TypeSpec")
                                {
                                    relatedReviewIds.Add(pr.ReviewId);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackException(ex);
                        // Continue with other PRs
                    }
                }

                return relatedReviewIds.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// Sends info to AI service for generating initial review on APIReview file
        /// </summary>
        public async Task GenerateAIReview(ClaimsPrincipal user, string reviewId, string activeApiRevisionId, string diffApiRevisionId)
        {
            var activeApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId: activeApiRevisionId);
            var reviewComments = await _commentManager.GetCommentsAsync(reviewId: reviewId, commentType: CommentType.APIRevision);
            var activeCodeFile = await _codeFileRepository.GetCodeFileAsync(activeApiRevision, false);
            List<ApiViewAgentComment> existingCommentInfo = AgentHelpers.BuildCommentsForAgent(comments: reviewComments, codeFile: activeCodeFile);
            var activeCodeLines = activeCodeFile.CodeFile.GetApiLines(skipDocs: true);
            var activeApiOutline = activeCodeFile.CodeFile.GetApiOutlineText();

            var copilotEndpoint = _configuration["CopilotServiceEndpoint"];
            var startUrl = $"{copilotEndpoint}/api-review/start";
            var client = _httpClientFactory.CreateClient();
            var payload = new Dictionary<string, object>
            {
                { "language", LanguageServiceHelpers.GetLanguageAliasForCopilotService(activeApiRevision.Language) },
                { "target", String.Join("\\n", activeCodeLines.Select(item => item.lineText.Trim())) },
                { "outline", activeApiOutline },
                { "comments", existingCommentInfo }
            };

            if (!String.IsNullOrEmpty(diffApiRevisionId))
            {
                var diffApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId: diffApiRevisionId);
                var diffCodeFile = await _codeFileRepository.GetCodeFileAsync(diffApiRevision, false);
                var diffCodeLines = diffCodeFile.CodeFile.GetApiLines(skipDocs: true);
                payload.Add("base", String.Join("\\n", diffCodeLines.Select(item => item.lineText.Trim())));
            }

            try {
                var response = await client.PostAsync(startUrl, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var jobStartedResponse = JsonSerializer.Deserialize<AIReviewJobStartedResponseModel>(responseString);
                activeApiRevision.CopilotReviewJobId = jobStartedResponse.JobId;
                activeApiRevision.CopilotReviewInProgress = true;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(activeApiRevision);
                _pollingJobQueueManager.Enqueue(new AIReviewJobInfoModel()
                {
                    JobId = jobStartedResponse.JobId,
                    APIRevision = activeApiRevision,
                    CodeLines = activeCodeLines,
                    CreatedBy = user.GetGitHubLogin()
                });
            }
            catch (Exception e ) {
                activeApiRevision.CopilotReviewInProgress = false;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(activeApiRevision);
                throw new Exception($"Copilot Failed: {e.Message}");
            }
        }

        /// <summary>
        /// Logic to update Reviews in a blackground task
        /// </summary>
        /// <param name="updateDisabledLanguages"></param>
        /// <param name="backgroundBatchProcessCount"></param>
        /// <param name="verifyUpgradabilityOnly"></param>
        /// <param name="packageNameFilterForUpgrade"></param>
        /// <returns></returns>
        public async Task UpdateReviewsInBackground(HashSet<string> updateDisabledLanguages, int backgroundBatchProcessCount, bool verifyUpgradabilityOnly, string packageNameFilterForUpgrade = "")
        {
            // verifyUpgradabilityOnly is set when we need to run the upgrade in read only mode to recreate code files
            // But review code file or metadata in the DB will not be updated
            // This flag is set only to make sure revisions are upgradable to the latest version of the parser
            if(verifyUpgradabilityOnly)
            {
                // verifyUpgradabilityOnly is set when we need to run the upgrade in read only mode to recreate code files
                // But review code file or metadata in the DB will not be updated
                // This flag is set only to make sure revisions are upgradable to the latest version of the parser
            }

            foreach (var language in LanguageService.SupportedLanguages)
            {
                if (updateDisabledLanguages.Contains(language))
                {
                    continue;
                }
                var languageService = LanguageServiceHelpers.GetLanguageService(language, _languageServices);
                if (languageService == null)
                    continue;

                // If review is updated using devops pipeline then batch process update review requests
                if (languageService.IsReviewGenByPipeline)
                {
                    // Do not run sandboxing based upgrade during verify upgradability only mode
                    // This requires some changes in the pipeline to support this mode
                    if (!verifyUpgradabilityOnly)
                    {
                        await UpdateReviewsUsingPipeline(language, languageService, backgroundBatchProcessCount);
                    }                    
                }
                else
                {
                    var reviews = await _reviewsRepository.GetReviewsAsync(language: language, isClosed: false);
                    if (!string.IsNullOrEmpty(packageNameFilterForUpgrade))
                    {
                        reviews = reviews.Where(r => r.PackageName == packageNameFilterForUpgrade);
                    }
                    foreach (var review in reviews)
                    {
                        var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id);
                        foreach (var revision in revisions)
                        {
                            if (
                                revision.Files.First().HasOriginal &&
                                LanguageServiceHelpers.GetLanguageService(revision.Language, _languageServices)?.CanUpdate(revision.Files.First().VersionString) == true)
                            {
                                var requestTelemetry = new RequestTelemetry { Name = $"Updating {review.Language} Review with id: {review.Id}"  };
                                var operation = _telemetryClient.StartOperation(requestTelemetry);
                                try
                                {
                                    await Task.Delay(100);
                                    await _apiRevisionsManager.UpdateAPIRevisionAsync(revision, languageService, verifyUpgradabilityOnly);
                                }
                                catch (Exception e)
                                {
                                    _telemetryClient.TrackException(e);
                                }
                                finally
                                {
                                    _telemetryClient.StopOperation(operation);
                                }
                            }
                        }
                    }
                }
            }
        }

        private async Task<ReviewListItemModel> ToggleReviewApproval(ClaimsPrincipal user, ReviewListItemModel review)
        {
            await ManagerHelpers.AssertApprover<ReviewListItemModel>(user, review, _authorizationService);
            var userId = user.GetGitHubLogin();
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, userId, "");
            review.ChangeHistory = changeUpdate.ChangeHistory;
            review.IsApproved = changeUpdate.ChangeStatus;
            await _reviewsRepository.UpsertReviewAsync(review);
            
            // If this review was approved (first release), check if we should auto-approve namespace for related TypeSpec
            if (changeUpdate.ChangeStatus && IsSDKLanguage(review.Language))
            {
                await CheckAndAutoApproveNamespaceForTypeSpec(review.PackageName, userId);
            }
            
            return review;
        }

        /// <summary>
        /// Check if all related SDK reviews are approved and if so, automatically approve namespace for TypeSpec review
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        private async Task CheckAndAutoApproveNamespaceForTypeSpec(string packageName, string userId)
        {
            // Get the base package name (e.g., "contoso" from "contoso.widget")
            var packageBaseName = packageName?.Split('.').FirstOrDefault()?.ToLower();
            if (string.IsNullOrEmpty(packageBaseName))
                return;

            try
            {
                // Find the TypeSpec review for this package
                var typeSpecReview = await _reviewsRepository.GetReviewAsync("TypeSpec", packageName, false);
                if (typeSpecReview == null || typeSpecReview.IsDeleted || typeSpecReview.IsApproved)
                    return;

                // Check if all related SDK reviews are approved
                var allSDKReviewsApproved = await AreAllRelatedSDKReviewsApproved(packageBaseName);
                
                if (allSDKReviewsApproved)
                {
                    await _reviewsRepository.UpsertReviewAsync(typeSpecReview);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the original approval
                // This is a nice-to-have feature, not critical
                System.Diagnostics.Debug.WriteLine($"Error in auto-namespace approval: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if all related SDK reviews for a package are approved
        /// </summary>
        /// <param name="packageBaseName">Base package name (e.g., "contoso" from "contoso.widget")</param>
        /// <returns>True if all existing SDK reviews are approved</returns>
        private async Task<bool> AreAllRelatedSDKReviewsApproved(string packageBaseName)
        {
            // The 5 supported SDK languages
            var sdkLanguages = new[] { "C#", "Java", "Python", "Go", "JavaScript" };
            var foundReviews = new List<ReviewListItemModel>();

            foreach (var language in sdkLanguages)
            {
                try
                {
                    // Get all reviews for this language
                    var reviews = await _reviewsRepository.GetReviewsAsync(language: language, isClosed: false);
                    
                    // Find reviews that match the package base name
                    var matchingReviews = reviews.Where(r => 
                        !r.IsDeleted && 
                        r.PackageName.ToLower().StartsWith(packageBaseName))
                        .ToList();
                    
                    foundReviews.AddRange(matchingReviews);
                }
                catch (Exception)
                {
                    // Continue if we can't get reviews for this language
                    continue;
                }
            }

            // If no SDK reviews found, we can't auto-approve
            if (!foundReviews.Any())
                return false;

            // Check if ALL found SDK reviews are approved
            return foundReviews.All(review => review.IsApproved);
        }

        /// <summary>
        /// Check if the language is one of the 5 supported SDK languages
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        private bool IsSDKLanguage(string language)
        {
            var sdkLanguages = new[] { "C#", "Java", "Python", "Go", "JavaScript" };
            return sdkLanguages.Contains(language);
        }

        /// <summary>
        /// Languages that full support sandboxing updates reviews using Azure devops pipeline
        /// We should batch all eligible reviews to avoid a pipeline run storm
        /// </summary>
        /// <param name="language"></param>
        /// <param name="languageService"></param>
        /// <param name="backgroundBatchProcessCount"></param>
        /// <returns></returns>
        private async Task UpdateReviewsUsingPipeline(string language, LanguageService languageService, int backgroundBatchProcessCount)
        {
            try
            {
                // Get all non-closed reviews for the language
                var reviews = await _reviewsRepository.GetReviewsAsync(language: language, isClosed: false);
                
                var eligibleReviews = new List<APIRevisionGenerationPipelineParamModel>();
                
                foreach (var review in reviews)
                {
                    try
                    {
                        var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id);
                        foreach (var revision in revisions)
                        {
                            // Check if the revision has original files and can be updated
                            if (revision.Files.Any(f => f.HasOriginal) && 
                                languageService.CanUpdate(revision.Files.First().VersionString))
                            {
                                // Add to the batch for pipeline processing
                                eligibleReviews.Add(new APIRevisionGenerationPipelineParamModel
                                {
                                    ReviewID = review.Id,
                                    RevisionID = revision.Id,
                                    FileID = revision.Files.First().FileId,
                                    FileName = revision.Files.First().FileName
                                });

                                // Break to avoid multiple revisions per review in the same batch
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackException(ex);
                        // Continue with other reviews
                    }
                }

                if (eligibleReviews.Count > 0)
                {
                    // Batch the reviews to avoid pipeline run storm
                    var batchSize = Math.Max(1, backgroundBatchProcessCount);
                    var batches = eligibleReviews
                        .Select((review, index) => new { review, index })
                        .GroupBy(x => x.index / batchSize)
                        .Select(g => g.Select(x => x.review).ToList())
                        .ToList();

                    foreach (var batch in batches)
                    {
                        try
                        {
                            // Use the same pipeline generation approach as API revisions
                            await _apiRevisionsManager.RunAPIRevisionGenerationPipeline(batch, language);
                        }
                        catch (Exception ex)
                        {
                            _telemetryClient.TrackException(ex);
                            // Continue with other batches
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                // Don't rethrow - this shouldn't break the background update process
            }
        }

        /// <summary>
        /// Get all pending namespace approval requests in a single optimized database query
        /// This queries review records directly with database-level filtering for better performance
        /// </summary>
        /// <param name="user">Current user to check permissions</param>
        /// <param name="limit">Maximum number of results to return (default 100)</param>
        /// <returns>List of reviews that have pending namespace approval requests</returns>
        public async Task<List<ReviewListItemModel>> GetPendingNamespaceApprovalsBatchAsync(ClaimsPrincipal user, int limit = 100)
        {
            var userId = user.GetGitHubLogin();
            var pendingApprovals = new List<ReviewListItemModel>();

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    // Get all reviews that have namespace approval requests with single optimized query
                    var sdkLanguages = new[] { "C#", "Java", "Python", "Go", "JavaScript" };
                    var allNamespaceReviews = await _reviewsRepository.GetPendingNamespaceApprovalReviewsAsync(sdkLanguages);

                    // For each review, add it to the results
                    foreach (var review in allNamespaceReviews.Take(limit))
                    {
                        try
                        {
                            pendingApprovals.Add(review);
                        }
                        catch (Exception)
                        {
                            // Continue with other reviews
                        }
                    }

                    // Sort to show all reviews by request time
                    var sortedApprovals = pendingApprovals
                        .OrderByDescending(r => r.NamespaceApprovalRequestedOn)
                        .ToList();

                    return sortedApprovals;
                }
            }
            catch (OperationCanceledException)
            {
                // Return empty list on timeout to prevent infinite loading
                return new List<ReviewListItemModel>();
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                // Return empty list on error to be safe
                return new List<ReviewListItemModel>();
            }
        }

        /// <summary>
        /// Check and update namespace review status based on associated API revisions
        /// This should be called when loading a TypeSpec review page to check if all related SDK revisions are approved
        /// </summary>
        /// <param name="typeSpecReview">The TypeSpec review to check</param>
        /// <returns>Updated review with correct namespace status</returns>
        public async Task<ReviewListItemModel> CheckAndUpdateNamespaceReviewStatusAsync(ReviewListItemModel typeSpecReview)
        {
            // Only process TypeSpec reviews that have pending namespace review status
            if (typeSpecReview.Language != "TypeSpec" || typeSpecReview.NamespaceReviewStatus != NamespaceReviewStatus.Pending)
            {
                return typeSpecReview;
            }

            try
            {
                // Find all related SDK API revisions for this package
                var sdkLanguages = new[] { "C#", "Java", "Python", "Go", "JavaScript" };
                var relatedRevisions = new List<APIRevisionListItemModel>();

                foreach (var language in sdkLanguages)
                {
                    try
                    {
                        // Find review with the same package name in each SDK language
                        var relatedReview = await _reviewsRepository.GetReviewAsync(language, typeSpecReview.PackageName, false);
                        if (relatedReview != null && !relatedReview.IsDeleted)
                        {
                            // Get all API revisions for this review
                            // Since namespace approval is now tracked at review level, all revisions of this review are considered namespace-related
                            var apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(relatedReview.Id);
                            var activeRevisions = apiRevisions.Where(r => !r.IsDeleted).ToList();
                            
                            relatedRevisions.AddRange(activeRevisions);
                        }
                    }
                    catch (Exception)
                    {
                        // Continue if we can't find a review for this language
                        continue;
                    }
                }

                // Check if we have any related revisions and if all are approved
                if (relatedRevisions.Any() && relatedRevisions.All(r => r.IsApproved))
                {
                    // All related SDK revisions are approved, update status to Approved
                    typeSpecReview.NamespaceReviewStatus = NamespaceReviewStatus.Approved;
                    
                    await _reviewsRepository.UpsertReviewAsync(typeSpecReview);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the page load
                System.Diagnostics.Debug.WriteLine($"Error checking namespace review status: {ex.Message}");
            }

            return typeSpecReview;
        }

        /// <summary>
        /// Helper method to check if user can approve a review
        /// </summary>
        /// <param name="review">Review to check permissions for</param>
        /// <returns>True if user has permission to approve</returns>
        private bool CanUserApproveReview(ReviewListItemModel review)
        {
            // Add your business logic here for determining if user can approve
            // This might check user roles, permissions, etc.
            // For now, return true as placeholder - you should implement the actual logic
            return true;
        }

        /// <summary>
        /// Process pending namespace reviews for auto-approval after 1 business day with no open comments
        /// Groups reviews by pull request number and sends one consolidated email per TypeSpec namespace
        /// </summary>
        public async Task ProcessPendingNamespaceAutoApprovals()
        {
            try
            {
                var pendingReviews = await GetPendingNamespaceReviewsForAutoApproval();
                
                // Group reviews by pull request numbers to consolidate related approvals
                var prGroups = await GroupReviewsByPullRequestNumbers(pendingReviews);

                foreach (var prGroup in prGroups)
                {
                    var reviewsInGroup = prGroup.Value;
                    
                    // Find the TypeSpec review as the primary review for this group
                    var typeSpecReview = reviewsInGroup.FirstOrDefault(r => r.Language == "TypeSpec");
                    
                    // Check if any review in this group should be auto-approved
                    var shouldAutoApproveGroup = false;
                    foreach (var review in reviewsInGroup)
                    {
                        if (await ShouldAutoApprove(review))
                        {
                            shouldAutoApproveGroup = true;
                            break;
                        }
                    }
                    
                    if (shouldAutoApproveGroup)
                    {
                        await AutoApproveNamespaceGroup(reviewsInGroup, typeSpecReview);
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }

        /// <summary>
        /// Get all reviews with pending namespace approval status
        /// </summary>
        private async Task<List<ReviewListItemModel>> GetPendingNamespaceReviewsForAutoApproval()
        {
            // Get all reviews (including TypeSpec) that have pending namespace approval
            var allLanguages = new[] { "C#", "Java", "Python", "Go", "JavaScript", "TypeSpec" };
            var pendingReviews = await _reviewsRepository.GetPendingNamespaceApprovalReviewsAsync(allLanguages);
            return pendingReviews.ToList();
        }

        /// <summary>
        /// Check if a review should be auto-approved (1 business day passed + no open comments)
        /// </summary>
        private async Task<bool> ShouldAutoApprove(ReviewListItemModel review)
        {
            if (!review.NamespaceApprovalRequestedOn.HasValue) return false;
            
            // Calculate 1 business day deadline (same logic as EmailTemplateService)
            var approvalDeadline = CalculateBusinessDays(review.NamespaceApprovalRequestedOn.Value, 1);
            
            // Check if deadline has passed
            if (DateTime.UtcNow < approvalDeadline) return false;
            
            // Check for open/unresolved comments
            var openComments = await GetOpenComments(review.Id);
            return !openComments.Any();
        }

        /// <summary>
        /// Calculate business days from a start date (reusing EmailTemplateService logic)
        /// </summary>
        private DateTime CalculateBusinessDays(DateTime startDate, int businessDays)
        {
            var currentDate = startDate;
            var daysAdded = 0;

            while (daysAdded < businessDays)
            {
                currentDate = currentDate.AddDays(1);
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    daysAdded++;
                }
            }
            return currentDate;
        }

        /// <summary>
        /// Get open/unresolved comments for a review
        /// </summary>
        private async Task<IEnumerable<CommentItemModel>> GetOpenComments(string reviewId)
        {
            var comments = await _commentManager.GetCommentsAsync(reviewId);
            return comments.Where(c => !c.IsResolved && !c.IsDeleted);
        }

        /// <summary>
        /// Group reviews by their pull request numbers to identify related reviews
        /// </summary>
        private async Task<Dictionary<string, List<ReviewListItemModel>>> GroupReviewsByPullRequestNumbers(List<ReviewListItemModel> pendingReviews)
        {
            var prGroups = new Dictionary<string, List<ReviewListItemModel>>();
            var reviewsWithoutPR = new List<ReviewListItemModel>();

            foreach (var review in pendingReviews)
            {
                try
                {
                    // Get all API revisions for this review to find pull request numbers
                    var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id);
                    var pullRequestNumbers = revisions
                        .Where(r => r.PullRequestNo.HasValue && r.PullRequestNo.Value > 0)
                        .Select(r => r.PullRequestNo.Value.ToString())
                        .Distinct()
                        .ToList();

                    if (pullRequestNumbers.Any())
                    {
                        // Group by first PR number found (most reviews should have the same PR number)
                        var prKey = pullRequestNumbers.First();
                        
                        if (!prGroups.ContainsKey(prKey))
                        {
                            prGroups[prKey] = new List<ReviewListItemModel>();
                        }
                        prGroups[prKey].Add(review);
                    }
                    else
                    {
                        // No PR number found, handle separately
                        reviewsWithoutPR.Add(review);
                    }
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                    // Add to individual processing if we can't group it
                    reviewsWithoutPR.Add(review);
                }
            }

            // Handle reviews without PR numbers - use smarter grouping by service name
            var serviceGroups = GroupReviewsByServiceName(reviewsWithoutPR);

            foreach (var serviceGroup in serviceGroups)
            {
                var fallbackKey = $"service_{serviceGroup.Key}";
                prGroups[fallbackKey] = serviceGroup.Value;
            }

            return prGroups;
        }

        /// <summary>
        /// Group reviews by extracting service name from package names
        /// </summary>
        private Dictionary<string, List<ReviewListItemModel>> GroupReviewsByServiceName(List<ReviewListItemModel> reviews)
        {
            var serviceGroups = new Dictionary<string, List<ReviewListItemModel>>(StringComparer.OrdinalIgnoreCase);

            foreach (var review in reviews)
            {
                var serviceName = ExtractServiceNameFromPackage(review.PackageName);
                
                if (!serviceGroups.ContainsKey(serviceName))
                {
                    serviceGroups[serviceName] = new List<ReviewListItemModel>();
                }
                serviceGroups[serviceName].Add(review);
            }

            return serviceGroups;
        }

        /// <summary>
        /// Extract service name from package name to group related packages
        /// Examples:
        /// - Microsoft.MixedReality -> MixedReality
        /// - com.azure.resourcemanager:azure-resourcemanager-mixedreality -> MixedReality  
        /// - azure-mgmt-mixedreality -> MixedReality
        /// - @azure/arm-mixedreality -> MixedReality
        /// </summary>
        private string ExtractServiceNameFromPackage(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return "Unknown";

            var name = packageName.ToLowerInvariant();
            
            // Remove common prefixes and suffixes
            var patterns = new[]
            {
                @"microsoft\.",
                @"azure\.",
                @"com\.azure\.resourcemanager:",
                @"azure-resourcemanager-",
                @"azure-mgmt-",
                @"@azure/arm-",
                @"@azure/"
            };

            foreach (var pattern in patterns)
            {
                name = System.Text.RegularExpressions.Regex.Replace(name, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Extract the service name (first part before any dots, hyphens, or underscores after cleanup)
            var serviceName = name.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            
            // Capitalize first letter for consistency
            if (!string.IsNullOrEmpty(serviceName) && serviceName.Length > 0)
            {
                serviceName = char.ToUpperInvariant(serviceName[0]) + serviceName.Substring(1);
            }

            return serviceName ?? "Unknown";
        }

        /// <summary>
        /// Auto-approve all reviews in a group and send one consolidated notification
        /// </summary>
        private async Task AutoApproveNamespaceGroup(List<ReviewListItemModel> allReviewsInGroup, ReviewListItemModel typeSpecReview)
        {
            try
            {
                // Approve all reviews in this group
                foreach (var review in allReviewsInGroup)
                {
                    review.NamespaceReviewStatus = NamespaceReviewStatus.Approved;
                    await _reviewsRepository.UpsertReviewAsync(review);
                }
                
                // Send ONE consolidated email for the TypeSpec review with all associated language reviews
                if (typeSpecReview != null)
                {
                    // Get language reviews (exclude TypeSpec and Swagger from the associated list)
                    var associatedLanguageReviews = allReviewsInGroup
                        .Where(r => r.Language != "TypeSpec" && r.Language != "Swagger")
                        .ToList();
                    
                    await _notificationManager.NotifyStakeholdersOfAutoApproval(typeSpecReview, associatedLanguageReviews);
                }
                else
                {
                    // Fallback: if no TypeSpec review found, use the first review as primary
                    var primaryReview = allReviewsInGroup.First();
                    var otherReviews = allReviewsInGroup.Skip(1).ToList();
                    
                    await _notificationManager.NotifyStakeholdersOfAutoApproval(primaryReview, otherReviews);
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }

        /// <summary>
        /// Auto-approve all reviews for a package and send one consolidated notification
        /// </summary>
        private async Task AutoApproveNamespacePackage(string packageName, List<ReviewListItemModel> allReviewsInPackage, ReviewListItemModel typeSpecReview)
        {
            try
            {
                // Approve all reviews in this package
                foreach (var review in allReviewsInPackage)
                {
                    review.NamespaceReviewStatus = NamespaceReviewStatus.Approved;
                    await _reviewsRepository.UpsertReviewAsync(review);
                }
                
                // Send ONE consolidated email for the TypeSpec review with all associated language reviews
                if (typeSpecReview != null)
                {
                    // Get language reviews (exclude TypeSpec and Swagger from the associated list)
                    var associatedLanguageReviews = allReviewsInPackage
                        .Where(r => r.Language != "TypeSpec" && r.Language != "Swagger")
                        .ToList();
                    
                    await _notificationManager.NotifyStakeholdersOfAutoApproval(typeSpecReview, associatedLanguageReviews);
                }
                else
                {
                    // Fallback: if no TypeSpec review found, use the first review as primary
                    var primaryReview = allReviewsInPackage.First();
                    var otherReviews = allReviewsInPackage.Skip(1).ToList();
                    
                    await _notificationManager.NotifyStakeholdersOfAutoApproval(primaryReview, otherReviews);
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }

        /// <summary>
        /// Auto-approve a namespace review and all associated language reviews
        /// NOTE: This method is deprecated in favor of AutoApproveNamespacePackage for consolidated approvals
        /// </summary>
        private async Task AutoApproveNamespaceReview(ReviewListItemModel review)
        {
            // Update the main review to Approved
            review.NamespaceReviewStatus = NamespaceReviewStatus.Approved;
            await _reviewsRepository.UpsertReviewAsync(review);
            
            // Find and update associated language reviews to Approved as well
            var associatedReviews = await FindAssociatedLanguageReviews(review.Id);
            foreach (var assocReview in associatedReviews)
            {
                assocReview.NamespaceReviewStatus = NamespaceReviewStatus.Approved;
                await _reviewsRepository.UpsertReviewAsync(assocReview);
            }
            
            // Send notification about auto-approval
            await _notificationManager.NotifyStakeholdersOfAutoApproval(review, associatedReviews);
        }

        /// <summary>
        /// Find associated language reviews for a TypeSpec review (reusing existing logic)
        /// </summary>
        private async Task<List<ReviewListItemModel>> FindAssociatedLanguageReviews(string reviewId)
        {
            var associatedReviews = new List<ReviewListItemModel>();
            
            try
            {
                // Reuse existing logic to find related reviews
                var discoveredReviewIds = await FindRelatedReviewsByPullRequestAsync(reviewId);
                
                foreach (var relatedReviewId in discoveredReviewIds)
                {
                    var relatedReview = await _reviewsRepository.GetReviewAsync(relatedReviewId);
                    if (relatedReview != null && 
                        IsSDKLanguage(relatedReview.Language) && 
                        relatedReview.NamespaceReviewStatus == NamespaceReviewStatus.Pending)
                    {
                        associatedReviews.Add(relatedReview);
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
            
            return associatedReviews;
        }
    }
}
