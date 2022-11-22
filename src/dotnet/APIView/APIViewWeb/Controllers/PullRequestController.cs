// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Managers;
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
        private readonly IPullRequestManager _pullRequestManager;
        private readonly ILogger _logger;

        string[] VALID_EXTENSIONS = new string[] { ".whl", ".api.json", ".nupkg", "-sources.jar", ".gosource" };

        public PullRequestController(IPullRequestManager pullRequestManager, ILogger<AutoReviewController> logger)
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
            string baselineCodeFile = null,
            bool commentOnPR = true,
            string language = null)
        {
            if (!ValidateInputParams())
            {
                return StatusCode(StatusCodes.Status400BadRequest);
            }

            //Handle only authorization exception and send 401 as status code.
            //All other exception should not be handled so we will have required info in app insights.
            try
            {
                var reviewUrl = await _pullRequestManager.DetectApiChanges(buildId, artifactName, filePath, commitSha, repoName, packageName, pullRequestNumber, this.Request.Host.ToUriComponent(), codeFileName: codeFile, baselineCodeFileName: baselineCodeFile, commentOnPR: commentOnPR, language: language);
                return !string.IsNullOrEmpty(reviewUrl) ? StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl) : StatusCode(statusCode: StatusCodes.Status208AlreadyReported);
            }
            catch (AuthorizationFailedException)
            {
                return StatusCode(StatusCodes.Status401Unauthorized);
            }
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
