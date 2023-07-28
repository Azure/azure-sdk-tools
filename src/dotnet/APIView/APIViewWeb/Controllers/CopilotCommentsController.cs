using System.Threading.Tasks;
using APIViewWeb.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Controllers
{
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
        public async Task<ActionResult> CreateDocument(string badCode, string language, string goodCode = null, string comment = null, string[] guidelineIds = null)
        {
            if (badCode != null)
            {
                var id = await _copilotManager.CreateDocumentAsync(User.GetGitHubLogin(), badCode, goodCode, language, comment, guidelineIds);
                _logger.LogInformation("Added a new document to database.");
                return StatusCode(statusCode: StatusCodes.Status201Created, id);
            }
            _logger.LogInformation("Request does not have the required badCode field for insert.");
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }

        [HttpPut]
        public async Task<ActionResult> UpdateDocument(string id, string badCode = null, string language = null, string goodCode = null, string comment = null, string[] guidelineIds = null)
        {
            if (id != null)
            {
                var result = await _copilotManager.UpdateDocumentAsync(User.GetGitHubLogin(), id, badCode, goodCode, language, comment, guidelineIds);
                if (result.IsAcknowledged)
                {
                    _logger.LogInformation("Found existing document with id. Updating document.");
                    return Ok();
                }
                _logger.LogInformation("Failed to update.");
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }
            _logger.LogInformation("Request does not have the required id field for update.");
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }

        [HttpGet]
        public async Task<ActionResult> GetDocument(string id)
        {
            if (id != null)
            {
                var document = await _copilotManager.GetDocumentAsync(id);
                if (document != null)
                {
                    return Ok(document);
                } else
                {
                    _logger.LogInformation("No document with this id exists in the database.");
                    return StatusCode(statusCode: StatusCodes.Status404NotFound);
                }
            }
            _logger.LogInformation("Request does not have the required id field.");
            return StatusCode(statusCode: StatusCodes.Status400BadRequest);
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteDocument(string id)
        {
            if (id != null)
            {
                var updateResult = await _copilotManager.DeleteDocumentAsync(User.GetGitHubLogin(), id);
                if (updateResult.IsAcknowledged)
                {
                    return Ok();
                }
                _logger.LogInformation("Failed to delete document.");
                return StatusCode(statusCode: StatusCodes.Status404NotFound);
            }
            _logger.LogInformation("Request does not have the required id field.");
            return StatusCode(statusCode: StatusCodes.Status400BadRequest); 
        }
    }
}
