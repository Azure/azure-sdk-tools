// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using APIViewWeb.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IReviewManager _reviewManager;
        private readonly ILogger _logger;

        public ReviewController(IReviewManager reviewManager, ILogger<ReviewController> logger)
        {
            _reviewManager = reviewManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult> UpdateApiReview(string repoName, string artifactPath, string buildId, string project = "internal")
        {
            await _reviewManager.UpdateReviewCodeFiles(repoName, buildId, artifactPath, project);
            return Ok();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task GenerateAIReview(string reviewId, string revisionId)
        {
            await _reviewManager.GenerateAIReview(reviewId, revisionId);
        }

        [HttpPost]
        public async Task<ActionResult> ApprovePackageName(string id)
        {
            await _reviewManager.ApprovePackageNameAsync(User, id);
            return RedirectToPage("/Assemblies/Review",  new { id = id });
        }
    }
}
