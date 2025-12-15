using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Filters;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Controllers
{
    public class AutoReviewController : Controller
    {
        private readonly ICodeFileManager _codeFileManager;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IAutoReviewService _autoReviewService;
        private readonly IConfiguration _configuration;
        private readonly IEnumerable<LanguageService> _languageServices;

        public AutoReviewController(ICodeFileManager codeFileManager,
            IReviewManager reviewManager, 
            IAPIRevisionsManager apiRevisionManager, 
            IAutoReviewService autoReviewService,
            IConfiguration configuration, 
            IEnumerable<LanguageService> languageServices)
        {
            _codeFileManager = codeFileManager;
            _apiRevisionsManager = apiRevisionManager;
            _autoReviewService = autoReviewService;
            _reviewManager = reviewManager;
            _configuration = configuration;
            _languageServices = languageServices;
        }

        // setReleaseTag param is set as true when request is originated from release pipeline to tag matching revision as released
        // regular CI pipeline will not send this flag in request
        [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]
        [HttpPost]
        public async Task<ActionResult> UploadAutoReview([FromForm] IFormFile file, string label, bool compareAllRevisions = false, string packageVersion = null, bool setReleaseTag = false, string packageType = null)
        {
            try
            {
                if (file != null)
                {
                    await using var openReadStream = file.OpenReadStream();
                    using var memoryStream = new MemoryStream();
                    var codeFile = await _codeFileManager.CreateCodeFileAsync(originalName: file.FileName, fileStream: openReadStream,
                        runAnalysis: false, memoryStream: memoryStream);

                    (ReviewListItemModel review, APIRevisionListItemModel apiRevision) = await _autoReviewService.CreateAutomaticRevisionAsync(user: User, codeFile: codeFile, label: label, originalName: file.FileName, memoryStream: memoryStream, packageType: packageType, compareAllRevisions: compareAllRevisions);
                    if (apiRevision != null)
                    {
                        apiRevision = await _apiRevisionsManager.UpdateRevisionMetadataAsync(apiRevision, packageVersion ?? codeFile.PackageVersion, label, setReleaseTag);
                        var reviewUrl = ManagerHelpers.ResolveReviewUrl(reviewId: apiRevision.ReviewId, apiRevisionId: apiRevision.Id, language: apiRevision.Language, configuration: _configuration, languageServices: _languageServices);

                        if (apiRevision.IsApproved)
                        {
                            return Ok(reviewUrl);
                        }
                        if (review.IsApproved)
                        {
                            return StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl);
                        }
                        return StatusCode(statusCode: StatusCodes.Status202Accepted, reviewUrl);
                    }
                }
            }
            catch (Exception e)
            {
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError, new 
                { 
                    error = "Failed to create API review",
                    message = e.Message,
                    exceptionType = e.GetType().Name,
                });
            }
  
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError, new
            {
                error = "Failed to create API review"

            });
        }
    
        public async Task<ActionResult> GetReviewStatus(string language, string packageName, string reviewId = null, bool? firstReleaseStatusOnly = null, string packageVersion = null)
        {
            // This API is used to get approval status of an API review revision. If a package version is passed then it will try to find a revision with exact package version match or revisions with same major and minor version.
            // If there is no matching revisions then it will return latest automatic revision details.
            // This is used by prepare release script and build pipeline to verify approval status.

            try
            {
                ReviewListItemModel review = await _reviewManager.GetReviewAsync(packageName: packageName, language: language, isClosed: null);

                if (review == null)
                {
                    return StatusCode(StatusCodes.Status404NotFound, "Review is not found for package " + packageName);
                }

                APIRevisionListItemModel apiRevision = null;
                    
                if (!string.IsNullOrEmpty(packageVersion))
                {
                    var apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId: review.Id, packageVersion: packageVersion, apiRevisionType: APIRevisionType.Automatic);
                    if (apiRevisions.Any())
                        apiRevision = apiRevisions.FirstOrDefault();
                }

                if (apiRevision == null)
                {
                    apiRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic);
                }
                    

                if(apiRevision == null)
                {
                    return StatusCode(StatusCodes.Status404NotFound, "Review is not found for package " + packageName);
                }

                // Return 200 OK for approved review and 201 for review in pending status
                if (firstReleaseStatusOnly != true && apiRevision != null && apiRevision.IsApproved)
                {
                    return Ok();
                }
                if (review.IsApproved)
                {
                    return StatusCode(statusCode: StatusCodes.Status201Created);
                }
                // Return 202 to indicate package name is not approved
                return StatusCode(statusCode: StatusCodes.Status202Accepted);

            }
            catch (Exception e)
            {
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError, new
                {
                    error = "Failed to get review status",
                    message = e.Message,
                    exceptionType = e.GetType().Name,
                    details = new
                    {
                        packageName,
                        language,
                        packageVersion,
                        reviewId
                    }
                });
            }
        }

        // setReleaseTag param is set as true when request is originated from release pipeline to tag matching revision as released
        // regular CI pipeline will not send this flag in request
        [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]
        [HttpGet]
        public async Task<ActionResult> CreateApiReview(
            string buildId,
            string artifactName,
            string originalFilePath,
            string reviewFilePath,
            string label,
            string repoName,
            string packageName,
            bool compareAllRevisions,
            string project,
            string packageVersion = null,
            bool setReleaseTag = false,
            string packageType = null
            )
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var codeFile = await _codeFileManager.GetCodeFileAsync(repoName: repoName, buildId: buildId, artifactName: artifactName,
                    packageName: packageName, originalFileName: originalFilePath, codeFileName: reviewFilePath, originalFileStream: memoryStream,
                    project: project);

                if (codeFile == null)
                {
                    return StatusCode(statusCode: StatusCodes.Status204NoContent, $"API review code file for package {packageName} is not found in DevOps pipeline artifacts.");
                }

                (ReviewListItemModel review, APIRevisionListItemModel apiRevision) = await _autoReviewService.CreateAutomaticRevisionAsync(user: User, codeFile: codeFile, label: label, originalName: originalFilePath, memoryStream: memoryStream, packageType: packageType, compareAllRevisions: compareAllRevisions);
                if (apiRevision == null)
                {
                    return StatusCode(statusCode: StatusCodes.Status500InternalServerError, "API revision creation returned null. This may indicate an issue with the code file parsing or revision creation process.");
                }

                apiRevision = await _apiRevisionsManager.UpdateRevisionMetadataAsync(apiRevision, packageVersion ?? codeFile.PackageVersion, label, setReleaseTag);
                var reviewUrl = ManagerHelpers.ResolveReviewUrl(reviewId: apiRevision.ReviewId, apiRevisionId: apiRevision.Id, language: apiRevision.Language, configuration: _configuration, languageServices: _languageServices);

                if (apiRevision.IsApproved)
                {
                    return Ok(reviewUrl);
                }
                if (review.IsApproved)
                {
                    return StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl);
                }

                return StatusCode(statusCode: StatusCodes.Status202Accepted, reviewUrl);

            }
            catch (Exception e)
            {
                return StatusCode(statusCode: StatusCodes.Status500InternalServerError, new
                {
                    error = "Failed to create API review from DevOps artifacts",
                    message = e.Message,
                    details = new
                    {
                        buildId,
                        artifactName,
                        packageName,
                        originalFilePath,
                        reviewFilePath,
                        label
                    }
                });
            }
        }
    }
}
