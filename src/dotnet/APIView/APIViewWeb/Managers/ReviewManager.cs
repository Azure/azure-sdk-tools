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
using APIView;
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
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<ReviewManager> _logger;

        public ReviewManager (
            IAuthorizationService authorizationService, ICosmosReviewRepository reviewsRepository,
            IAPIRevisionsManager apiRevisionsManager, ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository, ICosmosCommentsRepository commentsRepository, 
            ICosmosAPIRevisionsRepository apiRevisionsRepository,
            IHubContext<SignalRHub> signalRHubContext, IEnumerable<LanguageService> languageServices,
            TelemetryClient telemetryClient, ICodeFileManager codeFileManager, IConfiguration configuration, IHttpClientFactory httpClientFactory, IPollingJobQueueManager pollingJobQueueManager, INotificationManager notificationManager, ICosmosPullRequestsRepository pullRequestsRepository, ILogger<ReviewManager> logger)
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
            _logger = logger;
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
            var updatedReview = await ToggleReviewApproval(user, review, notes);
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
        /// <param name="revisionId"></param>
        /// <returns></returns>
        public async Task<ReviewListItemModel> RequestNamespaceReviewAsync(ClaimsPrincipal user, string reviewId, string revisionId)
        {
            if (string.IsNullOrEmpty(reviewId))
            {
                throw new ArgumentException("Review ID cannot be null or empty", nameof(reviewId));
            }

            var typeSpecReview = await _reviewsRepository.GetReviewAsync(reviewId);
            
            // Only allow for TypeSpec reviews
            if (typeSpecReview.Language != ApiViewConstants.TypeSpecLanguage)
            {
                throw new InvalidOperationException("Namespace review can only be requested for TypeSpec reviews");
            }

            var userId = user.GetGitHubLogin();
            var requestedOn = DateTime.UtcNow;
            var reviewGroupId = $"rg-{Guid.NewGuid():N}";
            

            // Get related reviews using pull request number from specific TypeSpec revision
            var relatedReviews = await FindRelatedReviewsByPullRequestAsync(reviewId, revisionId);

            // Update the reviews identified by review IDs with namespace approval fields
            await MarkAssociatedReviewsForNamespaceReview(relatedReviews, userId, requestedOn, reviewGroupId);

            var sdkLanguageReviews = relatedReviews
                                .Where(r => r != null && LanguageHelper.IsSDKLanguage(r.Language))
                                .ToList();

            // Send email notifications to preferred approvers with the actual language review data
            await _notificationManager.NotifyApproversOnNamespaceReviewRequest(user, typeSpecReview, sdkLanguageReviews);

            return typeSpecReview;
        }

        /// <summary>
        /// Update the associated language reviews with namespace approval fields
        /// This method takes review IDs and updates the review records only
        /// </summary>
        /// <param name="associatedReviews">List of reviews to update</param>
        /// <param name="userId">User requesting the namespace review</param>
        /// <param name="requestedOn">When the namespace approval was requested</param>
        /// <param name="reviewGroupId">Group ID to associate all related reviews</param>
        /// <returns></returns>
        private async Task MarkAssociatedReviewsForNamespaceReview(List<ReviewListItemModel> associatedReviews, string userId, DateTime requestedOn, string reviewGroupId)
        {
            try
            {
                foreach (var review in associatedReviews)
                {
                    try
                    {
                        if (review != null && LanguageHelper.IsSDKLanguageOrTypeSpec(review.Language) && review.NamespaceReviewStatus != NamespaceReviewStatus.Approved && !review.IsApproved)
                        {
                            review.ReviewGroupId = reviewGroupId;
                            review.NamespaceReviewStatus = NamespaceReviewStatus.Pending;
                            review.NamespaceApprovalRequestedBy = userId;
                            review.NamespaceApprovalRequestedOn = requestedOn;
                            
                            await _reviewsRepository.UpsertReviewAsync(review);
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
        /// Find related language reviews by looking up pull request number from specific TypeSpec revision
        /// </summary>
        /// <param name="typeSpecReviewId">The TypeSpec review ID</param>
        /// <param name="revisionId">The specific revision ID to get pull request from</param>
        /// <returns>List of related language reviews</returns>
        private async Task<List<ReviewListItemModel>> FindRelatedReviewsByPullRequestAsync(string typeSpecReviewId, string revisionId)
        {
            var relatedReviews = new List<ReviewListItemModel>();
            
            try
            {
                try
                {
                    // Look up pull requests directly by revision ID - much more reliable than extracting PR numbers
                    var pullRequests = await _pullRequestsRepository.GetPullRequestsAsync(typeSpecReviewId, revisionId);
                    // Get all review IDs from pull requests first to batch the database calls
                    var reviewIds = pullRequests
                        .Select(pr => pr.ReviewId)
                        .Distinct()
                        .ToList();

                    if (reviewIds.Count > 0)
                    {
                        relatedReviews = (List<ReviewListItemModel>)await _reviewsRepository.GetReviewsAsync(reviewIds);
                    }
                }
                catch (Exception ex)
                {
                    _telemetryClient.TrackException(ex);
                }

                return relatedReviews;
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                return new List<ReviewListItemModel>();
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
            List<ApiViewAgentComment> existingCommentInfo = AgentHelpers.BuildCommentsForAgent(reviewComments, activeCodeFile);
            var activeCodeLines = activeCodeFile.CodeFile.GetApiLines(skipDocs: true);
            var activeApiOutline = activeCodeFile.CodeFile.GetApiOutlineText();

            List<ApiViewAgentComment> diagnostics = new();
            if (activeCodeFile?.CodeFile?.Diagnostics?.Length > 0)
            {
                diagnostics = AgentHelpers.BuildDiagnosticsForAgent(activeCodeFile.CodeFile.Diagnostics.ToList(), activeCodeFile);
            }

            var copilotEndpoint = _configuration["CopilotServiceEndpoint"];
            var startUrl = $"{copilotEndpoint}/api-review/start";
            var client = _httpClientFactory.CreateClient();
            var payload = new Dictionary<string, object>
            {
                { "language", LanguageServiceHelpers.GetLanguageAliasForCopilotService(activeApiRevision.Language, activeCodeFile.CodeFile.LanguageVariant) },
                { "target", String.Join("\\n", activeCodeLines.Select(item => item.lineText.Trim())) },
                { "outline", activeApiOutline },
                { "comments", existingCommentInfo },
                { "diagnostics", diagnostics }
            };

            if (!String.IsNullOrEmpty(diffApiRevisionId))
            {
                var diffApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId: diffApiRevisionId);
                var diffCodeFile = await _codeFileRepository.GetCodeFileAsync(diffApiRevision, false);
                var diffCodeLines = diffCodeFile.CodeFile.GetApiLines(skipDocs: true);
                payload.Add("base", String.Join("\\n", diffCodeLines.Select(item => item.lineText.Trim())));
            }

            try {
                _logger.LogInformation("Starting Copilot job for ReviewId: {ReviewId}, APIRevisionId: {APIRevisionId}, Language: {Language}", 
                    reviewId, activeApiRevision.Id, activeApiRevision.Language);
                
                var response = await client.PostAsync(startUrl, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var jobStartedResponse = JsonSerializer.Deserialize<AIReviewJobStartedResponseModel>(responseString);
                
                _logger.LogInformation("Copilot job started successfully. JobId: {JobId}, ReviewId: {ReviewId}, APIRevisionId: {APIRevisionId}", 
                    jobStartedResponse.JobId, reviewId, activeApiRevision.Id);
                
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
                _logger.LogError(e, "Failed to start Copilot job for ReviewId: {ReviewId}, APIRevisionId: {APIRevisionId}", 
                    reviewId, activeApiRevision.Id);
                
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

        private async Task<ReviewListItemModel> ToggleReviewApproval(ClaimsPrincipal user, ReviewListItemModel review, string notes = "")
        {
            await ManagerHelpers.AssertApprover<ReviewListItemModel>(user, review, _authorizationService);
            var userId = user.GetGitHubLogin();
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, userId, notes);
            review.ChangeHistory = changeUpdate.ChangeHistory;
            review.IsApproved = changeUpdate.ChangeStatus;
            review.NamespaceReviewStatus = NamespaceReviewStatus.Approved;
            await _reviewsRepository.UpsertReviewAsync(review);
            
            // check if we should approve the TypeSpec namespace
            if (LanguageHelper.IsSDKLanguage(review.Language))
            {
                await CheckAndApproveNamespaceForTypeSpec(review);
            }
            
            return review;
        }

        /// <summary>
        /// Get all SDK reviews with a specific review group ID regardless of status
        /// </summary>
        /// <param name="reviewGroupId">The review group ID</param>
        /// <returns>List of all SDK reviews with the specified group ID</returns>
        private async Task<List<ReviewListItemModel>> GetReviewsWithGroupId(string reviewGroupId)
        {
            try
            {
                var sdkLanguageReviews = new List<ReviewListItemModel>();
                foreach (var language in ApiViewConstants.AllSupportedLanguages)
                {
                    var languageReviews = await _reviewsRepository.GetReviewsAsync(language: language, isClosed: null);
                    var matchingReviews = languageReviews.Where(r => 
                        r.ReviewGroupId == reviewGroupId && 
                        !r.IsDeleted);
                    sdkLanguageReviews.AddRange(matchingReviews);
                }

                return sdkLanguageReviews;
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                return new List<ReviewListItemModel>();
            }
        }

        /// <summary>
        /// Check if all related SDK reviews are approved and if so, approve namespace for TypeSpec review
        /// </summary>
        /// <param name="approvedReview">The review that was just approved</param>
        /// <returns></returns>
        private async Task CheckAndApproveNamespaceForTypeSpec(ReviewListItemModel approvedReview)
        {
            try
            {
                if (string.IsNullOrEmpty(approvedReview.ReviewGroupId))
                {
                    // Skip processing if no ReviewGroupId is available
                    return;
                }

                // Query database directly for all related reviews with the same GroupId
                var allReviews = await GetReviewsWithGroupId(approvedReview.ReviewGroupId);

                var pendingSdkReviews = allReviews.Where(r => r.ReviewGroupId == approvedReview.ReviewGroupId && LanguageHelper.IsSDKLanguage(r.Language) &&
                               !r.IsDeleted && 
                               !r.IsApproved).ToList();

                var typeSpecReview = allReviews.FirstOrDefault(r => 
                    r.NamespaceReviewStatus == NamespaceReviewStatus.Pending &&
                    r.Language == ApiViewConstants.TypeSpecLanguage &&
                    !r.IsDeleted);

                if (typeSpecReview == null){
                    return;
                }

                typeSpecReview.NamespaceReviewStatus = NamespaceReviewStatus.Approved;
                await _reviewsRepository.UpsertReviewAsync(typeSpecReview);

                // Send notification emails
                await _notificationManager.NotifyStakeholdersOfManualApproval(typeSpecReview, allReviews);
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
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
                        // Find the first revision that has original files and can be updated
                        APIRevisionListItemModel firstRevision = revisions.FirstOrDefault(r => 
                            r.Files.Any(f => f.HasOriginal) && 
                            languageService.CanUpdate(r.Files.First().VersionString));

                        if (firstRevision != null)
                        {
                            // Add to the batch for pipeline processing
                            eligibleReviews.Add(new APIRevisionGenerationPipelineParamModel
                            {
                                ReviewID = review.Id,
                                RevisionID = firstRevision.Id,
                                FileID = firstRevision.Files.First().FileId,
                                FileName = firstRevision.Files.First().FileName
                            });
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
        /// <param name="limit">Maximum number of results to return (default 100)</param>
        /// <returns>List of reviews that have pending namespace approval requests</returns>
        public async Task<List<ReviewListItemModel>> GetPendingNamespaceApprovalsBatchAsync(int limit = 100)
        {
            var pendingApprovals = new List<ReviewListItemModel>();

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    // Get all reviews that have namespace approval requests with single optimized query
                    var sdkLanguages = ApiViewConstants.SdkLanguages;
                    var allNamespaceReviews = await _reviewsRepository.GetPendingNamespaceApprovalReviewsAsync(sdkLanguages);

                    // Add all reviews up to the limit
                    pendingApprovals.AddRange(allNamespaceReviews.Take(limit));

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


        /*
        /// <summary>
        /// Process pending namespace reviews for auto-approval after 3 business days with no open comments
        /// Groups reviews by pull request number and sends one consolidated email per TypeSpec namespace
        /// TODO: Auto-approval feature is currently disabled - commenting out for future use
        /// </summary>
        public async Task ProcessPendingNamespaceAutoApprovals()
        {
            try
            {
                // Check if auto-approval feature is enabled
                var autoApprovalEnabled = _configuration.GetValue<bool>("Features:enableAutoApproval", false);
                if (!autoApprovalEnabled)
                {
                    _logger.LogInformation("Auto-approval feature is disabled. Skipping namespace auto-approval processing.");
                    return;
                }
                
                var pendingReviews = await GetPendingNamespaceReviewsForAutoApproval();
                
                // Group reviews by pull request numbers to consolidate related approvals
                var prGroups = GroupReviewsByPullRequestNumbers(pendingReviews);

                foreach (var prGroup in prGroups)
                {
                    var reviewsInGroup = prGroup.Value;
                    
                    // Find the TypeSpec review as the primary review for this group
                    var typeSpecReview = reviewsInGroup.FirstOrDefault(r => r.Language == ApiViewConstants.TypeSpecLanguage);
                    
                    // Check if any review in this group should be auto-approved
                    var shouldAutoApproveGroup = false;
                    foreach (var review in reviewsInGroup)
                    {
                        var shouldApprove = await ShouldAutoApprove(review);
                        if (shouldApprove)
                        {
                            shouldAutoApproveGroup = true;
                            break;
                        }
                    }     
                    if (shouldAutoApproveGroup)
                    {
                        _logger.LogInformation($"Auto-approving group '{prGroup.Key}' with {reviewsInGroup.Count} reviews");
                        await AutoApproveNamespaceGroup(reviewsInGroup, typeSpecReview);
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }
        */

        /// <summary>
        /// Get all reviews with pending namespace approval status
        /// </summary>
        private async Task<List<ReviewListItemModel>> GetPendingNamespaceReviewsForAutoApproval()
        {
            // Get all reviews (including TypeSpec) that have pending namespace approval
            var allLanguages = ApiViewConstants.AllSupportedLanguages;
            var pendingReviews = await _reviewsRepository.GetPendingNamespaceApprovalReviewsAsync(allLanguages);
            return pendingReviews.ToList();
        }

        /*
        /// <summary>
        /// Check if a review should be auto-approved (3 business days passed + no open comments)
        /// TODO: Auto-approval feature is currently disabled - commenting out for future use
        /// </summary>
        private async Task<bool> ShouldAutoApprove(ReviewListItemModel review)
        {
            if (!review.NamespaceApprovalRequestedOn.HasValue)
            {
                return false;
            }
            
            // Calculate 3 business day deadline (using centralized utility)
            var approvalDeadline = DateTimeHelper.CalculateBusinessDays(review.NamespaceApprovalRequestedOn.Value, 3);
            
            // Check if deadline has passed
            if (DateTime.UtcNow < approvalDeadline)
            {
                return false;
            }
            
            // Check for open/unresolved comments
            var openComments = await GetOpenComments(review.Id);
            var hasOpenComments = openComments.Any();
            
            var shouldApprove = !hasOpenComments;
            
            // Only log when actually approving to reduce noise
            if (shouldApprove)
            {
                _logger.LogInformation($"Auto-approving {review.PackageName} ({review.Language}) - deadline passed with no open comments");
            }
            
            return shouldApprove;
        }
        */

        // Get open/unresolved comments for a review
        // private async Task<IEnumerable<CommentItemModel>> GetOpenComments(string reviewId)
        // {
        //     var comments = await _commentManager.GetCommentsAsync(reviewId);
        //     return comments.Where(c => !c.IsResolved && !c.IsDeleted);
        // }

        // Group reviews by their timestamp and service to identify related reviews
        // Reviews with same NamespaceApprovalRequestedOn timestamp are likely from the same logical request
        // private Dictionary<string, List<ReviewListItemModel>> GroupReviewsByPullRequestNumbers(List<ReviewListItemModel> pendingReviews)
        // {
        //     var timestampGroups = new Dictionary<string, List<ReviewListItemModel>>();
        //     var reviewsWithoutTimestamp = new List<ReviewListItemModel>();

        //     // Group by NamespaceApprovalRequestedOn timestamp and service name
        //     foreach (var review in pendingReviews)
        //     {
        //         try
        //         {
        //             if (review.NamespaceApprovalRequestedOn.HasValue)
        //             {
        //                 // Extract service name from package name for grouping
        //                 var serviceName = StringHelper.ExtractServiceName(review.PackageName);
                        
        //                 // Create timestamp key with service name - precise to the minute to group related requests
        //                 var timestampKey = $"{serviceName}_{review.NamespaceApprovalRequestedOn.Value:yyyy-MM-dd_HH-mm}";
                        
        //                 if (!timestampGroups.ContainsKey(timestampKey))
        //                 {
        //                     timestampGroups[timestampKey] = new List<ReviewListItemModel>();
        //                 }
        //                 timestampGroups[timestampKey].Add(review);
        //             }
        //             else
        //             {
        //                 // No timestamp, handle individually
        //                 reviewsWithoutTimestamp.Add(review);
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             _telemetryClient.TrackException(ex);
        //             reviewsWithoutTimestamp.Add(review);
        //         }
        //     }

        //     // Add individual reviews for those without timestamps
        //     foreach (var review in reviewsWithoutTimestamp)
        //     {
        //         var individualKey = $"individual_{review.Id}";
        //         timestampGroups[individualKey] = new List<ReviewListItemModel> { review };
        //     }

        //     return timestampGroups;
        // }

        /*
        /// <summary>
        /// Auto-approve all reviews in a group and send one consolidated notification
        /// TODO: Auto-approval feature is currently disabled - commenting out for future use
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
                        .Where(r => r.Language != ApiViewConstants.TypeSpecLanguage && r.Language != ApiViewConstants.SwaggerLanguage)
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
        */
    }
}
