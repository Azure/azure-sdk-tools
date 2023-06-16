// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using APIViewWeb.Hubs;
using APIViewWeb.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IReviewManager _reviewManager;
        private readonly IHubContext<NotificationHub> _notificationHubContext;

        private readonly ILogger _logger;

        public ReviewController(IReviewManager reviewManager, IHubContext<NotificationHub> notificationHub, ILogger<ReviewController> logger)
        {
            _reviewManager = reviewManager;
            _notificationHubContext = notificationHub;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult> UpdateApiReview(string repoName, string artifactPath, string buildId, string project = "internal")
        {
            await _reviewManager.UpdateReviewCodeFiles(repoName, buildId, artifactPath, project);
            return Ok();
        }

        [HttpPost]
        public async Task GenerateAIReview(
            [FromQuery] string reviewId, [FromQuery]string revisionId = null)
        {
            var review = await _reviewManager.GetReviewAsync(User, reviewId);

            if (string.IsNullOrEmpty(reviewId))
                revisionId = review.Revisions.Last().RevisionId;

            await _reviewManager.GenerateAIReview(reviewId, revisionId);
            await _notificationHubContext.Clients.All.SendAsync("RecieveAIReviewGenerationStatus", new
            {
                reviewId,
                revisionId,
                isLatest = (revisionId == review.Revisions.Last().RevisionId),
                status = "generating"
            });
        }

        [HttpPost]
        public async Task<ActionResult> ApprovePackageName(string id)
        {
            await _reviewManager.ApprovePackageNameAsync(User, id);
            return RedirectToPage("/Assemblies/Review",  new { id = id });
        }
    }
}
