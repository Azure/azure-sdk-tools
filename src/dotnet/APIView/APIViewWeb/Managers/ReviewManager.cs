// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Data;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Helpers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;

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

        public ReviewManager (
            IAuthorizationService authorizationService, ICosmosReviewRepository reviewsRepository,
            IAPIRevisionsManager apiRevisionsManager, ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository, ICosmosCommentsRepository commentsRepository, 
            IHubContext<SignalRHub> signalRHubContext, IEnumerable<LanguageService> languageServices,
            TelemetryClient telemetryClient, ICodeFileManager codeFileManager)

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
                CreatedBy = "azure-sdk",
                IsClosed = isClosed,
                ChangeHistory = new List<ReviewChangeHistoryModel>()
                {
                    new ReviewChangeHistoryModel()
                    {
                        ChangeAction = ReviewChangeAction.Created,
                        ChangedBy = "azure-sdk",
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
        public async Task<int> GenerateAIReview(string reviewId, string revisionId)
        {
            var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);
            var revision = revisions.Where(r => r.Id == revisionId).FirstOrDefault();
            var codeFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
            var codeLines = codeFile.RenderText(false);

            var reviewText = new StringBuilder();
            foreach (var codeLine in codeLines)
            {
                reviewText.Append(codeLine.DisplayString);
                reviewText.Append("\\n");
            }

            var url = "https://apiview-gpt.azurewebsites.net/python";
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(20);
            var payload = new
            {
                content = reviewText.ToString()
            };

            var result = new AIReviewModel();
            try {
                var response = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var responseSanitized = JsonSerializer.Deserialize<string>(responseString);
                result = JsonSerializer.Deserialize<AIReviewModel>(responseSanitized);
            }
            catch (Exception e ) {
                throw new Exception($"Copilot Failed: {e.Message}");
            }
           
            // Write back result as comments to APIView
            foreach (var violation in result.Violations)
            {
                var codeLine = codeLines[violation.LineNo];
                var comment = new CommentItemModel();
                comment.CreatedOn = DateTime.UtcNow;
                comment.ReviewId = reviewId;
                comment.APIRevisionId = revisionId;
                comment.ElementId = codeLine.ElementId;
                //comment.SectionClass = sectionClass; // This will be needed for swagger

                var commentText = new StringBuilder();
                commentText.AppendLine($"Suggestion: `{violation.Suggestion}`");
                commentText.AppendLine();
                commentText.AppendLine(violation.Comment);
                foreach (var id in violation.RuleIds)
                {
                    commentText.AppendLine($"See: https://guidelinescollab.github.io/azure-sdk/{id}");
                }
                comment.ResolutionLocked = false;
                comment.CreatedBy = "azure-sdk";
                comment.CommentText = commentText.ToString();

                await _commentsRepository.UpsertCommentAsync(comment);
            }
            return result.Violations.Count;
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
