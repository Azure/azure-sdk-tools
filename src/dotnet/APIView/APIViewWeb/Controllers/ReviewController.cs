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
        public string ArtifactName { get; set; }
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

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult> UpdateApiReview(string repoName, string artifactPath, string buildId, string project = "internal", string metadataFile = null)
        {
            if (string.IsNullOrWhiteSpace(repoName) ||
                string.IsNullOrWhiteSpace(buildId) ||
                string.IsNullOrWhiteSpace(artifactPath))
            {
                return BadRequest("RepoName, BuildId, and ArtifactPath are required.");
            }

            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(repoName, buildId, artifactPath, project, metadataFile);
            return Ok();
        }

        [HttpPost]
        public async Task<ActionResult> UpdateApiReview([FromBody] UpdateApiReviewRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.RepoName) ||
                string.IsNullOrWhiteSpace(request.BuildId) ||
                string.IsNullOrWhiteSpace(request.ArtifactName))
            {
                return BadRequest("RepoName, BuildId, and ArtifactName are required.");
            }

            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(request.RepoName, request.BuildId, request.ArtifactName, request.Project, request.MetadataFile);
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
