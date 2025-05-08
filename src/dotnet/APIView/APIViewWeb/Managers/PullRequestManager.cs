// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Configuration;
using Octokit;

namespace APIViewWeb.Managers
{
    public class PullRequestManager : IPullRequestManager
    {
        static readonly GitHubClient _githubClient = new GitHubClient(new ProductHeaderValue("apiview"));
        
        private readonly TelemetryClient _telemetryClient;
        private readonly ICosmosPullRequestsRepository _pullRequestsRepository;
        private readonly ICosmosAPIRevisionsRepository _apiRevisionsRepository;
        private readonly IAPIRevisionsManager _apiRevisionManager;
        private readonly IConfiguration _configuration;
        private readonly int _pullRequestCleanupDays;
        private readonly bool _isGitClientAvailable;

        public PullRequestManager(
            ICosmosPullRequestsRepository pullRequestsRepository,
            ICosmosAPIRevisionsRepository apiRevisionsRepository,
            IAPIRevisionsManager apiRevisionManager,
            IConfiguration configuration,
            TelemetryClient telemetryClient
            )
        {
            _pullRequestsRepository = pullRequestsRepository;
            _apiRevisionsRepository = apiRevisionsRepository;
            _apiRevisionManager = apiRevisionManager;
            _configuration = configuration;
            _telemetryClient = telemetryClient;

            var ghToken = _configuration["github-access-token"];
            if (!string.IsNullOrEmpty(ghToken))
            {
                _githubClient.Credentials = new Credentials(ghToken);
                _isGitClientAvailable = true;
            }
            var pullRequestReviewCloseAfter = _configuration["pull-request-review-close-after-days"] ?? "30";
            _pullRequestCleanupDays = int.Parse(pullRequestReviewCloseAfter);
        }
        public async Task UpsertPullRequestAsync(PullRequestModel pullRequestModel)
        {
            await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(string reviewId, string apiRevisionId = null) {
            return await _pullRequestsRepository.GetPullRequestsAsync(reviewId, apiRevisionId);
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(int pullRequestNumber, string repoName)
        {
            return await _pullRequestsRepository.GetPullRequestsAsync(pullRequestNumber, repoName);
        }

        public async Task<PullRequestModel> GetPullRequestModelAsync(int prNumber, string repoName, string packageName, string originalFile, string language)
        {
            var pullRequestModel = await _pullRequestsRepository.GetPullRequestAsync(prNumber, repoName, packageName, language);
            if (pullRequestModel == null)
            {
                var repoInfo = repoName.Split("/");
                var pullRequest = await _githubClient.PullRequest.Get(repoInfo[0], repoInfo[1], prNumber);
                pullRequestModel = new PullRequestModel()
                {
                    RepoName = repoName,
                    PullRequestNumber = prNumber,
                    FilePath = originalFile,
                    CreatedBy = pullRequest.User.Login,
                    PackageName = packageName,
                    Language = language,
                    Assignee = pullRequest.Assignee?.Login
                };
            }
            return pullRequestModel;
        }

        public async Task CleanupPullRequestData()
        {
            var telemetry = new RequestTelemetry { Name = "Cleaning up Reviews created for pull requests" };
            var operation = _telemetryClient.StartOperation(telemetry);
            try
            {
                var pullRequests = await _pullRequestsRepository.GetPullRequestsAsync(true);
                foreach (var prModel in pullRequests)
                {
                    try
                    {
                        if (prModel.APIRevisionId != null && await IsPullRequestEligibleForCleanup(prModel))
                        {
                            _telemetryClient.TrackEvent($"Closing revision {prModel.ReviewId}/{prModel.APIRevisionId} created for pull request {prModel.PullRequestNumber}");
                            await ClosePullRequestAPIRevision(prModel);
                        }
                        // Wait 10 seconds before processing next record.
                        await Task.Delay(10000);
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackEvent("Failed to close review " + prModel.ReviewId);
                        _telemetryClient.TrackException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
            finally
            {
                _telemetryClient.StopOperation(operation);
            }
        }

        private async Task<bool> IsPullRequestEligibleForCleanup(PullRequestModel prModel)
        {
            if (!_isGitClientAvailable)
                return false;

            var repoInfo = prModel.RepoName.Split("/");
            var issue = await _githubClient.Issue.Get(repoInfo[0], repoInfo[1], prModel.PullRequestNumber);
            // Close review created for pull request if pull request was closed more than _pullRequestCleanupDays days ago
            if (issue.ClosedAt != null)
            {
                return issue.ClosedAt?.AddDays(_pullRequestCleanupDays) < DateTimeOffset.Now;
            }
            return false;
        }

        private async Task ClosePullRequestAPIRevision(PullRequestModel pullRequestModel)
        {
            if (!String.IsNullOrEmpty(pullRequestModel.APIRevisionId))
            {
                var apiRevision = await _apiRevisionsRepository.GetAPIRevisionAsync(pullRequestModel.APIRevisionId);
                if (apiRevision != null && !apiRevision.Approvers.Any())
                {
                    await _apiRevisionManager.SoftDeleteAPIRevisionAsync(userName: "azure-sdk", apiRevision: apiRevision, notes: "Deleted by PullRequest CleanUp Automation");
                }
            }

            pullRequestModel.IsOpen = false;
            await _pullRequestsRepository.UpsertPullRequestAsync(pullRequestModel);
        }
    }
}
