// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Octokit;

namespace APIViewWeb.Managers
{
    public class PullRequestManager : IPullRequestManager
    {
        static readonly GitHubClient _githubClient = new GitHubClient(new ProductHeaderValue("apiview"));

        private readonly ICosmosPullRequestsRepository _pullRequestsRepository;

        public PullRequestManager(
            ICosmosPullRequestsRepository pullRequestsRepository
            )
        {
            _pullRequestsRepository = pullRequestsRepository;
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsModelAsync(string reviewId) {
            return await _pullRequestsRepository.GetPullRequestsAsync(reviewId);
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

    }
}
