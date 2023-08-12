// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IReviewManager _reviewManager;
        private readonly IHubContext<SignalRHub> _signalRHubContext;

        private readonly ILogger _logger;

        public ReviewController(IReviewManager reviewManager, IHubContext<SignalRHub> signalRHubContext, ILogger<ReviewController> logger)
        {
            _reviewManager = reviewManager;
            _signalRHubContext = signalRHubContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult> UpdateApiReview(string repoName, string artifactPath, string buildId, string project = "internal")
        {
            await _reviewManager.UpdateReviewCodeFiles(repoName, buildId, artifactPath, project);
            return Ok();
        }

        [NonAction]
        [HttpPost]
        public async Task GenerateAIReview(
            [FromQuery] string reviewId, [FromQuery]string revisionId = null)
        {
            var review = await _reviewManager.GetReviewAsync(User, reviewId);

            if (string.IsNullOrEmpty(revisionId))
                revisionId = review.Revisions.Last().RevisionId;

            await SendAIReviewGenerationStatus(review, reviewId, revisionId, AIReviewGenerationStatus.Generating);

            try {
                var commentsGenerated = await _reviewManager.GenerateAIReview(reviewId, revisionId);
                await SendAIReviewGenerationStatus(review, reviewId, revisionId, AIReviewGenerationStatus.Succeeded, commentsGenerated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI review");
                await SendAIReviewGenerationStatus(review, reviewId, revisionId, AIReviewGenerationStatus.Error, errorMessage: ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult> ApprovePackageName(string id)
        {
            await _reviewManager.ApprovePackageNameAsync(User, id);
            return RedirectToPage("/Assemblies/Review",  new { id = id });
        }

        private async Task SendAIReviewGenerationStatus(ReviewModel review, string reviewId,
            string revisionId, AIReviewGenerationStatus status, int? noOfCommentsGenerated = null,
            string errorMessage = null)
        {
            var notification = new AIReviewGenerationNotificationModel
            {
                ReviewId = reviewId,
                RevisionId = revisionId,
                IsLatestRevision = (revisionId == review.Revisions.Last().RevisionId),
                Status = status
            };

            switch (status)
            {
                case AIReviewGenerationStatus.Generating:
                    notification.Message = "Generating Review. ";
                    notification.Level = NotificatonLevel.Info;
                    break;
                case AIReviewGenerationStatus.Succeeded:
                    notification.Message = $"Succeeded! {noOfCommentsGenerated} comment(s) added.";
                    notification.Level = NotificatonLevel.Info;
                    break;
                case AIReviewGenerationStatus.Error:
                    notification.Message = $"{errorMessage}";
                    notification.Level = NotificatonLevel.Error;
                    break;
            }
            await _signalRHubContext.Clients.Group(User.GetGitHubLogin()).SendAsync("RecieveAIReviewGenerationStatus", notification);
        }
    }
}
