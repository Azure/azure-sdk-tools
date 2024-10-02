using APIViewWeb.Extensions;
using APIViewWeb.Helpers;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        public PullRequestsController(ILogger<PullRequestsController> logger, IPullRequestManager pullRequestManager)
        {
            _logger = logger;
            _pullRequestManager = pullRequestManager;
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
    }
}
