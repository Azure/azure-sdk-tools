using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Runtime;
using APIViewWeb.Extensions;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers
{
    public class APIRevisionsController : BaseApiController
    {
        private readonly ILogger<APIRevisionsController> _logger;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IReviewManager _reviewManager;
        private readonly INotificationManager _notificationManager;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly string _copilotEndpoint;
        private readonly IHttpClientFactory _httpClientFactory;

        public APIRevisionsController(ILogger<APIRevisionsController> logger,
            IReviewManager reviewManager, IPullRequestManager pullRequestManager,
            IAPIRevisionsManager apiRevisionsManager, INotificationManager notificationManager, IConfiguration configuration,
            IHubContext<SignalRHub> signalRHub, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _apiRevisionsManager = apiRevisionsManager;
            _reviewManager = reviewManager;
            _notificationManager = notificationManager;
            _signalRHubContext = signalRHub;
            _copilotEndpoint = configuration["CopilotServiceEndpoint"];
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Get the APIRevisions for a Review filtered by query parameters
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionType"></param>
        /// <returns></returns>
        [HttpGet("{reviewId}/latest", Name = "GetAPIRevision")]
        public async Task<ActionResult<APIRevisionListItemModel>> GetLatestAPIRevisionAsync(string reviewId, APIRevisionType apiRevisionType = APIRevisionType.All)
        {
            var result = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId: reviewId, apiRevisionType: apiRevisionType);
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Endpoint used by Client SPA for listing API Revisions.
        /// </summary>
        /// <param name="pageParams"></param>
        /// <param name="filterAndSortParams"></param>
        /// <returns></returns>
        [HttpPost(Name = "GetAPIRevisions")]
        public async Task<ActionResult<PagedList<APIRevisionListItemModel>>> GetAPIRevisionsAsync([FromQuery] PageParams pageParams, [FromBody] FilterAndSortParams filterAndSortParams)
        {
            var result = await _apiRevisionsManager.GetAPIRevisionsAsync(User, pageParams, filterAndSortParams);
            if (filterAndSortParams.APIRevisionIds != null && filterAndSortParams.APIRevisionIds.Any())
            {
                foreach (var apiRevisionId in filterAndSortParams.APIRevisionIds)
                {
                    var apiRevision = result.FirstOrDefault(r => r.Id == apiRevisionId);
                    if (apiRevision != null && apiRevision.CopilotReviewInProgress)
                    {
                        // If Copilot review is in progress let verify that this is not due to a dropped background job
                        var pollUrl = $"{_copilotEndpoint}/api-review/{apiRevision.CopilotReviewJobId}";
                        var client = _httpClientFactory.CreateClient();
                        var response = await client.GetAsync(pollUrl);
                        response.EnsureSuccessStatusCode();
                        var pollResponseString = await response.Content.ReadAsStringAsync();
                        var pollResponse = JsonSerializer.Deserialize<AIReviewJobPolledResponseModel>(pollResponseString);
                        if (pollResponse.Status != "InProgress")
                        {
                            apiRevision.CopilotReviewInProgress = false;
                            var index = result.FindIndex(r => r.Id == apiRevisionId);
                            if (index >= 0)
                            {
                                result[index] = apiRevision;
                            }
                            _ = _apiRevisionsManager.UpdateAPIRevisionAsync(apiRevision); // no need to await this
                        }
                    }
                }
            }
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
                var updatedReview = await _reviewManager.ToggleReviewApprovalAsync(User, reviewId, apiRevisionId);
                await _signalRHubContext.Clients.All.SendAsync("ReviewUpdated", updatedReview);
            }
            await _signalRHubContext.Clients.All.SendAsync("APIRevisionUpdated", apiRevision);
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
            var apiRevision = await _apiRevisionsManager.UpdateAPIRevisionReviewersAsync(User, apiRevisionId, reviewers);
            await _notificationManager.NotifyApproversOfReview(User, apiRevisionId, reviewers);

            return new LeanJsonResult(apiRevision, StatusCodes.Status200OK);
        }

        [HttpPost("{reviewId}/generateReview", Name = "GenerateAIReview")]
        public async Task<ActionResult<int>> GenerateAIReview(string reviewId, [FromQuery]string activeApiRevisionId, [FromQuery]string diffApiRevisionId = null)
        {
            try
            {
                await _reviewManager.GenerateAIReview(User, reviewId, activeApiRevisionId, diffApiRevisionId);
                return Accepted();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating AI review " + ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
