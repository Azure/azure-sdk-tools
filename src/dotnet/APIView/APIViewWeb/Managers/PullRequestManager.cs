// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIView.DIff;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
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

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsModel(string reviewId) {
            return await _pullRequestsRepository.GetPullRequestsAsync(reviewId);
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsModel(int pullRequestNumber, string repoName)
        {
            return await _pullRequestsRepository.GetPullRequestsAsync(pullRequestNumber, repoName);
        }
    }
}
