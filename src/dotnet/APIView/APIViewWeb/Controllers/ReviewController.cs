// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class UpdateApiReviewRequest
    {
        public string RepoName { get; set; }
        public string ArtifactPath { get; set; }
        public string BuildId { get; set; }
        public string Project { get; set; } = "internal";
        public string MetadataFile { get; set; }
    }

    [Authorize("RequireTokenAuthentication")]
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

        [HttpPost]
        public async Task<ActionResult> UpdateApiReview([FromBody] UpdateApiReviewRequest request)
        {
            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(request.RepoName, request.BuildId, request.ArtifactPath, request.Project, request.MetadataFile);
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
