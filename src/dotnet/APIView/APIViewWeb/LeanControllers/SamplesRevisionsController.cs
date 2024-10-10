using Microsoft.Extensions.Logging;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Extensions;

namespace APIViewWeb.LeanControllers
{
    public class SamplesRevisionsController : BaseApiController
    {
        private readonly ILogger<SamplesRevisionsController> _logger;
        private readonly ISamplesRevisionsManager _samplesRevisionsManager;
        private readonly ICommentsManager _commentsManager;

        public SamplesRevisionsController(ILogger<SamplesRevisionsController> logger,
            ISamplesRevisionsManager samplesRevisionsManager, ICommentsManager commentsManager)
        {
            _logger = logger;
            _samplesRevisionsManager = samplesRevisionsManager;
            _commentsManager = commentsManager;
        }

        /// <summary>
        /// Get the APIRevisions for a Review filtered by query parameters
        /// </summary>
        /// <param name="reviewId"></param>
        /// <returns></returns>
        [HttpGet("{reviewId}/latest", Name = "GetSampleRevision")]
        public async Task<ActionResult<SamplesRevisionModel>> GetLatestSampleRevisionAsync(string reviewId)
        {
            var result = await _samplesRevisionsManager.GetLatestSampleRevisionsAsync(reviewId: reviewId);
            if (result != null)
            {
                return new LeanJsonResult(result, StatusCodes.Status200OK);
            }
            else
            {
                return new LeanJsonResult("No SamplesRevision found", StatusCodes.Status404NotFound);
            }
        }


        /// <summary>
        /// Endpoint used by Client SPA for listing samples revisions.
        /// </summary>
        /// <param name="pageParams"></param>
        /// <param name="filterAndSortParams"></param>
        /// <returns></returns>
        [HttpPost(Name = "GetSamplesRevisions")]
        public async Task<ActionResult<PagedList<SamplesRevisionModel>>> GetSamplesRevisionsAsync([FromQuery] PageParams pageParams, [FromBody] FilterAndSortParams filterAndSortParams)
        {
            var result = await _samplesRevisionsManager.GetSamplesRevisionsAsync(User, pageParams, filterAndSortParams);
            Response.AddPaginationHeader(new PaginationHeader(result.NoOfItemsRead, result.PageSize, result.TotalCount));
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }


        /// <summary>
        /// Create usage sample revision
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="usageSampleAPIParam"></param>
        /// <returns></returns>
        [HttpPost("{reviewId}/create", Name = "CreateUsageSample")]
        public async Task<ActionResult<SamplesRevisionModel>> CreateUsageSampleAsync(string reviewId, [FromForm] UsageSampleAPIParam usageSampleAPIParam)
        {
            if (!string.IsNullOrEmpty(usageSampleAPIParam.Content))
            {
                var samplesRevision = await _samplesRevisionsManager.UpsertSamplesRevisionsAsync(User, reviewId, usageSampleAPIParam.Content, usageSampleAPIParam.Title);
                return new LeanJsonResult(samplesRevision, StatusCodes.Status200OK);
            } 
            else if (usageSampleAPIParam.File != null)
            {
                var samplesRevision = await _samplesRevisionsManager.UpsertSamplesRevisionsAsync(User, reviewId, usageSampleAPIParam.File.OpenReadStream(), usageSampleAPIParam.Title, usageSampleAPIParam.File.FileName);
                return new LeanJsonResult(samplesRevision, StatusCodes.Status200OK);
            }
            else
            {
                return BadRequest();
            }
        }

        /// <summary>
        /// Update usage sample revision
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="sampleRevisionId"></param>
        /// <param name="usageSampleAPIParam"></param>
        /// <returns></returns>
        [HttpPatch("{reviewId}/update", Name = "UpdateUsageSample")]
        public async Task UpdateUsageSampleAsync(string reviewId, string sampleRevisionId, [FromForm] UsageSampleAPIParam usageSampleAPIParam)
        {
            await _samplesRevisionsManager.UpdateSamplesRevisionAsync(User, reviewId, sampleRevisionId, usageSampleAPIParam.Content, usageSampleAPIParam.Title);
        }

        /// <summary>
        /// Endpoint used by Client SPA for Deleting Usage Sample.
        /// </summary>
        /// <param name="deleteParams"></param>
        /// <returns></returns>
        [HttpPut("delete", Name = "DeleteUsageSample")]
        public async Task DeleteUsageSampleAsync([FromBody] SamplesRevisionSoftDeleteParam deleteParams)
        {
            foreach (var revisionId in deleteParams.samplesRevisionIds)
            {
                await _samplesRevisionsManager.DeleteSamplesRevisionAsync(User, deleteParams.reviewId, revisionId);
            }
        }

        [Route("{reviewId}/content")]
        [HttpGet]
        public async Task<ActionResult<string>> GetSamplesContentAsync(string reviewId, [FromQuery] string activeSamplesRevisionId)
        {
            var activeSamplesRevision = await _samplesRevisionsManager.GetSamplesRevisionAsync(reviewId, activeSamplesRevisionId);
            if (activeSamplesRevision != null)
            {
                string samplesContent = await _samplesRevisionsManager.GetSamplesRevisionContentAsync(activeSamplesRevision.OriginalFileId);
                return new LeanJsonResult(samplesContent, StatusCodes.Status200OK);
            }
            return new LeanJsonResult("SamplesRevision NotFound", StatusCodes.Status404NotFound);
        }
    }
}
