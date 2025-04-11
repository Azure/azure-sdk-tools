using APIViewWeb.DTOs;
using APIViewWeb.Helpers;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.LeanControllers
{
    public class PullRequestsController  : BaseApiController
    {
        private readonly ILogger<PullRequestsController> _logger;
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IConfiguration _configuration;
        private readonly IEnumerable<LanguageService> _languageServices;

        public PullRequestsController(
            ILogger<PullRequestsController> logger,
            IPullRequestManager pullRequestManager, IConfiguration configuration, IEnumerable<LanguageService> languageServices)
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

            var host = _configuration["APIVIew-Host-Url"];
            var spaHost = _configuration["APIVIew-SPA-Host-Url"];
            var reviewSpaUrlTemplate = "{0}/review/{1}?activeApiRevisionId={2}";
            var reviewUrlTemplate = "{0}/Assemblies/Review/{1}?revisionId={2}";

            List<PullRequestReviewDto> pullRequestReviewDtos = new List<PullRequestReviewDto>();
            var statusCode = StatusCodes.Status204NoContent;

            if (prsForCommit.Any())
            {
                foreach (var pr in prsForCommit)
                {
                    var prDto = new PullRequestReviewDto();
                    var language = LanguageServiceHelpers.MapLanguageAlias(pr.Language);
                    var languageService = LanguageServiceHelpers.GetLanguageService(language: language, languageServices: _languageServices);
                    if (languageService.UsesTreeStyleParser) // Languages using treestyle parser are also using the spa UI
                    {
                        prDto.Url = string.Format(reviewSpaUrlTemplate, spaHost, pr.ReviewId, pr.APIRevisionId);
                    }
                    else 
                    {
                        prDto.Url = string.Format(reviewUrlTemplate, host, pr.ReviewId, pr.APIRevisionId);
                    }
                    prDto.PackageName = pr.PackageName;
                    prDto.Language = language;
                    pullRequestReviewDtos.Add(prDto);
                }
                statusCode = StatusCodes.Status200OK;
            }
            return new LeanJsonResult(pullRequestReviewDtos, statusCode);
        }

        /// <summary>
        /// Check if ther are changes in API surface between new and existing API revisions
        /// Create new API revision bases presence of API changes
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
        /// <returns></returns>
        [HttpGet("CreateAPIRevisionIfAPIHasChanges", Name = "DetectAPIChanges")]
        public async Task<ActionResult<IEnumerable<PullRequestModel>>> CreateAPIRevisionIfAPIHasChanges(
            string buildId, string artifactName, string filePath, string commitSha,
            string repoName, string packageName, int pullRequestNumber = 0, string codeFile = null,
            string baselineCodeFile = null, string language = null, string project = "internal")
        {
        }
    }
}
