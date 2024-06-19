using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Managers;

namespace APIViewWeb.LeanControllers
{
    public class APIRevisionsController : BaseApiController
    {
        private readonly ILogger<APIRevisionsController> _logger;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IReviewManager _reviewManager;
        private readonly IPullRequestManager _pullRequestManager;

        public APIRevisionsController(ILogger<APIRevisionsController> logger,
            IAPIRevisionsManager apiRevisionsManager, IReviewManager reviewManager, IPullRequestManager pullRequestManager)
        {
            _logger = logger;
            _apiRevisionsManager = apiRevisionsManager;
            _reviewManager = reviewManager;
            _pullRequestManager = pullRequestManager;
        }

        /// <summary>
        /// Endpoint used by Client SPA for listing reviews.
        /// </summary>
        /// <param name="pageParams"></param>
        /// <param name="filterAndSortParams"></param>
        /// <returns></returns>
        [HttpPost(Name = "GetAPIRevisions")]
        public async Task<ActionResult<PagedList<APIRevisionListItemModel>>> GetAPIRevisionsAsync([FromQuery] PageParams pageParams, [FromBody] APIRevisionsFilterAndSortParams filterAndSortParams)
        {
            var result = await _apiRevisionsManager.GetAPIRevisionsAsync(User, pageParams, filterAndSortParams);
            Response.AddPaginationHeader(new PaginationHeader(result.NoOfItemsRead, result.PageSize, result.TotalCount));
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Endpoint used by Client SPA for Deleting APIRevisions.
        /// </summary>
        /// <param name="deleteParams"></param>
        /// <returns></returns>
        [HttpPut("delete", Name = "DeleteAPIRevisions")]
        public async Task DeleteAPIRevisionsAsync([FromBody] APIRevisionSoftDeleteParam deleteParams)
        {
            foreach (var apiRevisionId in deleteParams.apiRevisionIds)
            {
                await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(user: User, reviewId: deleteParams.reviewId, revisionId: apiRevisionId);
            }
        }

        /// <summary>
        /// Endpoint used by Client SPA for Restoring Deleted APIRevisions.
        /// </summary>
        /// <param name="deleteParams"></param>
        /// <returns></returns>
        [HttpPut("restore", Name = "RestoreAPIRevisions")]
        public async Task RestoreAPIRevisionsAsync([FromBody] APIRevisionSoftDeleteParam deleteParams)
        {
            foreach (var apiRevisionId in deleteParams.apiRevisionIds)
            {
                await _apiRevisionsManager.RestoreAPIRevisionAsync(user: User, reviewId: deleteParams.reviewId, revisionId: apiRevisionId);
            }
        }

        /// <summary>
        /// Endpoint used by Client SPA toggling ViewdBy property
        /// </summary>
        /// <param name="apiRevisionId"></param>
        /// <param name="state"></param> true = viewed, false = not viewed
        /// <returns></returns>
        [HttpPost("{apiRevisionId}/toggleViewedBy", Name = "ToggleViewedBy")]
        public async Task<ActionResult<APIRevisionListItemModel>> ToggleViewedByAsync(string apiRevisionId, [FromQuery] bool state)
        {
            string userName = User.GetGitHubLogin();
            var apiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId);

            if (state)
            {
                apiRevision.ViewedBy.Add(userName);
            }
            else 
            {
                apiRevision.ViewedBy.Remove(userName);
            }

            await _apiRevisionsManager.UpdateAPIRevisionAsync(apiRevision);
            return new LeanJsonResult(apiRevision, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Endpoint used by Client SPA for Toggling APIRevision Approval
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        [HttpPost("{reviewId}/{apiRevisionId}", Name = "ToggleAPIRevisionApproval")]
        public async Task<ActionResult<APIRevisionListItemModel>> ToggleReviewApprovalAsync(string reviewId, string apiRevisionId)
        {
            (var updateReview, var apiRevision) = await _apiRevisionsManager.ToggleAPIRevisionApprovalAsync(User, reviewId, apiRevisionId);
            if (updateReview)
            {
                await _reviewManager.ToggleReviewApprovalAsync(User, reviewId, apiRevisionId);
            }
            return new LeanJsonResult(apiRevision, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Endpoint used by Client SPA for getting associated Pull Request
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        /// 
        [HttpGet("{reviewId}/{apiRevisionId}/associatedPullRequest", Name = "GetAssociatedPullRequest")]
        public async Task<IActionResult> GetAssociatedPullRequestAsync([FromRoute] string reviewId, [FromRoute] string apiRevisionId)
        {
            var pullRequests = await _pullRequestManager.GetPullRequestsModelAsync(reviewId, apiRevisionId);
            return new LeanJsonResult(pullRequests, StatusCodes.Status200OK);
        }
    }
}
