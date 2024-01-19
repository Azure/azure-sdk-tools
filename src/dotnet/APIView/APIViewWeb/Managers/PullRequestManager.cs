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
        static readonly string PR_APIVIEW_BOT_COMMENT_IDENTIFIER = "**API change check**";
        static readonly string PR_APIVIEW_BOT_COMMENT = "APIView has identified API level changes in this PR and created following API reviews.";
        static readonly string PR_APIVIEW_BOT_NO_CHANGE_COMMENT = "API changes are not detected in this pull request.";
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

        public async Task CreateOrUpdateCommentsOnPR(List<PullRequestModel> pullRequests, string repoOwner, string repoName, int prNumber, string hostName)
        {
            var existingComment = await GetExistingCommentForPackage(repoOwner, repoName, prNumber);
            var bldr = new StringBuilder(PR_APIVIEW_BOT_COMMENT_IDENTIFIER);
            bldr.Append(Environment.NewLine).Append(Environment.NewLine);
            if (pullRequests.Count > 0)
            {
                bldr.Append(PR_APIVIEW_BOT_COMMENT).Append(Environment.NewLine).Append(Environment.NewLine);
                foreach (var p in pullRequests)
                {
                    var revisionLink = ManagerHelpers.ResolveReviewUrl(pullRequest: p, hostName: hostName);
                    bldr.Append('[').Append(p.PackageName).Append("](").Append(revisionLink).Append(')');
                    bldr.Append(Environment.NewLine);
                }
                bldr.Append(Environment.NewLine);
            }
            else
            {
                bldr.Append(PR_APIVIEW_BOT_NO_CHANGE_COMMENT);
            }

            if (existingComment != null)
            {
                await _githubClient.Issue.Comment.Update(repoOwner, repoName, existingComment.Id, bldr.ToString());
            }
            else
            {
                await _githubClient.Issue.Comment.Create(repoOwner, repoName, prNumber, bldr.ToString());
            }
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
                            _telemetryClient.TrackEvent($"Closing review {prModel.ReviewId}/{prModel.APIRevisionId} created for pull request {prModel.PullRequestNumber}");
                            await ClosePullRequestAPIRevision(prModel);
                        }
                        await Task.Delay(500);
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

        private async Task<IssueComment> GetExistingCommentForPackage(string repoOwner, string repoName, int pr)
        {
            var comments = await _githubClient.Issue.Comment.GetAllForIssue(repoOwner, repoName, pr);
            if (comments != null)
            {
                // Check for comment created for current package.
                // GitHub issue comment unfortunately doesn't have any key to verify. So we need to check actual body to find the comment.
                return comments.Where(c => c.Body.Contains(PR_APIVIEW_BOT_COMMENT_IDENTIFIER)).LastOrDefault();
            }
            return null;
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
