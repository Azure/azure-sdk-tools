// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
        private readonly IAPIRevisionsManager _apiRevisionManager;

        public ReviewController(IAPIRevisionsManager apiRevisionManager)
        {
            _apiRevisionManager = apiRevisionManager;
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

            string project = string.IsNullOrWhiteSpace(request.Project) ? "internal" : request.Project.Trim();

            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(request.RepoName, request.BuildId, request.ArtifactName, project, request.MetadataFile);
            return Ok();
        }
    }
}
