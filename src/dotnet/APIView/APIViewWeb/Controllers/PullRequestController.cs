using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.DataContracts;
using APIViewWeb.Helpers;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.IO;
using APIViewWeb.Managers.Interfaces;
using ApiView;
using System;
using APIViewWeb.Models;
using APIViewWeb.LeanModels;
using Microsoft.VisualStudio.Services.DelegatedAuthorization;
using Amazon.Util;
using Octokit;
using static Microsoft.VisualStudio.Services.Graph.Constants;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace APIViewWeb.Controllers
{
    public class PullRequestController : Controller
    {
        private readonly ICodeFileManager _codeFileManager;
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ILogger<PullRequestController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IOpenSourceRequestManager _openSourceManager;
        private readonly TelemetryClient _telemetryClient = new TelemetryClient(TelemetryConfiguration.CreateDefault());
        private HashSet<string> _allowedListBotAccounts = new HashSet<string>();

        string[] VALID_EXTENSIONS = new string[] { ".whl", ".api.json", ".nupkg", "-sources.jar", ".gosource" };
        
        public PullRequestController(ICodeFileManager codeFileManager, IPullRequestManager pullRequestManager,
            IBlobCodeFileRepository codeFileRepository, IAPIRevisionsManager apiRevisionsManager, IReviewManager reviewManager, ILogger<PullRequestController> logger,
            IConfiguration configuration, IOpenSourceRequestManager openSourceRequestManager)
        {
            _codeFileManager = codeFileManager;
            _pullRequestManager = pullRequestManager;
            _codeFileRepository = codeFileRepository;
            _reviewManager = reviewManager;
            _apiRevisionsManager = apiRevisionsManager;
            _logger = logger;
            _configuration = configuration;
            _openSourceManager = openSourceRequestManager;

            var botAllowedList = _configuration["allowedList-bot-github-accounts"];
            if (!string.IsNullOrEmpty(botAllowedList))
            {
                _allowedListBotAccounts.UnionWith(botAllowedList.Split(","));
            }
        }

        [HttpGet]
        public async Task<ActionResult> DetectApiChanges(
            string buildId,
            string artifactName,
            string filePath,
            string commitSha,
            string repoName,
            string packageName,
            int pullRequestNumber = 0,
            string codeFile = null,
            string baselineCodeFile = null,
            bool commentOnPR = true,
            string language = null)
        {
            if (!ValidateInputParams())
            {
                return StatusCode(StatusCodes.Status400BadRequest);
            }

            //Handle only authorization exception and send 401 as status code.
            //All other exception should not be handled so we will have required info in app insights.
            try
            {
                var reviewUrl = await DetectAPIChanges(
                    buildId: buildId, artifactName: artifactName,
                    originalFileName: filePath, commitSha: commitSha,
                    repoName: repoName, packageName: packageName,
                    prNumber: pullRequestNumber, hostName: this.Request.Host.ToUriComponent(),
                    codeFileName: codeFile, baselineCodeFileName: baselineCodeFile,
                    commentOnPR: commentOnPR, language: language);

                return !string.IsNullOrEmpty(reviewUrl) ? StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl) : StatusCode(statusCode: StatusCodes.Status208AlreadyReported);
            }
            catch (AuthorizationFailedException)
            {
                return StatusCode(StatusCodes.Status401Unauthorized);
            }
        }

        private async Task<string> DetectAPIChanges(string buildId,
            string artifactName,
            string originalFileName,
            string commitSha,
            string repoName,
            string packageName,
            int prNumber,
            string hostName,
            string codeFileName = null,
            string baselineCodeFileName = null,
            bool commentOnPR = true,
            string language = null,
            string project = "public")
        {
            language = LanguageServiceHelpers.MapLanguageAlias(language: language);
            var requestTelemetry = new RequestTelemetry { Name = "Detecting API changes for PR: " + prNumber };
            var operation = _telemetryClient.StartOperation(requestTelemetry);
            originalFileName = originalFileName ?? codeFileName;
            var repoInfo = repoName.Split("/");
            var pullRequestModel = await _pullRequestManager.GetPullRequestModelAsync(prNumber, repoName, packageName, originalFileName, language);
            if (pullRequestModel == null)
            {
                return "";
            }
            if (pullRequestModel.Commits.Any(c => c == commitSha))
            {
                // PR commit is already processed. No need to reprocess it again.
                return !string.IsNullOrEmpty(pullRequestModel.ReviewId) ? ManagerHelpers.ResolveReviewUrl(pullRequest: pullRequestModel, hostName: hostName) : "";
            }
           
            pullRequestModel.Commits.Add(commitSha);
            //Check if PR owner is part of Azure//Microsoft org in GitHub
            await ManagerHelpers.AssertPullRequestCreatorPermission(prModel: pullRequestModel, allowedListBotAccounts: _allowedListBotAccounts,
                openSourceManager: _openSourceManager, telemetryClient: _telemetryClient);
           
            using var memoryStream = new MemoryStream();
            using var baselineStream = new MemoryStream();
            var codeFile = await _codeFileManager.GetCodeFileAsync(
                repoName: repoName, buildId: buildId, artifactName: artifactName,
                packageName: packageName, originalFileName: originalFileName,
                codeFileName: codeFileName, originalFileStream: memoryStream,
                baselineCodeFileName: baselineCodeFileName, baselineStream: baselineStream,
                project: project);
           
            CodeFile baseLineCodeFile = null;
            if (baselineStream.Length > 0)
            {
                baselineStream.Position = 0;
                baseLineCodeFile = await CodeFile.DeserializeAsync(baselineStream);
            }
            if (codeFile != null)
            {
                await CreateAPIRevisionIfRequired(codeFile, prNumber, originalFileName, memoryStream, pullRequestModel, baseLineCodeFile, baselineStream, baselineCodeFileName);
            }
            else
            {
                _telemetryClient.TrackTrace("Failed to download artifact. Please recheck build id and artifact path values in API change detection request.");
            }

            //Generate combined single comment to update on PR.
            var pullRequests = await _pullRequestManager.GetPullRequestsModelAsync(pullRequestNumber: prNumber, repoName: repoName);
            if (commentOnPR)
            {
                await _pullRequestManager.CreateOrUpdateCommentsOnPR(pullRequests.ToList(), repoInfo[0], repoInfo[1], prNumber, hostName);
            }

            await _pullRequestManager.UpsertPullRequestAsync(pullRequestModel);

            // Return review URL created for current package if exists
            var pr = pullRequests.SingleOrDefault(r => r.PackageName == packageName && (r.Language == null || r.Language == language));
            return pr == null ? "" : ManagerHelpers.ResolveReviewUrl(pullRequest: pr, hostName: hostName);

        }

        private async Task CreateAPIRevisionIfRequired(CodeFile codeFile, int prNumber,
            string originalFileName, MemoryStream memoryStream,
            PullRequestModel pullRequestModel, CodeFile baselineCodeFile,
            MemoryStream baseLineStream, string baselineFileName)
        {
            // fetch review for the package or create brand new review
            var review  = await _reviewManager.GetReviewAsync(language: codeFile.Language, packageName: codeFile.PackageName);
            if (review == null)
            {
                review = await _reviewManager.CreateReviewAsync(language: codeFile.Language, packageName: codeFile.PackageName, isClosed: false);
            }
            pullRequestModel.ReviewId = review.Id;

            var renderedCodeFile = new RenderedCodeFile(codeFile);
            var apiRevisions = (await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId: review.Id)).OrderByDescending(r => r.CreatedOn);

            if (apiRevisions.Any())
            {
                if (codeFile.Language == "Swagger" || codeFile.Language == "TypeSpec")
                {
                    if (_codeFileManager.AreCodeFilesTheSame(codeFileA: codeFile, codeFileB: baselineCodeFile))
                    {
                        return;
                    }
                    
                    var createBaseLine = true;
                    
                    foreach (var apiRevision in apiRevisions)
                    {
                        var aprRevisionCodeFile = await _codeFileRepository.GetCodeFileAsync(apiRevision, false);
                        if (_codeFileManager.AreCodeFilesTheSame(codeFileA: aprRevisionCodeFile.CodeFile, codeFileB: baselineCodeFile))
                        {
                            createBaseLine = false;
                            break;
                        }
                    }

                    if (createBaseLine)
                    {
                        await _apiRevisionsManager.CreateAPIRevisionAsync(
                            userName: pullRequestModel.CreatedBy, reviewId: review.Id, apiRevisionType: APIRevisionType.PullRequest,
                            label: $"BaseLine for PR: {prNumber}", memoryStream: baseLineStream, codeFile: baselineCodeFile, originalName: baselineFileName, prNumber: prNumber);
                    }

                    var codeFileUpdated = await AttemptUpdateOfExistingAPIRevisionCodeFile(apiRevisions: apiRevisions, review: review, memoryStream: memoryStream, codeFile: codeFile, prNumber: prNumber);
                    if (codeFileUpdated)
                    {
                        return;
                    }
                }
                else
                {
                    // checked if the new apiRevision matches any automatic apiRevision
                    var autoAPIRevisions = apiRevisions.Where(r => r.APIRevisionType == APIRevisionType.Automatic);

                    foreach (var autoAPIRevision in autoAPIRevisions)
                    {
                        if (await _apiRevisionsManager.AreAPIRevisionsTheSame(autoAPIRevision, renderedCodeFile))
                        {
                            // no change in api surface level from exisiting revision
                            return;
                        }
                    }

                    var codeFileUpdated = await AttemptUpdateOfExistingAPIRevisionCodeFile(apiRevisions: apiRevisions, review: review, memoryStream: memoryStream, codeFile: codeFile, prNumber: prNumber);
                    if (codeFileUpdated)
                    {
                        return;
                    }
                }
            }

            var newAPIRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(
                userName: pullRequestModel.CreatedBy, reviewId: review.Id, apiRevisionType: APIRevisionType.PullRequest,
                label: String.Empty, memoryStream: memoryStream, codeFile: codeFile, originalName: originalFileName, prNumber: prNumber);
            
            pullRequestModel.APIRevisionId = newAPIRevision.Id;

            if (!String.IsNullOrEmpty(review.Language) && review.Language == "Swagger")
            {
                await _apiRevisionsManager.GetLineNumbersOfHeadingsOfSectionsWithDiff(reviewId: review.Id, apiRevision: newAPIRevision);
            }
        }

        /// <summary>
        /// Check to see if there is an existing APIRevision for the same PR. If yes, update the codeFile for the existing APIRevision
        /// </summary>
        /// <param name="apiRevisions"></param>
        /// <param name="review"></param>
        /// <param name="memoryStream"></param>
        /// <param name="codeFile"></param>
        /// <param name="prNumber"></param>
        /// <returns>true if update happened otherwise false</returns>
        private async Task<bool> AttemptUpdateOfExistingAPIRevisionCodeFile(IEnumerable<APIRevisionListItemModel> apiRevisions, ReviewListItemModel review, MemoryStream memoryStream, CodeFile codeFile, int prNumber)
        {
            var result = false;
            var prAPIRevisions = apiRevisions.Where(r => r.APIRevisionType == APIRevisionType.PullRequest);
            var prsForReview = await _pullRequestManager.GetPullRequestsModelAsync(reviewId: review.Id);

            foreach (var prAPIRevision in prAPIRevisions)
            {
                // Check if you have already created a revision for the same PR
                var existingRevisionForPR = prsForReview.FirstOrDefault(p => p.APIRevisionId == prAPIRevision.Id && p.PullRequestNumber == prNumber);
                if (existingRevisionForPR != default(PullRequestModel))
                {
                    // update codeFile for existing apiRevision with the incoming codefile
                    prAPIRevision.Files[0] = await _codeFileManager.CreateReviewCodeFileModel(
                        apiRevisionId: prAPIRevision.Id, memoryStream: memoryStream, codeFile: codeFile);

                    await _apiRevisionsManager.UpdateAPIRevisionAsync(prAPIRevision);
                    result = true;
                    break;
                }
            }
            return result;
        }



        private bool ValidateInputParams()
        { 
            foreach (var queryParam in this.Request.Query)
            {
                var value = queryParam.Value.ToString();
                if (queryParam.Key == "filePath")
                {
                    if (!VALID_EXTENSIONS.Any(e => value.EndsWith(e)))
                        return false;
                }

                if (queryParam.Key == "repoName")
                {
                    if (!value.Contains("/"))
                        return false;
                }
            }
            return true;
        }
    }
}
