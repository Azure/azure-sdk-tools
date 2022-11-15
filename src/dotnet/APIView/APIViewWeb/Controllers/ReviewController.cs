// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Filters;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ReviewManager _reviewManager;
        private readonly ILogger _logger;

        public ReviewController(ReviewManager reviewManager, ILogger<ReviewController> logger)
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

        [HttpPost]
        public async Task<ActionResult> ApprovePackageName(string id)
        {
            await _reviewManager.ApprovePackageNameAsync(User, id);
            return RedirectToPage("/Assemblies/Review",  new { id = id });
        }
    }
}
