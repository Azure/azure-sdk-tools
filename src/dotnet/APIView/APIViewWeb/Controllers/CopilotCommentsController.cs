using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using APIViewWeb.Managers;
using System;
using Microsoft.Azure.Cosmos;
using APIViewWeb.Filters;

namespace APIViewWeb.Controllers
{
    [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]

    public class CopilotCommentsController : Controller
    {
        private readonly ICopilotCommentsManager _copilotManager;
        private readonly ILogger _logger;

        public CopilotCommentsController(ICopilotCommentsManager copilotManager, ILogger<CopilotCommentsController> logger)
        {
            _copilotManager = copilotManager;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult> CreateDocument(string language, string badCode, string goodCode = null, string comment = null, string guidelineIds = null)
        {
            if (badCode == null || language == null)
            {
                _logger.LogInformation("Request does not have the required badCode or language fields for CREATE.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var result = await _copilotManager.CreateDocumentAsync(language, badCode, goodCode, comment, guidelineIds, User.GetGitHubLogin());
                _logger.LogInformation("Added a new document to database.");
                return StatusCode(statusCode: StatusCodes.Status201Created, result);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error: unsuccessful CREATE request. " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut]
        public async Task<ActionResult> UpdateDocument(string id, string language, string badCode = null, string goodCode = null, string comment = null, string guidelineIds = null)
        {
            if (id == null || language == null)
            {
                _logger.LogInformation("Request does not have the required ID or language fields for UPDATE.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var result = await _copilotManager.UpdateDocumentAsync(id, language, badCode, goodCode, comment, guidelineIds, User.GetGitHubLogin());
                return Ok(result);
            }
            catch (CosmosException)
            {
                _logger.LogInformation("Could not find a match for the given id and language combination.");
                return StatusCode(statusCode: StatusCodes.Status404NotFound);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error: unsuccessful UPDATE request. " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }

        }

        [HttpGet]
        public async Task<ActionResult> GetDocument(string id, string language)
        {
            if (id == null || language == null)
            {
                _logger.LogInformation("Request does not have the required ID or language fields for GET.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var result = await _copilotManager.GetDocumentAsync(id, language);
                return Ok(result);
            }
            catch (CosmosException)
            {
                _logger.LogInformation("Could not find a match for this id and language combination exists in the database.");
                return StatusCode(statusCode: StatusCodes.Status404NotFound);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error: unsuccessful GET request. " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }

        }

        [HttpDelete]
        public async Task<ActionResult> DeleteDocument(string id, string language)
        {
            if (id == null || language == null)
            {
                _logger.LogInformation("Request does not have the required ID or language fields for DELETE.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await _copilotManager.DeleteDocumentAsync(id, language, User.GetGitHubLogin());
                return StatusCode(statusCode: StatusCodes.Status204NoContent);
            }
            catch (CosmosException)
            {
                _logger.LogInformation("Could not find a match for this id and language combination exists in the database.");
                return StatusCode(statusCode: StatusCodes.Status404NotFound);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error: unsuccessful DELETE request. " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet]
        public async Task<ActionResult> SearchDocument(string language, string code, float threshold, int? limit = null)
        {
            if (language == null || code == null)
            {
                _logger.LogInformation("Request does not have the required code or language fields for SEARCH.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var topResults = await _copilotManager.SearchDocumentsAsync(language, code, threshold, limit ?? 5);
                return Ok(topResults);
            }
            catch (Exception err)
            {
                _logger.LogInformation("Error: unsuccessful SEARCH request: " + err.Message);
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
