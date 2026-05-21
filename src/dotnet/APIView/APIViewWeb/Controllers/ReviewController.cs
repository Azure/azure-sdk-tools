// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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
            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(repoName, buildId, artifactPath, project, metadataFile);
            return Ok();
        }

        [Authorize("RequireTokenAuthentication")]
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

            if (!int.TryParse(request.BuildId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return BadRequest("BuildId must be numeric.");
            }

            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(request.RepoName, request.BuildId, request.ArtifactName, request.Project, request.MetadataFile);
            return Ok();
        }

        [Authorize("RequireTokenOrCookieAuthentication")]
        [HttpPost]
        public async Task<ActionResult> ApprovePackageName(string id)
        {
            await _reviewManager.ApproveReviewAsync(user: User, reviewId: id);
            return RedirectToPage("/Assemblies/Review",  new { id = id });
        }
    }
}
