﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    public class PullRequestController : Controller
    {
        private readonly PullRequestManager _pullRequestManager;
        private readonly ILogger _logger;

        string[] VALID_EXTENSIONS = new string[] { ".whl", ".api.json", ".nupkg", "-sources.jar" };

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
            string commitSha,
            string repoName,
            string packageName,
            int pullRequestNumber = 0,
            string codeFile = null,
            string baselineCodeFile = null)
        {
            if (!ValidateInputParams())
            {
                return StatusCode(StatusCodes.Status400BadRequest);
            }
            await _pullRequestManager.DetectApiChanges(buildId, artifactName, filePath, commitSha, repoName, packageName, pullRequestNumber, this.Request.Host.ToUriComponent(), codeFileName: codeFile, baselineCodeFileName: baselineCodeFile);
            return Ok();
        }

        private bool ValidateInputParams()
        {
            foreach (var queryParam in this.Request.Query)
            {
                var value = queryParam.Value.ToString();
                if (queryParam.Key == "filePath")
                {
                    if (!VALID_EXTENSIONS.Any(e => value.EndsWith(e)))
                        return false;
                }

                if (queryParam.Key == "repoName")
                {
                    if (!value.Contains("/"))
                        return false;
                }
            }
            return true;
        }
    }
}
