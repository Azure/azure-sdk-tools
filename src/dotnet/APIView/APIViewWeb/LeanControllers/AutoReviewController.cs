using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.LeanControllers;

[ApiController]
[Authorize("RequireTokenAuthentication")]
[Route("autoreview")]
public class AutoReviewController : ControllerBase
{
    private readonly ICodeFileManager _codeFileManager;
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly IAutoReviewService _autoReviewService;
    private readonly IEnumerable<LanguageService> _languageServices;
    private readonly IConfiguration _configuration;

    public AutoReviewController(ICodeFileManager codeFileManager, 
        IAPIRevisionsManager apiRevisionsManager,
        IAutoReviewService autoReviewService,
        IEnumerable<LanguageService> languageServices,
        IConfiguration configuration)
    {
        _codeFileManager = codeFileManager;
        _apiRevisionsManager = apiRevisionsManager;
        _autoReviewService = autoReviewService;
        _languageServices = languageServices;
        _configuration = configuration;
    }

    // setReleaseTag param is set as true when request is originated from release pipeline to tag matching revision as released
    // regular CI pipeline will not send this flag in request
    [HttpPost("upload")]
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
            error = "Failed to create API review. No file provided."

        });
    }


    // setReleaseTag param is set as true when request is originated from release pipeline to tag matching revision as released
    // regular CI pipeline will not send this flag in request
    [HttpPost("create")]
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
        string packageType = null,
        string sourceBranch = null)
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

            (ReviewListItemModel review, APIRevisionListItemModel apiRevision) = await _autoReviewService.CreateAutomaticRevisionAsync(user: User, codeFile: codeFile, label: label, originalName: originalFilePath, memoryStream: memoryStream, packageType: packageType, compareAllRevisions: compareAllRevisions, sourceBranch: sourceBranch);
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
