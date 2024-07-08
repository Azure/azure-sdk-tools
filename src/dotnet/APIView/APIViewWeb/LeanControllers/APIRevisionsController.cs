using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using APIViewWeb.Managers.Interfaces;
using System;
using APIViewWeb.Managers;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using System.Linq;
using System.Collections.Generic;

namespace APIViewWeb.LeanControllers
{
    public class APIRevisionsController : BaseApiController
    {
        private readonly ILogger<APIRevisionsController> _logger;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IReviewManager _reviewManager;
        private readonly INotificationManager _notificationManager;


        public APIRevisionsController(ILogger<APIRevisionsController> logger,
            IReviewManager reviewManager,
            IAPIRevisionsManager apiRevisionsManager
,
            INotificationManager notificationManager)
        {
            _logger = logger;
            _apiRevisionsManager = apiRevisionsManager;
            _reviewManager = reviewManager;
            _notificationManager = notificationManager;
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
        /// Endpoint used by Client SPA for Requesting Reviewers
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="reviewers"></param>
        /// <returns></returns>
        
        [HttpPost("{reviewId}/{apiRevisionId}/reviewers", Name = "AddReviewers")]
        public async Task<ActionResult<APIRevisionListItemModel>> AddReviewersAsync(string reviewId, string apiRevisionId, [FromBody] HashSet<string> reviewers)
        {
            var apiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(apiRevisionId);
            var existingReviewers = apiRevision.AssignedReviewers;
            var newReviewers = reviewers.Where(reviewer => !existingReviewers.Any(existingReviewer => existingReviewer.AssingedTo == reviewer)).ToHashSet();
            var removedReviewers = existingReviewers.Where(existingReviewer => !reviewers.Contains(existingReviewer.AssingedTo)).Select(r => r.AssingedTo).ToHashSet();

            if (newReviewers.Any())
            {
                await _apiRevisionsManager.AssignReviewersToAPIRevisionAsync(User, apiRevisionId, newReviewers);
                await _notificationManager.NotifyApproversOfReview(User, apiRevisionId, newReviewers);
            }
            if (removedReviewers.Any())
            {
                await _apiRevisionsManager.RemoveReviewersFromAPIRevisionAsync(User, apiRevisionId, removedReviewers);
            }
            return new LeanJsonResult(apiRevision, StatusCodes.Status200OK);
        }
    }
}
