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
using APIViewWeb.Models;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Controllers
{
    public class PullRequestController : Controller
    {
        private readonly ICodeFileManager _codeFileManager;
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IConfiguration _configuration;
        private readonly IOpenSourceRequestManager _openSourceManager;
        private readonly TelemetryClient _telemetryClient;
        private HashSet<string> _allowedListBotAccounts = new HashSet<string>();

        string[] VALID_EXTENSIONS = new string[] { ".whl", ".api.json", ".nupkg", "-sources.jar", ".gosource" };
        
        public PullRequestController(ICodeFileManager codeFileManager, IPullRequestManager pullRequestManager,
            IAPIRevisionsManager apiRevisionsManager, IReviewManager reviewManager,
            IConfiguration configuration, IOpenSourceRequestManager openSourceRequestManager, TelemetryClient telemetryClient)
        {
            _codeFileManager = codeFileManager;
            _pullRequestManager = pullRequestManager;
            _reviewManager = reviewManager;
            _apiRevisionsManager = apiRevisionsManager;
            _configuration = configuration;
            _openSourceManager = openSourceRequestManager;
            _telemetryClient = telemetryClient;

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

            //Get Code File to find the actual package name emmitted by parser
            // We should deprecate package name param and use PackageName in CodeFile
            using var memoryStream = new MemoryStream();
            using var baselineStream = new MemoryStream();
            var codeFile = await _codeFileManager.GetCodeFileAsync(
                repoName: repoName, buildId: buildId, artifactName: artifactName,
                packageName: packageName, originalFileName: originalFileName,
                codeFileName: codeFileName, originalFileStream: memoryStream,
                baselineCodeFileName: baselineCodeFileName, baselineStream: baselineStream,
                project: project);

            if (codeFile.PackageName != null && (packageName ==  null || packageName != codeFile.PackageName))
            {
                packageName = codeFile.PackageName;
            }

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


            try
            {
                CodeFile baseLineCodeFile = null;
                if (baselineStream.Length > 0)
                {
                    baselineStream.Position = 0;
                    baseLineCodeFile = await CodeFile.DeserializeAsync(baselineStream);
                }
                if (codeFile != null)
                {
                    await CreateAPIRevisionIfRequired(codeFile, originalFileName, memoryStream, pullRequestModel, baseLineCodeFile, baselineStream, baselineCodeFileName);
                }
                else
                {
                    _telemetryClient.TrackTrace("Failed to download artifact. Please recheck build id and artifact path values in API change detection request.");
                }

                List<PullRequestModel> pullRequests = new List<PullRequestModel>();
                //Add pull request info only if API revision is created
                if (!string.IsNullOrEmpty(pullRequestModel.APIRevisionId))
                {
                    // Update pull request metadata in DB
                    await _pullRequestManager.UpsertPullRequestAsync(pullRequestModel);
                    pullRequests = (await _pullRequestManager.GetPullRequestsModelAsync(pullRequestNumber: prNumber, repoName: repoName)).ToList();
                }
                //Generate combined single comment to update on PR or add a comment stating no API changes.            
                if (commentOnPR)
                {
                    await _pullRequestManager.CreateOrUpdateCommentsOnPR(pullRequests, repoInfo[0], repoInfo[1], prNumber, hostName);
                }
            }
            finally
            {
                memoryStream.Dispose();
                baselineStream.Dispose();
            }            

            // Return review URL created for current package if exists
            return string.IsNullOrEmpty(pullRequestModel.APIRevisionId)? "" : ManagerHelpers.ResolveReviewUrl(pullRequest: pullRequestModel, hostName: hostName);
        }

        private async Task CreateAPIRevisionIfRequired(CodeFile codeFile,
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

            // Base line file is sent in request only for swagger and TypeSpec
            if(baselineCodeFile == null)
            {
                await CreateUpdateRevisionWithoutBaseline(pullRequestModel, codeFile, memoryStream, review, originalFileName);
            }
            else
            {
                await CreateUpdateRevisionWithBaseline(pullRequestModel, codeFile, baselineCodeFile, memoryStream, baseLineStream, review, baselineFileName);
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

        private async Task CreateUpdateRevisionWithoutBaseline(PullRequestModel prModel, CodeFile codeFile, MemoryStream memoryStream, ReviewListItemModel review, string originalFileName)
        {
            var apiRevisions = (await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId: review.Id)).OrderByDescending(r => r.LastUpdatedOn);
            // If a revision already exists for PR then just update the code file for that revision.
            if (revisionAlreadyExistsForPR(prModel, false))
            {
                if (await UpdateExistingAPIRevisionCodeFile(apiRevisions, prModel.APIRevisionId, memoryStream, codeFile))
                    return;
            }

            //Create new API revision if PR has API changes            
            if ( await prHasAPIChanges(apiRevisions, codeFile))
            {
                var newAPIRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(
                    userName: prModel.CreatedBy, reviewId: review.Id, apiRevisionType: APIRevisionType.PullRequest,
                    label: $"Created for PR {prModel.PullRequestNumber}", memoryStream: memoryStream, codeFile: codeFile, originalName: originalFileName, prNumber: prModel.PullRequestNumber);

                prModel.APIRevisionId = newAPIRevision.Id;
            }
        }
        private async Task CreateUpdateRevisionWithBaseline(PullRequestModel prModel,
            CodeFile codeFile,
            CodeFile baselineCodeFile,
            MemoryStream memoryStream,
            MemoryStream baselineMemoryStream,
            ReviewListItemModel review,
            string originalFileName)
        {
            var apiRevisions = (await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId: review.Id)).OrderByDescending(r => r.LastUpdatedOn);
            // If a revision already exists for PR then just update the code file for that revision.
            bool createNewBaselineRevision = true;
            bool createNewModifiedRevision = true;
            if (revisionAlreadyExistsForPR(prModel, true))
            {
                if (await UpdateExistingAPIRevisionCodeFile(apiRevisions, prModel.APIRevisionId, baselineMemoryStream, baselineCodeFile))
                    createNewBaselineRevision = false;
            }
            if (revisionAlreadyExistsForPR(prModel, false))
            {
                if (await UpdateExistingAPIRevisionCodeFile(apiRevisions, prModel.APIRevisionId, memoryStream, codeFile))
                    createNewModifiedRevision = false;
            }

            // Create baseline revision
            if (createNewBaselineRevision)
            {
                var newAPIRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(
                    userName: prModel.CreatedBy, reviewId: review.Id, apiRevisionType: APIRevisionType.PullRequest,
                    label: $"Baseline for PR {prModel.PullRequestNumber}", memoryStream: baselineMemoryStream, codeFile: baselineCodeFile,
                    originalName: originalFileName);
                prModel.BaselineAPIRevisionId = newAPIRevision.Id;
            }

            // Create modified revision
            if (createNewModifiedRevision)
            {
                var newAPIRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(
                    userName: prModel.CreatedBy, reviewId: review.Id, apiRevisionType: APIRevisionType.PullRequest,
                    label: $"Created for PR {prModel.PullRequestNumber}", memoryStream: memoryStream, codeFile: codeFile,
                    originalName: originalFileName);
                prModel.APIRevisionId = newAPIRevision.Id;
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

        /// <summary>
        /// Check to see if there is an existing APIRevision for the same PR. If yes, update the codeFile for the existing APIRevision
        /// </summary>
        /// <param name="apiRevisions"></param>
        /// <param name="revisionId"></param>
        /// <param name="memoryStream"></param>
        /// <param name="codeFile"></param>
        /// <returns>true if update happened otherwise false</returns>
        private async Task<bool> UpdateExistingAPIRevisionCodeFile(IEnumerable<APIRevisionListItemModel> apiRevisions, string revisionId, MemoryStream memoryStream, CodeFile codeFile)
        {
            var apiRevision = apiRevisions.FirstOrDefault(v => v.Id == revisionId);
            if (apiRevision != default(APIRevisionListItemModel))
            {
                //Update the code file if revision already exists
                var codeModel = await _codeFileManager.CreateReviewCodeFileModel(
                       apiRevisionId: revisionId, memoryStream: memoryStream, codeFile: codeFile);

                apiRevision.Files[0] = codeModel;
                await _apiRevisionsManager.UpdateAPIRevisionAsync(apiRevision);
                return true;
            }
            return false;
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
