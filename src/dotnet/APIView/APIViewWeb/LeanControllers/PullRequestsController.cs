using APIViewWeb.DTOs;
using APIViewWeb.Helpers;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.LeanControllers
{
    public class PullRequestsController  : BaseApiController
    {
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IEnumerable<LanguageService> _languageServices;

        string[] VALID_EXTENSIONS = new string[] { ".whl", ".api.json", ".json", ".nupkg", "-sources.jar", ".gosource" };

        public PullRequestsController(
            ILogger<PullRequestsController> logger, IPullRequestManager pullRequestManager,
            IConfiguration configuration, IEnumerable<LanguageService> languageServices)
        {
            _logger = logger;
            _pullRequestManager = pullRequestManager;
            _configuration = configuration;
            _languageServices = languageServices;
        }

        /// <summary>
        /// Retrieves Pull Requests associated with an API Revision
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        [HttpGet("{reviewId}/{apiRevisionId}", Name = "GetAssociatedPullRequests")]
        public async Task<ActionResult<IEnumerable<PullRequestModel>>> GetAssociatedPullRequestsAsync(string reviewId, string apiRevisionId)
        {
            var results = await _pullRequestManager.GetPullRequestsModelAsync(reviewId, apiRevisionId);
            return new LeanJsonResult(results, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Retrieves Pull Requests of all API Revisions associated with a Review
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        [HttpGet("{reviewId}/{apiRevisionId}/prsofassociatedapirevisions", Name = "GetPRsOfAssociatedAPIRevisions")]
        public async Task<ActionResult<IEnumerable<PullRequestModel>>> GetPRsOfAssociatedAPIRevisionsAsync(string reviewId, string apiRevisionId)
        {
            IEnumerable<PullRequestModel> results = new List<PullRequestModel>();
            var creatingPR = (await _pullRequestManager.GetPullRequestsModelAsync(reviewId, apiRevisionId)).FirstOrDefault();
            if (creatingPR != null)
            {
                results = await _pullRequestManager.GetPullRequestsModelAsync(creatingPR.PullRequestNumber, creatingPR.RepoName);
            }
            return new LeanJsonResult(results, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Retrieves API Revision information associated with a Pull Request and commitSHA
        /// </summary>
        /// <param name="pullRequestNumber"></param>
        /// <param name="repoName"></param>
        /// <param name="commitSHA"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet(Name = "GetPullRequestReviews")]
        public async Task<ActionResult<IEnumerable<PullRequestModel>>> GetPullRequestReviews(int pullRequestNumber, string repoName, string commitSHA)
        {
            IEnumerable<PullRequestModel> pullRequestModels = await _pullRequestManager.GetPullRequestsModelAsync(pullRequestNumber: pullRequestNumber, repoName: repoName);
            var prsForCommit = pullRequestModels.Where(c => c.Commits.Contains(commitSHA));

            List<PullRequestReviewDto> pullRequestReviewDtos = new List<PullRequestReviewDto>();
            var statusCode = StatusCodes.Status204NoContent;

            if (prsForCommit.Any())
            {
                foreach (var pr in prsForCommit)
                {
                    var prDto = new PullRequestReviewDto();
                    var language = LanguageServiceHelpers.MapLanguageAlias(pr.Language);
                    prDto.Url = ManagerHelpers.ResolveReviewUrl(reviewId: pr.ReviewId, apiRevisionId: pr.APIRevisionId, language: language, configuration: _configuration, languageServices: _languageServices);
                    prDto.PackageName = pr.PackageName;
                    prDto.Language = language;
                    pullRequestReviewDtos.Add(prDto);
                }
                statusCode = StatusCodes.Status200OK;
            }
            return new LeanJsonResult(pullRequestReviewDtos, statusCode);
        }

        /// <summary>
        /// Check if there are changes in API surface between new and existing API revisions
        /// Create new API revision based on presence of API changes
        /// </summary>
        /// <param name="buildId"></param>
        /// <param name="artifactName"></param>
        /// <param name="filePath"></param>
        /// <param name="commitSha"></param>
        /// <param name="repoName"></param>
        /// <param name="packageName"></param>
        /// <param name="pullRequestNumber"></param>
        /// <param name="codeFile"></param>
        /// <param name="baselineCodeFile"></param>
        /// <param name="language"></param>
        /// <param name="project"></param>
        /// <param name="packageType"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("CreateAPIRevisionIfAPIHasChanges", Name = "CreateAPIRevisionIfAPIHasChanges")]
        public async Task<ActionResult<IEnumerable<CreateAPIRevisionAPIResponse>>> CreateAPIRevisionIfAPIHasChanges(
            string buildId, string artifactName, string filePath, string commitSha,string repoName, string packageName,
            int pullRequestNumber = 0, string codeFile = null, string baselineCodeFile = null, string language = null,
            string project = "internal", string packageType = null)
        {
            var responseContent = new CreateAPIRevisionAPIResponse();
            if (!ValidateInputParams())
            {
                return new LeanJsonResult(responseContent, StatusCodes.Status400BadRequest);
            }

            //Handle only authorization exception and send 401 as status code.
            //All other exception should not be handled so we will have required info in app insights.
            try
            {
                var apiRevisionUrl = await _pullRequestManager.CreateAPIRevisionIfAPIHasChanges(buildId: buildId,
                    artifactName: artifactName, originalFileName: filePath, commitSha: commitSha, repoName: repoName,
                    packageName: packageName, prNumber: pullRequestNumber, hostName: this.Request.Host.ToUriComponent(),
                    responseContent: responseContent, codeFileName: codeFile, baselineCodeFileName: baselineCodeFile,
                    language: language, project: project, packageType: packageType);

                responseContent.APIRevisionUrl = apiRevisionUrl;

                return !string.IsNullOrEmpty(apiRevisionUrl) ? new LeanJsonResult(responseContent, StatusCodes.Status201Created) : new LeanJsonResult(responseContent, StatusCodes.Status208AlreadyReported);
            }
            catch (AuthorizationFailedException)
            {
                return new LeanJsonResult(responseContent, StatusCodes.Status401Unauthorized);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating API revision if API has changes.");
                responseContent.ErrorMessage = ex.Message;
                return new LeanJsonResult(responseContent, StatusCodes.Status500InternalServerError);
            }
        }

        private bool ValidateInputParams()
        {
            foreach (var queryParam in this.Request.Query)
            {
                var value = queryParam.Value.ToString();
                if (queryParam.Key == "filePath")
                {
                    if (!VALID_EXTENSIONS.Any(e => value.EndsWith(e))) 
                    {
                        _logger.LogWarning($"QueryParam 'filePath' has an invalid extension.");
                        return false;
                    }
                       
                }

                if (queryParam.Key == "repoName")
                {
                    if (!value.Contains("/"))
                    {
                        _logger.LogWarning($"QueryParam 'repoName' should contain '/'.");
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
