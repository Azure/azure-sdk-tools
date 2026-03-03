using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using APIViewWeb.Extensions;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Services;
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
        private readonly ICopilotAuthenticationService _copilotAuthService;

        public APIRevisionsController(ILogger<APIRevisionsController> logger,
            IReviewManager reviewManager, IPullRequestManager pullRequestManager,
            IAPIRevisionsManager apiRevisionsManager, INotificationManager notificationManager, IConfiguration configuration,
            IHubContext<SignalRHub> signalRHub, IHttpClientFactory httpClientFactory, ICopilotAuthenticationService copilotAuthService)
        {
            _logger = logger;
            _apiRevisionsManager = apiRevisionsManager;
            _reviewManager = reviewManager;
            _notificationManager = notificationManager;
            _signalRHubContext = signalRHub;
            _copilotEndpoint = configuration["CopilotServiceEndpoint"];
            _httpClientFactory = httpClientFactory;
            _copilotAuthService = copilotAuthService;
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
        /// Get APIRevisions with the same Cross Language Id
        /// </summary>
        /// <param name="crossLanguageId"></param>
        /// <param name="apiRevisionType"></param>
        /// <returns></returns>
        [HttpGet("{crossLanguageId}/crosslanguage", Name = "GetCrossLanguageAPIRevision")]
        public async Task<ActionResult<APIRevisionListItemModel>> GetCrossLanguageAPIRevision(string crossLanguageId, APIRevisionType apiRevisionType = APIRevisionType.All)
        {
            var results = new List<APIRevisionListItemModel>();
            foreach (var language in LanguageServiceHelpers.SupportedLanguages)
            {
                results.AddRange(await _apiRevisionsManager.GetCrossLanguageAPIRevisionsAsync(crossLanguageId: crossLanguageId, language: language, apiRevisionType: apiRevisionType));
            }
            var groupResults = results.GroupBy(r => r.Language).Select(g => new
            {
               Label = g.Key,
               Items = g.ToList()
            });
            return new LeanJsonResult(groupResults, StatusCodes.Status200OK);
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
                        
                        var request = new HttpRequestMessage(HttpMethod.Get, pollUrl);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _copilotAuthService.GetAccessTokenAsync());
                        
                        HttpResponseMessage response = await client.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                        var pollResponseString = await response.Content.ReadAsStringAsync();
                        var pollResponse = JsonSerializer.Deserialize<AIReviewJobPolledResponseModel>(pollResponseString);
                        if (pollResponse.Status != "InProgress")
                        {
                            apiRevision.CopilotReviewInProgress = false;
                            if (string.Equals(pollResponse.Status, "Success", StringComparison.OrdinalIgnoreCase))
                            {
                                apiRevision.HasAutoGeneratedComments = true;
                            }
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
        /// Endpoint used by Client SPA for Toggling APIRevision Approval
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="approvalRequest"></param>
        /// <returns></returns>
        [HttpPost("{reviewId}/{apiRevisionId}", Name = "ToggleAPIRevisionApproval")]
        public async Task<ActionResult<APIRevisionListItemModel>> ToggleReviewApprovalAsync(string reviewId, string apiRevisionId, [FromBody] ApprovalRequest approvalRequest)
        {
            APIRevisionListItemModel currentAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, apiRevisionId);
            if (currentAPIRevision.IsApproved == approvalRequest.Approve)
            {
                return new LeanJsonResult(currentAPIRevision, StatusCodes.Status200OK);
            }

            (bool updateReview, APIRevisionListItemModel apiRevision) = await _apiRevisionsManager.ToggleAPIRevisionApprovalAsync(User, reviewId, apiRevisionId);
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
            var currentApiRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, apiRevisionId);
            var existingReviewers = new HashSet<string>(
                currentApiRevision.AssignedReviewers.Select(assignment => assignment.AssingedTo),
                StringComparer.OrdinalIgnoreCase);

            var apiRevision = await _apiRevisionsManager.UpdateAPIRevisionReviewersAsync(User, apiRevisionId, reviewers);

            var newlyAddedReviewers = reviewers
                .Where(reviewer => !existingReviewers.Contains(reviewer))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            await _notificationManager.NotifyAssignedReviewersAsync(User, apiRevisionId, newlyAddedReviewers);

            return new LeanJsonResult(apiRevision, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Endpoint used by Client SPA for getting the quality score of an API Revision
        /// </summary>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        [HttpGet("{apiRevisionId}/qualityScore", Name = "GetQualityScore")]
        public async Task<ActionResult<ReviewQualityScore>> GetQualityScoreAsync(string apiRevisionId)
        {
            try
            {
                var result = await _apiRevisionsManager.GetReviewQualityScoreAsync(apiRevisionId);
                return new LeanJsonResult(result, StatusCodes.Status200OK);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting quality score: " + ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
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
