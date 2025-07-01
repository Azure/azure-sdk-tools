// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionManager;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly TelemetryClient _telemetryClient;

        public ReviewController(IReviewManager reviewManager,
            IAPIRevisionsManager apiRevisionManager, IHubContext<SignalRHub> signalRHubContext,
            TelemetryClient telemetryClient)
        {
            _reviewManager = reviewManager;
            _apiRevisionManager = apiRevisionManager;
            _signalRHubContext = signalRHubContext;
            _telemetryClient = telemetryClient;
        }

        [HttpGet]
        public async Task<ActionResult> UpdateApiReview(string repoName, string artifactPath, string buildId, string project = "internal")
        {
            await _apiRevisionManager.UpdateAPIRevisionCodeFileAsync(repoName, buildId, artifactPath, project);
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
