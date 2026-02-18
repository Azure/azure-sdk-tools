// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionManager;

        public ReviewController(IReviewManager reviewManager,
            IAPIRevisionsManager apiRevisionManager )
        {
            _reviewManager = reviewManager;
            _apiRevisionManager = apiRevisionManager;
        }

        [HttpGet]
        public async Task<ActionResult> UpdateApiReview(string repoName, string artifactPath, string buildId, string project = "internal", string metadataFile = null)
        {
            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(repoName, buildId, artifactPath, project, metadataFile);
            return Ok();
        }

        [HttpPost]
        public async Task<ActionResult> ApprovePackageName(string id)
        {
            await _reviewManager.ApproveReviewAsync(user: User, reviewId: id);
            return RedirectToPage("/Assemblies/Review",  new { id = id });
        }
    }
}
