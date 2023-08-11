using System.Text.Json;
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
            if (badCode == null)
            {
                _logger.LogInformation("Request does not have the required badCode field for CREATE.");
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }

            var id = await _copilotManager.CreateDocumentAsync(User.GetGitHubLogin(), badCode, goodCode, language, comment, guidelineIds);
            _logger.LogInformation("Added a new document to database.");
            return StatusCode(statusCode: StatusCodes.Status201Created, id);

        }

        [HttpPut]
        public async Task<ActionResult> UpdateDocument(string id, string badCode = null, string language = null, string goodCode = null, string comment = null, string[] guidelineIds = null)
        {
            if (id == null)
            {
                _logger.LogInformation("Request does not have the required ID field for UPDATE.");
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
            }

            var result = await _copilotManager.UpdateDocumentAsync(User.GetGitHubLogin(), id, badCode, goodCode, language, comment, guidelineIds);
            if (result.ModifiedCount > 0)
            {
                _logger.LogInformation("Found existing document with ID. Updating document.");
                var document = await _copilotManager.GetDocumentAsync(id);
                return Ok(document);
            } else
            {
                _logger.LogInformation("Could not find a match for the given ID.");
                return StatusCode(statusCode: StatusCodes.Status404NotFound);
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetDocument(string id)
        {
            if (id == null)
            {
                _logger.LogInformation("Request does not have the required ID field for GET.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

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

        [HttpDelete]
        public async Task<ActionResult> DeleteDocument(string id)
        {
            if (id == null)
            {
                _logger.LogInformation("Request does not have the required ID field for DELETE.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            await _copilotManager.DeleteDocumentAsync(User.GetGitHubLogin(), id);
            return StatusCode(statusCode: StatusCodes.Status204NoContent);
        }
    }
}
