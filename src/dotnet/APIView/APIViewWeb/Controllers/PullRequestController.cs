// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class PullRequestController : Controller
    {
        private readonly PullRequestManager _pullRequestManager;
        private readonly ILogger _logger;

        public PullRequestController(PullRequestManager pullRequestManager, ILogger<AutoReviewController> logger)
        {
            _pullRequestManager = pullRequestManager;
            _logger = logger;
        }
                
        [HttpGet]
        public async Task<ActionResult> DetectApiChanges(
            string buildId, 
            string artifactName, 
            string filePath, 
            int pullRequestNumber, 
            string commitSha,
            string repoName)
        {
            await _pullRequestManager.DetectApiChanges(buildId, artifactName, filePath, pullRequestNumber, commitSha, repoName);
            return Ok();
        }

        
    }
}
