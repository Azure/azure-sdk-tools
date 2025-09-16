using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.LeanControllers;

[ApiController]
[Authorize("RequireTokenOrCookieAuthentication")]
[Route("api/reviews")]
public class ReviewsHybridAuthController : ControllerBase
{
    private readonly ICommentsManager _commentsManager;
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly IBlobCodeFileRepository _codeFileRepository;
    private readonly IEnumerable<LanguageService> _languageServices;

    public ReviewsHybridAuthController(ICommentsManager commentsManager,
        IBlobCodeFileRepository codeFileRepository,
        IAPIRevisionsManager reviewRevisionsManager,
        IEnumerable<LanguageService> languageServices)
    {
        _apiRevisionsManager = reviewRevisionsManager;
        _codeFileRepository = codeFileRepository;
        _commentsManager = commentsManager;
        _languageServices = languageServices;
    }

    ///<summary>
    ///Retrieve the Content (codeLines and Navigation) of a review
    ///</summary>
    ///<param name="reviewId"></param>
    ///<param name="activeApiRevisionId"></param>
    /// <param name="diffApiRevisionId"></param>
    ///<returns></returns>
    [Route("{reviewId}/content")]
    [HttpGet]
    public async Task<ActionResult<CodePanelData>> GetReviewContentAsync(string reviewId, [FromQuery] string activeApiRevisionId,
        [FromQuery] string diffApiRevisionId = null)
    {
        var activeAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, activeApiRevisionId);
        APIRevisionListItemModel diffAPIRevision = null;

        if (activeAPIRevision.IsDeleted)
        {
            return new LeanJsonResult(null, StatusCodes.Status204NoContent);
        }

        if (!string.IsNullOrEmpty(diffApiRevisionId))
        {
            diffAPIRevision = await _apiRevisionsManager.GetAPIRevisionAsync(User, diffApiRevisionId);

            if (diffAPIRevision.IsDeleted)
            {
                return new LeanJsonResult(null, StatusCodes.Status204NoContent);
            }
        }

        if (activeAPIRevision.Files[0].ParserStyle == ParserStyle.Tree)
        {
            var comments = await _commentsManager.GetCommentsAsync(reviewId, commentType: CommentType.APIRevision);
            var activeRevisionReviewCodeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(revisionId: activeAPIRevision.Id, codeFileId: activeAPIRevision.Files[0].FileId);

            if (activeRevisionReviewCodeFile.ContentGenerationInProgress)
            {
                var languageServices = LanguageServiceHelpers.GetLanguageService(activeAPIRevision.Language, _languageServices);
                return new LeanJsonResult("Content generation in progress", StatusCodes.Status202Accepted, languageServices.ReviewGenerationPipelineUrl);
            }

            var codePanelRawData = new CodePanelRawData()
            {
                activeRevisionCodeFile = activeRevisionReviewCodeFile,
                Comments = comments
            };

            if (diffAPIRevision != null)
            {
                codePanelRawData.diffRevisionCodeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(revisionId: diffAPIRevision.Id, codeFileId: diffAPIRevision.Files[0].FileId);
            }

            // Render the code files to generate UI token tree
            var result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }

        return new LeanJsonResult("Invalid APIRevision", StatusCodes.Status500InternalServerError);
    }

}
