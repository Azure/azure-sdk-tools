// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionManager;
        private readonly IHubContext<SignalRHub> _signalRHubContext;

        private readonly ILogger _logger;

        public ReviewController(IReviewManager reviewManager,
            IAPIRevisionsManager apiRevisionManager, IHubContext<SignalRHub> signalRHubContext,
            ILogger<ReviewController> logger)
        {
            _reviewManager = reviewManager;
            _apiRevisionManager = apiRevisionManager;
            _signalRHubContext = signalRHubContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult> UpdateApiReview(string repoName, string artifactPath, string buildId, string project = "internal")
        {
            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(repoName, buildId, artifactPath, project);
            return Ok();
        }

        [NonAction]
        [HttpPost]
        public async Task GenerateAIReview(
            [FromQuery] string reviewId, [FromQuery]string revisionId = null)
        {
            var review = await _reviewManager.GetReviewAsync(User, reviewId);
            var latestAPIRevision = await _apiRevisionManager.GetLatestAPIRevisionsAsync(reviewId: review.Id);
            var apiRevison = latestAPIRevision;

            if (!string.IsNullOrEmpty(revisionId))
            {
                apiRevison = await _apiRevisionManager.GetAPIRevisionAsync(user: User, apiRevisionId: revisionId);
            }

            var isLatestAPIRevision = (apiRevison.Id == latestAPIRevision.Id);

            await SendAIReviewGenerationStatus(review, reviewId, revisionId, AIReviewGenerationStatus.Generating, isLatestAPIRevision);

            try {
                var commentsGenerated = await _reviewManager.GenerateAIReview(reviewId, revisionId);
                await SendAIReviewGenerationStatus(review, reviewId, revisionId, AIReviewGenerationStatus.Succeeded, isLatestAPIRevision, commentsGenerated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI review");
                await SendAIReviewGenerationStatus(review, reviewId, revisionId, AIReviewGenerationStatus.Error, isLatestAPIRevision, errorMessage: ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult> ApprovePackageName(string id)
        {
            await _reviewManager.ApproveReviewAsync(user: User, reviewId: id);
            return RedirectToPage("/Assemblies/Review",  new { id = id });
        }

        private async Task SendAIReviewGenerationStatus(ReviewListItemModel review, string reviewId,
            string revisionId, AIReviewGenerationStatus status, bool isLatestAPIRevision, int? noOfCommentsGenerated = null,
            string errorMessage = null)
        {
            var notification = new AIReviewGenerationNotificationModel
            {
                ReviewId = reviewId,
                RevisionId = revisionId,
                IsLatestRevision = isLatestAPIRevision,
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
