// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly TelemetryClient _telemetryClient;
        private readonly ICodeFileManager _codeFileManager;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public ReviewManager (
            IAuthorizationService authorizationService, ICosmosReviewRepository reviewsRepository,
            IAPIRevisionsManager apiRevisionsManager, ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository, ICosmosCommentsRepository commentsRepository, 
            IHubContext<SignalRHub> signalRHubContext, IEnumerable<LanguageService> languageServices,
            TelemetryClient telemetryClient, ICodeFileManager codeFileManager, IConfiguration configuration, IHttpClientFactory httpClientFactory)

        {
            _authorizationService = authorizationService;
            _reviewsRepository = reviewsRepository;
            _apiRevisionsManager = apiRevisionsManager;
            _commentManager = commentManager;
            _codeFileRepository = codeFileRepository;
            _commentsRepository = commentsRepository;
            _signalRHubContext = signalRHubContext;
            _languageServices = languageServices;
            _telemetryClient = telemetryClient;
            _codeFileManager = codeFileManager;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
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
                CreatedBy = ApiViewConstants.BotName,
                IsClosed = isClosed,
                ChangeHistory = new List<ReviewChangeHistoryModel>()
                {
                    new ReviewChangeHistoryModel()
                    {
                        ChangeAction = ReviewChangeAction.Created,
                        ChangedBy = ApiViewConstants.BotName,
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
        public async Task<ReviewListItemModel> ToggleReviewApprovalAsync(ClaimsPrincipal user, string id, string revisionId, string notes="")
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
            await ToggleReviewApproval(user, review, notes);
        }

        /// <summary>
        /// Sends info to AI service for generating initial review on APIReview file
        /// </summary>
        public async Task<int> GenerateAIReview(ClaimsPrincipal user, string reviewId, string activeApiRevisionId, string diffApiRevisionId)
        {
            var activeApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId: activeApiRevisionId);
            var reviewComments = await _commentManager.GetCommentsAsync(reviewId: reviewId, commentType: CommentType.APIRevision);

            var activeCodeFile = await _codeFileRepository.GetCodeFileAsync(activeApiRevision, false);
            var activeCodeLines = activeCodeFile.CodeFile.GetApiLines(skipDocs: true);
            var activeApiOutline = activeCodeFile.CodeFile.GetApiOutlineText();
            List<CommentModelForCopilot> existingCommentInfo = new List<CommentModelForCopilot>();

            foreach (var comment in reviewComments)
            {
                if (activeCodeLines.Any(line => line.lineId == comment.ElementId))
                {
                    var associatedLine = activeCodeLines.FindIndex(line => line.lineId == comment.ElementId);
                    existingCommentInfo.Add(new CommentModelForCopilot
                    {
                        LineNumber = associatedLine + 1,
                        CommentText = comment.CommentText,
                        Author = comment.CreatedBy,
                    });
                }
            }

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

            var result = new AIReviewJobPolledResponseModel();
            try {
                var response = await client.PostAsync(startUrl, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var jobStartedResponse = JsonSerializer.Deserialize<AIReviewJobStartedResponseModel>(responseString);
                activeApiRevision.CopilotReviewInProgress = true;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(activeApiRevision);

                var poolUrl = $"{copilotEndpoint}/api-review/{jobStartedResponse.JobId}";
                var poller = new Poller();
                result = await poller.PollAsync<AIReviewJobPolledResponseModel>(
                    operation: async () => {
                        var response = await client.GetAsync(poolUrl);
                        response.EnsureSuccessStatusCode();
                        var pollResponseString = await response.Content.ReadAsStringAsync();
                        var pollResponse = JsonSerializer.Deserialize<AIReviewJobPolledResponseModel>(pollResponseString);
                        return pollResponse;
                    },
                    isComplete: response => (response.Status != "InProgress"),
                    initialInterval : 120, // Two minutes
                    maxInterval: 120
                );
                if (result.Status == "Error")
                {
                    throw new Exception(result.Details);
                }
            }
            catch (Exception e ) {
                activeApiRevision.CopilotReviewInProgress = false;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(activeApiRevision);
                throw new Exception($"Copilot Failed: {e.Message}");
            }
           
            // Write back result as comments to APIView
            foreach (var comment in result.Comments)
            {
                var codeLine = activeCodeLines[comment.LineNo - 1];
                var commentModel = new CommentItemModel();
                commentModel.CreatedOn = DateTime.UtcNow;
                commentModel.ReviewId = reviewId;
                commentModel.APIRevisionId = activeApiRevisionId;
                commentModel.ElementId = codeLine.lineId;
                //comment.SectionClass = sectionClass; // This will be needed for swagger
            
                var commentText = new StringBuilder();
                commentText.AppendLine(comment.Comment);
                commentText.AppendLine();
                commentText.AppendLine();
                if (!String.IsNullOrEmpty(comment.Suggestion))
                {
                    commentText.AppendLine($"Suggestion : `{comment.Suggestion}`");
                    commentText.AppendLine();
                    commentText.AppendLine();
                }
                foreach (var id in comment.RuleIds)
                {
                    commentText.AppendLine($"See: https://azure.github.io/azure-sdk/{id}");
                }
                commentModel.ResolutionLocked = false;
                commentModel.CreatedBy = ApiViewConstants.BotName;
                commentModel.CommentText = commentText.ToString();
            
                await _commentsRepository.UpsertCommentAsync(commentModel);
                activeApiRevision.HasAutoGeneratedComments = true;
            }
            activeApiRevision.CopilotReviewInProgress = false;
            await _apiRevisionsManager.UpdateAPIRevisionAsync(activeApiRevision);
            return result.Comments.Count;
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

        private async Task<ReviewListItemModel> ToggleReviewApproval(ClaimsPrincipal user, ReviewListItemModel review, string notes)
        {
            await ManagerHelpers.AssertApprover<ReviewListItemModel>(user, review, _authorizationService);
            var userId = user.GetGitHubLogin();
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, userId, notes);
            review.ChangeHistory = changeUpdate.ChangeHistory;
            review.IsApproved = changeUpdate.ChangeStatus;
            await _reviewsRepository.UpsertReviewAsync(review);
            return review;
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
            var reviews = await _reviewsRepository.GetReviewsAsync(language: language, isClosed: false);
            var paramList = new List<APIRevisionGenerationPipelineParamModel>();

            foreach (var review in reviews)
            {
                var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id);
                foreach (var revision in revisions)
                {
                    foreach (var file in revision.Files)
                    {
                        //Don't include current revision if file is not required to be updated.
                        // E.g. json token file is uploaded for a language, specific revision was already upgraded.
                        if (!file.HasOriginal || file.FileName == null || !languageService.IsSupportedFile(file.FileName) || !languageService.CanUpdate(file.VersionString))
                        {
                            continue;
                        }

                        _telemetryClient.TrackTrace($"Updating review: {review.Id}, revision: {revision.Id}");
                        paramList.Add(new APIRevisionGenerationPipelineParamModel()
                        {
                            FileID = file.FileId,
                            ReviewID = review.Id,
                            RevisionID = revision.Id,
                            FileName = Path.GetFileName(file.FileName)
                        });
                    }
                }

                // This should be changed to configurable batch count
                if (paramList.Count >= backgroundBatchProcessCount)
                {
                    _telemetryClient.TrackTrace($"Running pipeline to update reviews for {language} with batch size {paramList.Count}");
                    await _apiRevisionsManager.RunAPIRevisionGenerationPipeline(paramList, languageService.Name);
                    // Delay of 10 minute before starting next batch
                    // We should try to increase the number of revisions in the batch than number of runs.
                    await Task.Delay(600000);
                    paramList.Clear();
                }
            }

            if (paramList.Count > 0)
            {
                _telemetryClient.TrackTrace($"Running pipeline to update reviews for {language} with batch size {paramList.Count}");
                await _apiRevisionsManager.RunAPIRevisionGenerationPipeline(paramList, languageService.Name);
            }
        }
    }
}
