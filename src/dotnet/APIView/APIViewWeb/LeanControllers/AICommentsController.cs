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

namespace APIViewWeb.LeanControllers
{
    [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]
    public class AICommentsController : BaseApiController
    {
        private readonly IAICommentsManager _aiCommentsManager;
        private readonly ILogger _logger;

        public AICommentsController(IAICommentsManager aiCommentsManager, ILogger<AICommentsController> logger)
        {
            _aiCommentsManager = aiCommentsManager;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult> CreateAICommentAsync(AICommentDTOForCreate aiCommentDTOForCreate)
        {
            try
            {
                var result = await _aiCommentsManager.CreateAICommentAsync(aiCommentDTOForCreate, User.GetGitHubLogin());
                _logger.LogInformation("New comment added to database");
                return StatusCode(statusCode: StatusCodes.Status201Created, result);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error: Failed to create AI Comment " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost]
        [Route("{id}")]
        public async Task<ActionResult> UpdateAICommentAsync(string id, AICommentDTO aiCommentDto)
        {
            try
            {
                var result = await _aiCommentsManager.UpdateAICommentAsync(id, aiCommentDto, User.GetGitHubLogin());
                return Ok(result);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error: Failed to update AI comment. " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet]
        [Route("{id}")]
        public async Task<ActionResult> GetAICommentAsync(string id)
        {
            try
            {
                var result = await _aiCommentsManager.GetAICommentAsync(id);
                return Ok(result);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error:  Failed to update AI comment. " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }

        }

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
                _logger.LogInformation("Error: Failed to delete AI comment " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet]
        [Route("search")]
        public async Task<ActionResult> SearchAICommentAsync(AICommentDTOForSearch aiCommentDTOForSearch)
        {
            try
            {
                var topResults = await _aiCommentsManager.SearchAICommentAsync(aiCommentDTOForSearch);
                return Ok(topResults);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error: Failed to retrieve AI comment" + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
