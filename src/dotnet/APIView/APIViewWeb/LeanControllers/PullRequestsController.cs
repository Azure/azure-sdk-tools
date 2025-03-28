using APIViewWeb.DTOs;
using APIViewWeb.Extensions;
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
            var reviewSpaUrlTemplate = "{0}review/{1}?activeApiRevisionId={2}";
            var reviewUrlTemplate = "{0}Assemblies/Review/{1}?revisionId={2}";

            List<PullRequestReviewDto> pullRequestReviewDtos = new List<PullRequestReviewDto>();
            var statusCode = StatusCodes.Status204NoContent;

            if (prsForCommit.Any())
            {
                foreach (var pr in prsForCommit)
                {
                    var prDto = new PullRequestReviewDto();
                    var languageService = LanguageServiceHelpers.GetLanguageService(language: pr.Language, languageServices: _languageServices);
                    if (languageService.UsesTreeStyleParser) // Languages using treestyle parser are also using the spa UI
                    {
                        prDto.Url = string.Format(reviewSpaUrlTemplate, spaHost, pr.ReviewId, pr.APIRevisionId);
                    }
                    else 
                    {
                        prDto.Url = string.Format(reviewUrlTemplate, host, pr.ReviewId, pr.APIRevisionId);
                    }
                    prDto.PackageName = pr.PackageName;
                    prDto.Language = pr.Language;
                    pullRequestReviewDtos.Add(prDto);
                }
                statusCode = StatusCodes.Status200OK;
            }
            return new LeanJsonResult(pullRequestReviewDtos, statusCode);
        }
    }
}
