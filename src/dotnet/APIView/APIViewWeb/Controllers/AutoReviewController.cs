using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Util;
using ApiView;
using APIViewWeb.Filters;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Account;
using Octokit;

namespace APIViewWeb.Controllers
{
    [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]
    public class AutoReviewController : Controller
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly ICodeFileManager _codeFileManager;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ICommentsManager _commentsManager;
        private readonly ILogger<AutoReviewController> _logger;

        public AutoReviewController(IAuthorizationService authorizationService, ICodeFileManager codeFileManager,
            IReviewManager reviewManager, IAPIRevisionsManager apiRevisionManager, ICommentsManager commentsManager,
            ILogger<AutoReviewController> logger)
        {
            _authorizationService = authorizationService;
            _codeFileManager = codeFileManager;
            _apiRevisionsManager = apiRevisionManager;
            _commentsManager = commentsManager;
            _reviewManager = reviewManager;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult> UploadAutoReview([FromForm] IFormFile file, string label, bool compareAllRevisions = false)
        {
            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    using var memoryStream = new MemoryStream();
                    var codeFile = await _codeFileManager.CreateCodeFileAsync(originalName: file.FileName, fileStream: openReadStream,
                        runAnalysis: false, memoryStream: memoryStream);

                    var apiRevision = await CreateAutomaticRevisionAsync(codeFile: codeFile, label: label, originalName: file.FileName, memoryStream: memoryStream, compareAllRevisions);

                    if (apiRevision != null)
                    {
                        var reviewUrl = $"{this.Request.Scheme}://{this.Request.Host}/Assemblies/Review/{apiRevision.ReviewId}?revisionId={apiRevision.Id}";
                        return apiRevision.IsApproved ? Ok(reviewUrl) : StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl);
                    }
                };
            }
            // Return internal server error for any unknown error
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    
        public async Task<ActionResult> GetReviewStatus(string language, string packageName, string reviewId = null, bool? firstReleaseStatusOnly = null)
        {
            // This API is used by prepare release script to check if API review for a package is approved or not.
            // This caller script doesn't have artifact to submit and so it can't check using create review API
            // So it rely on approval status of latest revision of automatic review for the package
            // With new restriction of creating automatic review only from master branch or GA version, this should ensure latest revision
            // is infact the version intended to be released.

            ReviewListItemModel review = await _reviewManager.GetReviewAsync(packageName: packageName, language: language, isClosed: null);

            if (review != null)
            {
                APIRevisionListItemModel latestAutomaticApiRevisions = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic);
                
                // Return 200 OK for approved review and 201 for review in pending status
                if (firstReleaseStatusOnly != true && latestAutomaticApiRevisions != null && latestAutomaticApiRevisions.IsApproved)
                {
                    return Ok();
                }
                else 
                {
                    if (review.IsApproved)
                    {
                        return StatusCode(statusCode: StatusCodes.Status201Created);
                    }
                    // Return 202 to indicate package name is not approved
                    return StatusCode(statusCode: StatusCodes.Status202Accepted);
                }
            }
            throw new Exception("Review is not found for package " + packageName);
        }

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
            string project
            )
        {
            using var memoryStream = new MemoryStream();
            var codeFile = await _codeFileManager.GetCodeFileAsync(repoName: repoName, buildId: buildId, artifactName: artifactName,
                packageName: packageName, originalFileName: originalFilePath, codeFileName: reviewFilePath, originalFileStream: memoryStream,
                project: project);

            if (codeFile == null)
            {
                return StatusCode(statusCode: StatusCodes.Status204NoContent, $"API review code file for package {packageName} is not found in DevOps pipeline artifacts.");
            }
            var apiRevision = await CreateAutomaticRevisionAsync(codeFile: codeFile, label: label, originalName: originalFilePath, memoryStream: memoryStream, compareAllRevisions);
            if (apiRevision != null)
            {
                var reviewUrl = $"{this.Request.Scheme}://{this.Request.Host}/Assemblies/Review/{apiRevision.ReviewId}?revisionId={apiRevision.Id}";
                return apiRevision.IsApproved ? Ok(reviewUrl) : StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl);
            }
            // Return internal server error for any unknown error
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }

        private async Task<APIRevisionListItemModel> CreateAutomaticRevisionAsync(CodeFile codeFile, string label, string originalName, MemoryStream memoryStream, bool compareAllRevisions = false)
        {
            var createNewRevision = true;
            var review = await _reviewManager.GetReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language, isClosed: null);
            var apiRevision = default(APIRevisionListItemModel);
            var renderedCodeFile = new RenderedCodeFile(codeFile);
            IEnumerable<APIRevisionListItemModel> apiRevisions = new List<APIRevisionListItemModel>();

            if (review != null)
            {
                apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id);
                if (apiRevisions.Any())
                {
                    apiRevisions = apiRevisions.OrderByDescending(r => r.CreatedOn);

                    // Delete pending apiRevisions if it is not in approved state before adding new revision
                    // This is to keep only one pending revision since last approval or from initial review revision
                    var automaticRevisions = apiRevisions.Where(r => r.APIRevisionType == APIRevisionType.Automatic);
                    var automaticRevisionsQueue = new Queue<APIRevisionListItemModel>(automaticRevisions);
                    var latestAutomaticAPIRevision = automaticRevisionsQueue.Peek();
                    var comments = await _commentsManager.GetCommentsAsync(review.Id);

                    while (
                        automaticRevisionsQueue.Any() &&
                        !latestAutomaticAPIRevision.IsApproved &&
                        !await _apiRevisionsManager.AreAPIRevisionsTheSame(latestAutomaticAPIRevision, renderedCodeFile) &&
                        !comments.Any(c => latestAutomaticAPIRevision.Id == c.APIRevisionId))
                    {
                        await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(apiRevision: latestAutomaticAPIRevision, notes: "Deleted by Automatic Review Creation...");
                        latestAutomaticAPIRevision = automaticRevisionsQueue.Dequeue();
                    }

                    // We should compare against only latest revision when calling this API from scheduled CI runs
                    // But any manual pipeline run at release time should compare against all approved revisions to ensure hotfix release doesn't have API change
                    // If review surface doesn't match with any approved revisions then we will create new revision if it doesn't match pending latest revision

                    if (compareAllRevisions)
                    {
                        foreach (var approvedAPIRevision in automaticRevisions.Where(r => r.IsApproved))
                        {
                            if (await _apiRevisionsManager.AreAPIRevisionsTheSame(approvedAPIRevision, renderedCodeFile))
                            {
                                return approvedAPIRevision;
                            }
                        }
                    }

                    if (await _apiRevisionsManager.AreAPIRevisionsTheSame(latestAutomaticAPIRevision, renderedCodeFile))
                    {
                        apiRevision = latestAutomaticAPIRevision;
                        createNewRevision = false;
                    }
                }
            }
            else
            {
                review = await _reviewManager.CreateReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language, isClosed: false);
            }
            
            if (createNewRevision)
            {
                apiRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(userName: User.GetGitHubLogin(), reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic, label: label, memoryStream: memoryStream, codeFile: codeFile, originalName: originalName);
            }

            if (apiRevision != null)
            {
                if (!apiRevision.IsApproved && apiRevisions.Any())
                {
                    foreach (var apiRev in apiRevisions)
                    {
                        if (apiRev.IsApproved && await _apiRevisionsManager.AreAPIRevisionsTheSame(apiRev, renderedCodeFile))
                        {
                            await _apiRevisionsManager.ToggleAPIRevisionApprovalAsync(user: User, id: review.Id, apiRevision: apiRevision, notes: $"Approval Copied over from Revision with Id : {apiRev.Id}", approver: apiRev.Approvers.LastOrDefault());
                        }
                        break;
                    }
                }
            }
            return apiRevision;
        }
    }
}
