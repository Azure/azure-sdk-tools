using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using APIViewWeb.Managers;
using System;
using Microsoft.Azure.Cosmos;
using APIViewWeb.Filters;
using APIViewWeb.LeanModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using APIViewWeb.Helpers;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace APIViewWeb.LeanControllers
{
    [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]
    public class AICommentsController : BaseApiController
    {
        private readonly IAICommentsManager _aiCommentsManager;
        private readonly TelemetryClient _telemetryClient;

        public AICommentsController(IAICommentsManager aiCommentsManager, TelemetryClient telemetryClient)
        {
            _aiCommentsManager = aiCommentsManager;
            _telemetryClient = telemetryClient;
        }
        /// <summary>
        /// Create AI Comment
        /// </summary>
        /// <param name="aiCommentDTOForCreate"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult> CreateAICommentAsync(AICommentDTOForCreate aiCommentDTOForCreate)
        {
            try
            {
                var result = await _aiCommentsManager.CreateAICommentAsync(aiCommentDTOForCreate, User.GetGitHubLogin());
                _telemetryClient.TrackTrace("New comment added to database", SeverityLevel.Information);
                return new LeanJsonResult(result, StatusCodes.Status201Created);
            }
            catch (Exception err)
            {
                _telemetryClient.TrackTrace("Error: Failed to create AI Comment " + err.Message, SeverityLevel.Information);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        /// <summary>
        /// Update AI Comment with specific Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="aiCommentDto"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("{id}")]
        public async Task<ActionResult> UpdateAICommentAsync(string id, AICommentDTO aiCommentDto)
        {
            try
            {
                var result = await _aiCommentsManager.UpdateAICommentAsync(id, aiCommentDto, User.GetGitHubLogin());
                return new LeanJsonResult(result, StatusCodes.Status200OK);
            }
            catch (Exception err)
            {
                _telemetryClient.TrackTrace("Error: Failed to update AI comment. " + err.Message, SeverityLevel.Information);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        /// <summary>
        /// Get AI Comment with specific Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{id}")]
        public async Task<ActionResult> GetAICommentAsync(string id)
        {
            try
            {
                var result = await _aiCommentsManager.GetAICommentAsync(id);
                return new LeanJsonResult(result, StatusCodes.Status200OK);
            }
            catch (Exception err)
            {
                _telemetryClient.TrackTrace("Error:  Failed to update AI comment. " + err.Message, SeverityLevel.Information);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }

        }

        /// <summary>
        /// Delete AI Comment with specific Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("{id}")]
        public async Task<ActionResult> DeleteAICommentAsync(string id)
        {
            try
            {
                await _aiCommentsManager.DeleteAICommentAsync(id, User.GetGitHubLogin());
                return StatusCode(statusCode: StatusCodes.Status204NoContent);
            }
            catch (Exception err)
            {
                _telemetryClient.TrackTrace("Error: Failed to delete AI comment " + err.Message, SeverityLevel.Information);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Search AI Comments
        /// </summary>
        /// <param name="aiCommentDTOForSearch"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("search")]
        public async Task<ActionResult> SearchAICommentAsync([FromQuery]AICommentDTOForSearch aiCommentDTOForSearch)
        {
            try
            {
                var topResults = await _aiCommentsManager.SearchAICommentAsync(aiCommentDTOForSearch);
                return new LeanJsonResult(topResults, StatusCodes.Status200OK);
            }
            catch (Exception err)
            {
                _telemetryClient.TrackTrace("Error: Failed to retrieve AI comment" + err.Message, SeverityLevel.Information);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
