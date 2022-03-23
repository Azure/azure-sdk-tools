// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Filters;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]
    public class AutoReviewController : Controller
    {
        private readonly ReviewManager _reviewManager;
        private readonly ILogger _logger;

        public AutoReviewController(ReviewManager reviewManager, ILogger<AutoReviewController> logger)
        {
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
                    var reviewRevision = await _reviewManager.CreateMasterReviewAsync(User, file.FileName, label, openReadStream, compareAllRevisions);
                    if(reviewRevision != null)
                    {
                        var reviewUrl = $"{this.Request.Scheme}://{this.Request.Host}/Assemblies/Review/{reviewRevision.Review.ReviewId}";
                        //Return 200 OK if last revision is approved and 201 if revision is not yet approved.
                        var result = reviewRevision.IsApproved ? Ok(reviewUrl) : StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl);
                        return result;
                    }
                }
            }
            // Return internal server error for any unknown error
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }

        [HttpGet]
        public async Task<ActionResult> GetReviewStatus(string language, string packageName)
        {
            // This API is used by prepare release script to check if API review for a package is approved or not.
            // This caller script doesn't have artifact to submit and so it can't check using create review API
            // So it rely on approval status of latest revision of automatic review for the package
            // With new restriction of creating automatic review only from master branch or GA version, this should ensure latest revision
            // is infact the version intended to be released.
            var reviews = await _reviewManager.GetReviewsAsync(false, language, packageName: packageName, ReviewType.Automatic);
            var review = reviews.FirstOrDefault();
            if (review != null)
            {
                _logger.LogInformation("Found review ID " + review.ReviewId + " for package " + packageName);
                // Return 200 OK for approved review and 201 for review in pending status
                return review.Revisions.LastOrDefault().IsApproved ? Ok() : StatusCode(statusCode: StatusCodes.Status201Created);
            }

            throw new Exception("Automatic review is not found for package " + packageName);
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
            bool compareAllRevisions
            )
        {
            var reviewRevision = await _reviewManager.CreateApiReview(User, buildId, artifactName, originalFilePath, label, repoName, packageName, reviewFilePath, compareAllRevisions);
            if (reviewRevision != null)
            {
                var reviewUrl = $"{this.Request.Scheme}://{this.Request.Host}/Assemblies/Review/{reviewRevision.Review.ReviewId}";
                //Return 200 OK if last revision is approved and 201 if revision is not yet approved.
                return reviewRevision.IsApproved ? Ok(reviewUrl) : StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl);
            }
            // Return internal server error for any unknown error
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
