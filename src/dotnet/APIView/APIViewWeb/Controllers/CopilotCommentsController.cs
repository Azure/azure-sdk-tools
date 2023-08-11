using System.Threading.Tasks;
using APIViewWeb.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Controllers
{
    //[TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]

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
        public async Task<ActionResult> CreateDocument(string language, string badCode, string goodCode = null, string comment = null, string[] guidelineIds = null)
        {
            if (badCode == null || language == null)
            {
                _logger.LogInformation("Request does not have the required badCode or language fields for CREATE.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            var id = await _copilotManager.CreateDocumentAsync(language, badCode, goodCode, comment, guidelineIds, User.GetGitHubLogin());
            _logger.LogInformation("Added a new document to database.");
            return StatusCode(statusCode: StatusCodes.Status201Created, id);
        }

        [HttpPut]
        public async Task<ActionResult> UpdateDocument(string id, string language, string badCode = null, string goodCode = null, string comment = null, string[] guidelineIds = null)
        {
            if (id == null || language == null)
            {
                _logger.LogInformation("Request does not have the required ID or language fields for UPDATE.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await _copilotManager.UpdateDocumentAsync(id, language, badCode, goodCode, comment, guidelineIds, User.GetGitHubLogin());
            if (result != null)
            {
                _logger.LogInformation("Found existing document with ID. Updating document.");
                return Ok(result);
            } else
            {
                _logger.LogInformation("Could not find a match for the given ID.");
                return StatusCode(statusCode: StatusCodes.Status404NotFound);
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

            var document = await _copilotManager.GetDocumentAsync(id, language);
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
        public async Task<ActionResult> DeleteDocument(string id, string language)
        {
            if (id == null || language == null)
            {
                _logger.LogInformation("Request does not have the required ID or language fields for DELETE.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            await _copilotManager.DeleteDocumentAsync(id, language, User.GetGitHubLogin());
            return StatusCode(statusCode: StatusCodes.Status204NoContent);
        }

        [HttpGet]
        public async Task<ActionResult> SearchDocument(string language, string code, float threshold, int limit)
        {
            if (code == null || language == null)
            {
                _logger.LogInformation("Request does not have the required code or language fields for SEARCH.");
                return StatusCode(statusCode: StatusCodes.Status400BadRequest);
            }

            var document = await _copilotManager.SearchDocumentsAsync(language, code, threshold, limit);
            return Ok(document);
        }
    }
}
