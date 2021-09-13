// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Respositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace APIViewWeb.Repositories
{
    public class PullRequestManager
    {
        static readonly HttpClient devopsClient = new();
        static readonly GitHubClient githubClient = new(new Octokit.ProductHeaderValue("apiview"));
        static readonly string ARTIFACT_GET_URL = "https://dev.azure.com/azure-sdk/public/_apis/build/builds/{buildId}/artifacts?artifactName={artifactName}&api-version=4.1";

        private readonly ReviewManager _reviewManager;
        private readonly CosmosPullRequestsRepository _pullRequestsRepository;

        public PullRequestManager(
            ReviewManager reviewManager,
            CosmosReviewRepository reviewsRepository,
            CosmosPullRequestsRepository pullRequestsRepository,
            IConfiguration configuration
            )
        {
            _reviewManager = reviewManager;
            _pullRequestsRepository = pullRequestsRepository;
            githubClient.Credentials = new Credentials(configuration["github-access-token"]);
        }

        // API change detection for PR will pull artifact from devops artifact
        // This will be changed to a different component in the future based on the implementation for SDK automation
        public async Task DetectApiChanges(string buildId, string artifactName, string filePath, int prNumber, string commitSha, string language)
        {
            TelemetryClient telemetryClient = new(TelemetryConfiguration.CreateDefault());
            var requestTelemetry = new RequestTelemetry { Name = "Detecting API changes for PR: " + prNumber };
            var operation = telemetryClient.StartOperation(requestTelemetry);
            try
            {
                if (await IsShaAlreadyProcessed(language, prNumber, commitSha, filePath))
                {
                    return;
                }

                var pullRequestModel = await _pullRequestsRepository.GetPullRequestAsync(prNumber, language, filePath);
                if (pullRequestModel == null)
                {
                    pullRequestModel = new PullRequestModel()
                    {
                        Language = language,
                        CommitSha = commitSha,
                        PullRequestNumber = prNumber,
                        FilePath = filePath
                    };
                }
                else
                {
                    pullRequestModel.CommitSha = commitSha;
                }

                // Download code file from devops artifact location
                // We have opted for pull option instead of passing artifact in request since these validations are done for PR pipeline
                using var stream = await DownloadPackageArtifact(buildId, artifactName, filePath, telemetryClient);
                if (stream != null)
                {
                    using var memoryStream = new MemoryStream();
                    var codeFile = await _reviewManager.CreateCodeFile(Path.GetFileName(filePath), stream, false, memoryStream);
                    var apiDiff = await _reviewManager.GetApiDiffFromAutomaticReview(codeFile);
                    // Add API change detection label and comment to PR if there are API changes
                    if (apiDiff != "")
                    {
                        UpdatedPullRequest(apiDiff, prNumber, language, telemetryClient);
                    }
                    await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);
                }
                else
                {
                    telemetryClient.TrackTrace("Failed to download artifact. Please recheck build id and artifact path values in API change detection request.");
                }                
            }
            catch (Exception ex)
            {
                telemetryClient.TrackException(ex);
            }
            finally
            {
                telemetryClient.StopOperation(operation);
            }
        }

        // Make sure we are not processing same commit by repeated run of build pipeline
        private async Task<bool> IsShaAlreadyProcessed(string language, int prNumber, string commitSha, string filePath)
        {
            var pullRequestModel = await _pullRequestsRepository.GetPullRequestAsync(prNumber, language, commitSha, filePath);
            return pullRequestModel != null;
        }

        private async Task<Stream> DownloadPackageArtifact(string buildId, string artifactName, string filePath, TelemetryClient telemetryClient)
        {
            try
            {
                var artifactGetReq = ARTIFACT_GET_URL.Replace("{buildId}", buildId).Replace("{artifactName}", artifactName);
                var response = await devopsClient.GetAsync(artifactGetReq);
                response.EnsureSuccessStatusCode();
                var buildResource = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (buildResource == null)
                {
                    telemetryClient.TrackTrace("Artifact get response is invalid. Check REST api to get devops artifacts for any changes.");
                    return null;
                }
                var downloadUrl = buildResource.RootElement.GetProperty("resource").GetProperty("downloadUrl").GetString();
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    downloadUrl = downloadUrl.Split("?")[0] + "?format=file&subPath=" + filePath;
                    var downloadResp = await devopsClient.GetAsync(downloadUrl);
                    downloadResp.EnsureSuccessStatusCode();
                    return await downloadResp.Content.ReadAsStreamAsync();
                }
                else
                {
                    telemetryClient.TrackTrace("Artifact get response does not have download URL. Check REST api response to get devops artifacts for any changes.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                telemetryClient.TrackException(ex);
            }
            return null;
        }

        private async void UpdatedPullRequest(string diff, int prNumber, string language, TelemetryClient telemetryClient)
        {
            var repoName = GetGitHubRepoForLanguage(language);
            if (repoName != "")
            {
                try
                {
                    // We should add handling of GH rate limiting here in next revision
                    await githubClient.Issue.Comment.Create("azure", repoName, prNumber, diff);
                }
                catch (Exception ex)
                {
                    telemetryClient.TrackException(ex);
                }
            }
            else
            {
                telemetryClient.TrackTrace("API change detection is not enabled for language " + language);
            }
            
        }

        private string GetGitHubRepoForLanguage(string language)
        {
            // API change detection is currently only supported for 4 languages.
            switch (language)
            {
                case "java":
                case "python":
                case "js":
                case "dotnet":
                    return "azure-sdk-for-"+language;
                default:
                    return "";
            }
        }
    }
}
