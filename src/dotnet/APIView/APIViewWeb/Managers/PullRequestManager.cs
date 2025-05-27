// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace APIViewWeb.Managers
{
    public class PullRequestManager : IPullRequestManager
    {
        static readonly GitHubClient _githubClient = new GitHubClient(new ProductHeaderValue("apiview"));
        
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<PullRequestManager> _logger;
        private readonly ICosmosPullRequestsRepository _pullRequestsRepository;
        private readonly ICosmosAPIRevisionsRepository _apiRevisionsRepository;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ICodeFileManager _codeFileManager;
        private readonly IConfiguration _configuration;
        private readonly int _pullRequestCleanupDays;
        private readonly bool _isGitClientAvailable;

        public PullRequestManager(
            ICosmosPullRequestsRepository pullRequestsRepository, ICosmosAPIRevisionsRepository apiRevisionsRepository,
            IAPIRevisionsManager apiRevisionsManager, IConfiguration configuration, ICodeFileManager codeFileManager,
            IReviewManager reviewManager, TelemetryClient telemetryClient, ILogger<PullRequestManager> logger)
        {
            _pullRequestsRepository = pullRequestsRepository;
            _apiRevisionsRepository = apiRevisionsRepository;
            _reviewManager = reviewManager;
            _apiRevisionsManager = apiRevisionsManager;
            _configuration = configuration;
            _codeFileManager = codeFileManager;
            _telemetryClient = telemetryClient;
            _logger = logger;

            var ghToken = _configuration["github-access-token"];
            if (!string.IsNullOrEmpty(ghToken))
            {
                _githubClient.Credentials = new Credentials(ghToken);
                _isGitClientAvailable = true;
            }
            var pullRequestReviewCloseAfter = _configuration["pull-request-review-close-after-days"] ?? "30";
            _pullRequestCleanupDays = int.Parse(pullRequestReviewCloseAfter);
        }
        public async Task UpsertPullRequestAsync(PullRequestModel pullRequestModel)
        {
            await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(string reviewId, string apiRevisionId = null) {
            return await _pullRequestsRepository.GetPullRequestsAsync(reviewId, apiRevisionId);
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(int pullRequestNumber, string repoName)
        {
            return await _pullRequestsRepository.GetPullRequestsAsync(pullRequestNumber, repoName);
        }

        public async Task<PullRequestModel> GetPullRequestModelAsync(int prNumber, string repoName, string packageName, string originalFile, string language)
        {
            var pullRequestModel = await _pullRequestsRepository.GetPullRequestAsync(prNumber, repoName, packageName, language);
            if (pullRequestModel == null)
            {
                var repoInfo = repoName.Split("/");
                var pullRequest = await _githubClient.PullRequest.Get(repoInfo[0], repoInfo[1], prNumber);
                pullRequestModel = new PullRequestModel()
                {
                    RepoName = repoName,
                    PullRequestNumber = prNumber,
                    FilePath = originalFile,
                    CreatedBy = pullRequest.User.Login,
                    PackageName = packageName,
                    Language = language,
                    Assignee = pullRequest.Assignee?.Login
                };
            }
            return pullRequestModel;
        }

        public async Task CleanupPullRequestData()
        {
            var telemetry = new RequestTelemetry { Name = "Cleaning up Reviews created for pull requests" };
            var operation = _telemetryClient.StartOperation(telemetry);
            try
            {
                var pullRequests = await _pullRequestsRepository.GetPullRequestsAsync(true);
                foreach (var prModel in pullRequests)
                {
                    try
                    {
                        if (prModel.APIRevisionId != null && await IsPullRequestEligibleForCleanup(prModel))
                        {
                            _telemetryClient.TrackEvent($"Closing revision {prModel.ReviewId}/{prModel.APIRevisionId} created for pull request {prModel.PullRequestNumber}");
                            await ClosePullRequestAPIRevision(prModel);
                        }
                        // Wait 10 seconds before processing next record.
                        await Task.Delay(10000);
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackEvent("Failed to close review " + prModel.ReviewId);
                        _telemetryClient.TrackException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
            finally
            {
                _telemetryClient.StopOperation(operation);
            }
        }

        public async Task<string> CreateAPIRevisionIfAPIHasChanges(
            string buildId, string artifactName, string originalFileName, string commitSha, string repoName,
            string packageName, int prNumber, string hostName, CreateAPIRevisionAPIResponse responseContent,
            string codeFileName = null, string baselineCodeFileName = null, string language = null,
            string project = "internal")
        {
            language = LanguageServiceHelpers.MapLanguageAlias(language: language);
            originalFileName = originalFileName ?? codeFileName;

            // Get Code File to find the actual package name emitted by parser
            // We should deprecate package name param and use PackageName in CodeFile
            using var memoryStream = new MemoryStream();
            using var baselineStream = new MemoryStream();
            var codeFile = await _codeFileManager.GetCodeFileAsync(
                repoName: repoName, buildId: buildId, artifactName: artifactName,
                packageName: packageName, originalFileName: originalFileName,
                codeFileName: codeFileName, originalFileStream: memoryStream,
                baselineCodeFileName: baselineCodeFileName, baselineStream: baselineStream,
                project: project, language: language);

            if (codeFile.PackageName != null && (packageName == null || packageName != codeFile.PackageName))
            {
                packageName = codeFile.PackageName;
            }

            var repoInfo = repoName.Split("/");
            var pullRequestModel = await GetPullRequestModelAsync(prNumber, repoName, packageName, originalFileName, language);
            if (pullRequestModel == null)
            {
                return "";
            }
            if (pullRequestModel.Commits.Any(c => c == commitSha))
            {
                // PR commit is already processed. No need to reprocess it again.
                responseContent.ActionsTaken.Add("CommitSha for this request has been previously processed.");
                return !string.IsNullOrEmpty(pullRequestModel.ReviewId) ? ManagerHelpers.ResolveReviewUrl(pullRequest: pullRequestModel, hostName: hostName) : "";
            }

            pullRequestModel.Commits.Add(commitSha);
            responseContent.ActionsTaken.Add($"Pull Request data for PR updated with new CommitSha '{commitSha}'");

            try
            {
                CodeFile baseLineCodeFile = null;
                if (baselineStream.Length > 0)
                {
                    baselineStream.Position = 0;
                    baseLineCodeFile = await CodeFile.DeserializeAsync(stream: baselineStream);
                }
                if (codeFile != null)
                {
                    await CreateAPIRevisionIfRequired(codeFile: codeFile, originalFileName: originalFileName, memoryStream: memoryStream, pullRequestModel: pullRequestModel,
                        baselineCodeFile: baseLineCodeFile, baseLineStream: baselineStream, baselineFileName: baselineCodeFileName, responseContent: responseContent);
                }
                else
                {
                    var warningMessage = "Failed to download artifact. Please recheck build id and artifact path values in API change detection request.";
                    _logger.LogWarning(warningMessage);
                    _telemetryClient.TrackTrace(warningMessage, SeverityLevel.Warning, new Dictionary<string, string>
                    {
                        { "BuildId", buildId },
                        { "artifactName", artifactName }
                    });
                }

                List<PullRequestModel> pullRequests = new List<PullRequestModel>();
                //Add pull request info only if API revision is created
                if (!string.IsNullOrEmpty(pullRequestModel.APIRevisionId))
                {
                    // Update pull request metadata in DB
                    await UpsertPullRequestAsync(pullRequestModel);
                    pullRequests = (await GetPullRequestsModelAsync(pullRequestNumber: prNumber, repoName: repoName)).ToList();
                }
            }
            finally
            {
                memoryStream.Dispose();
                baselineStream.Dispose();
            }

            // Return review URL created for current package if exists
            return string.IsNullOrEmpty(pullRequestModel.APIRevisionId) ? "" : ManagerHelpers.ResolveReviewUrl(pullRequest: pullRequestModel, hostName: hostName);
        }

        private async Task<bool> IsPullRequestEligibleForCleanup(PullRequestModel prModel)
        {
            if (!_isGitClientAvailable)
                return false;

            var repoInfo = prModel.RepoName.Split("/");
            var issue = await _githubClient.Issue.Get(repoInfo[0], repoInfo[1], prModel.PullRequestNumber);
            // Close review created for pull request if pull request was closed more than _pullRequestCleanupDays days ago
            if (issue.ClosedAt != null)
            {
                return issue.ClosedAt?.AddDays(_pullRequestCleanupDays) < DateTimeOffset.Now;
            }
            return false;
        }

        private async Task ClosePullRequestAPIRevision(PullRequestModel pullRequestModel)
        {
            if (!String.IsNullOrEmpty(pullRequestModel.APIRevisionId))
            {
                var apiRevision = await _apiRevisionsRepository.GetAPIRevisionAsync(pullRequestModel.APIRevisionId);
                if (apiRevision != null && !apiRevision.Approvers.Any())
                {
                    await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(userName: "azure-sdk", apiRevision: apiRevision, notes: "Deleted by PullRequest CleanUp Automation");
                }
            }

            pullRequestModel.IsOpen = false;
            await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);
        }

        private async Task CreateAPIRevisionIfRequired(CodeFile codeFile, string originalFileName, MemoryStream memoryStream,
            PullRequestModel pullRequestModel, CodeFile baselineCodeFile, MemoryStream baseLineStream, string baselineFileName, CreateAPIRevisionAPIResponse responseContent)
        {
            // fetch review for the package or create brand new review
            var review = await _reviewManager.GetReviewAsync(language: codeFile.Language, packageName: codeFile.PackageName);
            if (review == null)
            {
                review = await _reviewManager.CreateReviewAsync(language: codeFile.Language, packageName: codeFile.PackageName, isClosed: false);
                responseContent.ActionsTaken.Add($"No existing review with packageName: '{codeFile.PackageName}' and language: '{codeFile.Language}'.");
                responseContent.ActionsTaken.Add($"Created a new Review with Id: '{review.Id}'.");
            }
            pullRequestModel.ReviewId = review.Id;

            // Base line file is sent in request only for swagger and TypeSpec
            if (baselineCodeFile == null)
            {
                await CreateUpdateRevisionWithoutBaseline(prModel: pullRequestModel, codeFile: codeFile, memoryStream: memoryStream, review: review,
                    originalFileName: originalFileName, responseContent: responseContent);
            }
            else
            {
                await CreateUpdateRevisionWithBaseline(prModel: pullRequestModel, codeFile: codeFile, baselineCodeFile: baselineCodeFile, memoryStream: memoryStream,
                    baselineMemoryStream: baseLineStream, review: review, originalFileName: baselineFileName, responseContent: responseContent);
            }
        }

        private async Task CreateUpdateRevisionWithoutBaseline(PullRequestModel prModel, CodeFile codeFile, MemoryStream memoryStream,
            ReviewListItemModel review, string originalFileName, CreateAPIRevisionAPIResponse responseContent)
        {
            var apiRevisions = (await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId: review.Id)).OrderByDescending(r => r.LastUpdatedOn);
            // If a revision already exists for PR then just update the code file for that revision.
            if (revisionAlreadyExistsForPR(prModel, false))
            {
                if (await UpdateExistingAPIRevisionCodeFile(apiRevisions: apiRevisions, revisionId: prModel.APIRevisionId, memoryStream: memoryStream, codeFile: codeFile,
                    originalFileName: originalFileName, responseContent: responseContent)) 
                {
                    responseContent.ActionsTaken.Add($"Updated the CodeFile of the existing APIRevision: '{prModel.APIRevisionId}'");
                    return;
                }   
            }

            //Create new API revision if PR has API changes            
            if (await prHasAPIChanges(apiRevisions, codeFile))
            {
                var newAPIRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(
                    userName: prModel.CreatedBy, reviewId: review.Id, apiRevisionType: APIRevisionType.PullRequest,
                    label: $"Created for PR {prModel.PullRequestNumber}", memoryStream: memoryStream, codeFile: codeFile, originalName: originalFileName, prNumber: prModel.PullRequestNumber);

                responseContent.ActionsTaken.Add("Pull Request has changes in the API surface when compared to the last updated existing APIRevision.");
                responseContent.ActionsTaken.Add($"Created new APIRevision with id '{newAPIRevision.Id}'.");

                prModel.APIRevisionId = newAPIRevision.Id;
            } 
        }

        private static bool revisionAlreadyExistsForPR(PullRequestModel prModel, bool baseline)
        {
            if (baseline)
            {
                return !string.IsNullOrEmpty(prModel.BaselineAPIRevisionId);
            }
            return !string.IsNullOrEmpty(prModel.APIRevisionId);
        }

        private async Task<bool> prHasAPIChanges(IEnumerable<APIRevisionListItemModel> apiRevisions, CodeFile codeFile)
        {
            var renderedCodeFile = new RenderedCodeFile(codeFile);
            if (apiRevisions != null && apiRevisions.Any())
            {
                // checked if the new apiRevision matches any automatic apiRevision
                var autoAPIRevisions = apiRevisions.Where(r => r.APIRevisionType == APIRevisionType.Automatic);
                foreach (var autoAPIRevision in autoAPIRevisions)
                {
                    if (await _apiRevisionsManager.AreAPIRevisionsTheSame(autoAPIRevision, renderedCodeFile))
                    {
                        // no change in api surface level from existing revision
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Check to see if there is an existing APIRevision for the same PR. If yes, update the codeFile for the existing APIRevision
        /// </summary>
        /// <param name="apiRevisions"></param>
        /// <param name="revisionId"></param>
        /// <param name="memoryStream"></param>
        /// <param name="codeFile"></param>
        /// <param name="originalFileName"></param>
        /// <param name="responseContent"></param>
        /// <returns>true if update happened otherwise false</returns>
        private async Task<bool> UpdateExistingAPIRevisionCodeFile(IEnumerable<APIRevisionListItemModel> apiRevisions,
            string revisionId, MemoryStream memoryStream, CodeFile codeFile, string originalFileName, CreateAPIRevisionAPIResponse responseContent)
        {
            var apiRevision = apiRevisions.FirstOrDefault(v => v.Id == revisionId);
            if (apiRevision != default(APIRevisionListItemModel))
            {
                //Update the code file if revision already exists
                var codeModel = await _codeFileManager.CreateReviewCodeFileModel(
                       apiRevisionId: revisionId, memoryStream: memoryStream, codeFile: codeFile);
                codeModel.FileName = originalFileName;
                apiRevision.Files[0] = codeModel;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(apiRevision);
                return true;
            }
            return false;
        }

        private async Task CreateUpdateRevisionWithBaseline(PullRequestModel prModel, CodeFile codeFile, CodeFile baselineCodeFile,
            MemoryStream memoryStream, MemoryStream baselineMemoryStream, ReviewListItemModel review, string originalFileName, CreateAPIRevisionAPIResponse responseContent)
        {
            var apiRevisions = (await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId: review.Id)).OrderByDescending(r => r.LastUpdatedOn);
            // If a revision already exists for PR then just update the code file for that revision.
            bool createNewBaselineRevision = true;
            bool createNewModifiedRevision = true;
            if (revisionAlreadyExistsForPR(prModel, true))
            {
                if (await UpdateExistingAPIRevisionCodeFile(apiRevisions: apiRevisions, revisionId: prModel.BaselineAPIRevisionId, memoryStream: baselineMemoryStream,
                    codeFile: baselineCodeFile, originalFileName: originalFileName, responseContent: responseContent))
                {
                    responseContent.ActionsTaken.Add($"Updated the CodeFile of the existing BaseLine APIRevision: '{prModel.BaselineAPIRevisionId}'");
                    createNewBaselineRevision = false;
                }   
            }
            if (revisionAlreadyExistsForPR(prModel, false))
            {
                if (await UpdateExistingAPIRevisionCodeFile(apiRevisions: apiRevisions, revisionId: prModel.APIRevisionId, memoryStream: memoryStream, codeFile: codeFile,
                    originalFileName: originalFileName, responseContent: responseContent))
                {
                    responseContent.ActionsTaken.Add($"Updated the CodeFile of the existing APIRevision: '{prModel.APIRevisionId}'");
                    createNewModifiedRevision = false;
                }
            }

            // Create baseline revision
            if (createNewBaselineRevision)
            {
                var newAPIRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(
                    userName: prModel.CreatedBy, reviewId: review.Id, apiRevisionType: APIRevisionType.PullRequest,
                    label: $"Baseline for PR {prModel.PullRequestNumber}", memoryStream: baselineMemoryStream, codeFile: baselineCodeFile,
                    originalName: originalFileName);
                prModel.BaselineAPIRevisionId = newAPIRevision.Id;
                responseContent.ActionsTaken.Add($"Created new Baseline APIRevisions with Id '{newAPIRevision.Id}'.");
            }

            // Create modified revision
            if (createNewModifiedRevision)
            {
                var newAPIRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(
                    userName: prModel.CreatedBy, reviewId: review.Id, apiRevisionType: APIRevisionType.PullRequest,
                    label: $"Created for PR {prModel.PullRequestNumber}", memoryStream: memoryStream, codeFile: codeFile,
                    originalName: originalFileName);
                prModel.APIRevisionId = newAPIRevision.Id;
                responseContent.ActionsTaken.Add($"Created new APIRevision with id '{newAPIRevision.Id}'.");
            }

            //Calculate the diff if it's for swagger as an async task.
            //No need to await for diff calculation processing to be completed to respond to HTTP request
            _ = Task.Run(async () =>
            {
                var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId: review.Id);
                var modifiedRevision = revisions.FirstOrDefault(r => r.Id == prModel.APIRevisionId);
                var baseline = revisions.Where(r => r.Id == prModel.BaselineAPIRevisionId);
                if (modifiedRevision != default(APIRevisionListItemModel))
                {
                    await _apiRevisionsManager.GetLineNumbersOfHeadingsOfSectionsWithDiff(review.Id, modifiedRevision, baseline);
                }
            });
        }
    }
}
