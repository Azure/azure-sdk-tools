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

        public ReviewManager (
            IAuthorizationService authorizationService, ICosmosReviewRepository reviewsRepository,
            IAPIRevisionsManager apiRevisionsManager, ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository, ICosmosCommentsRepository commentsRepository, 
            ICosmosAPIRevisionsRepository apiRevisionsRepository,
            IHubContext<SignalRHub> signalRHubContext, IEnumerable<LanguageService> languageServices,
            TelemetryClient telemetryClient, ICodeFileManager codeFileManager, IConfiguration configuration, IHttpClientFactory httpClientFactory, IPollingJobQueueManager pollingJobQueueManager, INotificationManager notificationManager)

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
        public async Task<ReviewListItemModel> RequestNamespaceReviewAsync(ClaimsPrincipal user, string reviewId,List<string> associatedReviewIds)
        {
            ReviewListItemModel typeSpecReview = await _reviewsRepository.GetReviewAsync(reviewId);
            
            // Only allow for TypeSpec reviews
            if (typeSpecReview.Language != "TypeSpec")
            {
                throw new InvalidOperationException("Namespace review can only be requested for TypeSpec reviews");
            }

            var userId = user.GetGitHubLogin();
            var requestId = Guid.NewGuid().ToString();
            var requestedOn = DateTime.UtcNow;
            
            // Mark the TypeSpec review as namespace review requested
            typeSpecReview.NamespaceReviewStatus = NamespaceReviewStatus.Pending;
            
            await _reviewsRepository.UpsertReviewAsync(typeSpecReview);

            // Send email notifications to preferred approvers
            await _notificationManager.NotifyApproversOnNamespaceReviewRequest(user, typeSpecReview);

            // Update the reviews identified by review IDs with namespace approval fields
            await MarkAssociatedReviewsForNamespaceReview(associatedReviewIds, userId, requestId, requestedOn);

            return typeSpecReview;
        }

        /// <summary>
        /// Update the associated language reviews with namespace approval fields
        /// This method takes review IDs and updates the review records only
        /// </summary>
        /// <param name="associatedReviewIds">List of review IDs to update</param>
        /// <param name="userId">User requesting the namespace review</param>
        /// <param name="requestId">Unique ID for this namespace approval request</param>
        /// <param name="requestedOn">When the namespace approval was requested</param>
        /// <returns></returns>
        private async Task MarkAssociatedReviewsForNamespaceReview(List<string> associatedReviewIds, string userId, string requestId, DateTime requestedOn)
        {
            try
            {
                var updatedCount = 0;

                // Update all provided associated reviews
                foreach (var reviewId in associatedReviewIds.Where(id => !string.IsNullOrEmpty(id)))
                {
                    try
                    {
                        // Get the review and update it with namespace approval fields
                        var review = await _reviewsRepository.GetReviewAsync(reviewId);
                        if (review != null && IsSDKLanguageOrTypeSpec(review.Language))
                        {
                            // Update the review record itself with namespace approval fields
                            review.IsNamespaceReviewRequested = true;
                            review.NamespaceApprovalRequestId = requestId;
                            review.NamespaceApprovalRequestedBy = userId;
                            review.NamespaceApprovalRequestedOn = requestedOn;
                            
                            // Update the review record in database
                            await _reviewsRepository.UpsertReviewAsync(review);
                            
                            updatedCount++;
                            _telemetryClient.TrackTrace($"[NAMESPACE] Updated review {reviewId} ({review.Language}) with namespace approval fields");
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackException(ex);
                        _telemetryClient.TrackTrace($"[NAMESPACE] Error updating review {reviewId}: {ex.Message}");
                        // Continue with other reviews
                    }
                }

                _telemetryClient.TrackTrace($"[NAMESPACE] Updated {updatedCount} reviews with namespace approval fields using provided review IDs");
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                _telemetryClient.TrackTrace($"[NAMESPACE] Error updating associated reviews: {ex.Message}");
                // Continue - don't fail the namespace request if this fails
            }
        }

        /// <summary>
        /// Update the provided API revision IDs with namespace approval fields
        /// This uses the revision IDs already identified by the frontend from Associated API Revisions
        /// </summary>
        /// <param name="activeApiRevisionId">Current API revision ID being viewed</param>
        /// <param name="associatedApiRevisionIds">List of associated API revision IDs from frontend</param>
        /// <param name="userId"></param>
        /// <param name="requestId">Unique ID for this namespace approval request</param>
        /// <param name="requestedOn">When the namespace approval was requested</param>
        /// <returns></returns>
        private async Task MarkProvidedAPIRevisionsForNamespaceReview(string activeApiRevisionId, List<string> associatedApiRevisionIds, string userId, string requestId, DateTime requestedOn)
        {
            try
            {
                var updatedCount = 0;

                // Always update the current active revision
                if (!string.IsNullOrEmpty(activeApiRevisionId))
                {
                    var currentRevision = await _apiRevisionsManager.GetAPIRevisionAsync(activeApiRevisionId);
                    if (currentRevision != null)
                    {
                        await UpdateSingleAPIRevisionWithNamespaceFields(currentRevision, requestId, userId, requestedOn);
                        updatedCount++;
                    }
                }

                // Update all provided associated API revisions
                foreach (var revisionId in associatedApiRevisionIds.Where(id => !string.IsNullOrEmpty(id) && id != activeApiRevisionId))
                {
                    try
                    {
                        var revision = await _apiRevisionsManager.GetAPIRevisionAsync(revisionId);
                        if (revision != null && IsSDKLanguageOrTypeSpec(revision.Language))
                        {
                            await UpdateSingleAPIRevisionWithNamespaceFields(revision, requestId, userId, requestedOn);
                            updatedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackException(ex);
                        _telemetryClient.TrackTrace($"[NAMESPACE] Error updating revision {revisionId}: {ex.Message}");
                        // Continue with other revisions
                    }
                }

                _telemetryClient.TrackTrace($"[NAMESPACE] Updated {updatedCount} API revisions with namespace approval fields using provided revision IDs");
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                _telemetryClient.TrackTrace($"[NAMESPACE] Error updating provided API revisions: {ex.Message}");
                // Continue - don't fail the namespace request if this fails
            }
        }

        /// <summary>
        /// Update a single API revision with namespace approval fields
        /// </summary>
        /// <param name="revision"></param>
        /// <param name="requestId"></param>
        /// <param name="userId"></param>
        /// <param name="requestedOn"></param>
        /// <returns></returns>
        private async Task UpdateSingleAPIRevisionWithNamespaceFields(APIRevisionListItemModel revision, string requestId, string userId, DateTime requestedOn)
        {
            try
            {
                revision.NamespaceApprovalRequestId = requestId;
                revision.NamespaceApprovalRequestedBy = userId;
                revision.IsNamespaceReviewRequested = true;
                
                await _apiRevisionsRepository.UpsertAPIRevisionAsync(revision);
                
                _telemetryClient.TrackTrace($"[NAMESPACE] Updated revision {revision.Id} ({revision.Language}) with namespace approval fields");
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                _telemetryClient.TrackTrace($"[NAMESPACE] Error updating revision {revision.Id}: {ex.Message}");
                // Continue with other revisions
            }
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
                _telemetryClient.TrackTrace("Running background task to verify review upgradability only.");
            }

            foreach (var language in LanguageService.SupportedLanguages)
            {
                if (updateDisabledLanguages.Contains(language))
                {
                    _telemetryClient.TrackTrace("Background task to update API review at startup is disabled for language " + language);
                    continue;
                }
                var languageService = LanguageServiceHelpers.GetLanguageService(language, _languageServices);
                if (languageService == null)
                    continue;

                // If review is updated using devops pipeline then batch process update review requests
                if (languageService.IsReviewGenByPipeline)
                {
                    _telemetryClient.TrackTrace($"{language} uses sandboxing pipeline to upgrade API revisions. Upgrade eligibility test is not yet supported for {language}.");
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
                _telemetryClient.TrackTrace($"Starting UpdateReviewsUsingPipeline for language: {language}");

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
                        _telemetryClient.TrackTrace($"Error processing review {review.Id} for pipeline update: {ex.Message}");
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

                    _telemetryClient.TrackTrace($"Processing {eligibleReviews.Count} eligible reviews in {batches.Count} batches for language: {language}");

                    foreach (var batch in batches)
                    {
                        try
                        {
                            // Use the same pipeline generation approach as API revisions
                            await _apiRevisionsManager.RunAPIRevisionGenerationPipeline(batch, language);
                            _telemetryClient.TrackTrace($"Successfully queued pipeline batch with {batch.Count} reviews for language: {language}");
                        }
                        catch (Exception ex)
                        {
                            _telemetryClient.TrackException(ex);
                            _telemetryClient.TrackTrace($"Failed to queue pipeline batch for language {language}: {ex.Message}");
                            // Continue with other batches
                        }
                    }
                }
                else
                {
                    _telemetryClient.TrackTrace($"No eligible reviews found for pipeline update for language: {language}");
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                _telemetryClient.TrackTrace($"Error in UpdateReviewsUsingPipeline for language {language}: {ex.Message}");
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
                            var apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(relatedReview.Id);
                            var namespacePendingRevisions = apiRevisions.Where(r => 
                                !string.IsNullOrEmpty(r.NamespaceApprovalRequestId) && 
                                (r.IsNamespaceReviewRequested ?? false) && 
                                !r.IsDeleted).ToList();
                            
                            relatedRevisions.AddRange(namespacePendingRevisions);
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
    }
}
