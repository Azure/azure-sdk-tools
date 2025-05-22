// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace IssueLabeler.Shared
{
    public interface IGitHubClientWrapper
    {
        Task<Octokit.Issue> GetIssue(string owner, string repo, int number);
        Task<Octokit.PullRequest> GetPullRequest(string owner, string repo, int number);
        Task<IReadOnlyList<PullRequestFile>> GetPullRequestFiles(string owner, string repo, int number);
        Task CommentOn(string owner, string repo, int number, string v);
        Task UpdateIssue(string owner, string repo, int number, IssueUpdate issueUpdate);
    }

    public class GitHubClientWrapper : IGitHubClientWrapper
    {
        private readonly ILogger<GitHubClientWrapper> _logger;
        private GitHubClient _client;
        private readonly GitHubClientFactory _gitHubClientFactory;

        public GitHubClientWrapper(
            ILogger<GitHubClientWrapper> logger,
            GitHubClientFactory gitHubClientFactory)
        {
            _gitHubClientFactory = gitHubClientFactory;
            _logger = logger;

        }

        // TODO add lambda to remove repetetive logic in this class
        // -> call and pass a lambda calls create, and if fails remake and call it again.

        public async Task<Octokit.Issue> GetIssue(string owner, string repo, int number)
        {
            if (_client == null)
            {
                _client = await _gitHubClientFactory.CreateAsync();
            }
            Octokit.Issue iop = null;
            try
            {
                iop = await _client.Issue.Get(owner, repo, number);
            }
            catch (Exception ex)
            {
                _logger.LogError($"ex was of type {ex.GetType()}, message: {ex.Message}");
                _client = await _gitHubClientFactory.CreateAsync();
                iop = await _client.Issue.Get(owner, repo, number);
            }
            return iop;
        }

        public async Task<Octokit.PullRequest> GetPullRequest(string owner, string repo, int number)
        {
            if (_client == null)
            {
                _client = await _gitHubClientFactory.CreateAsync();
            }
            Octokit.PullRequest iop = null;
            try
            {
                iop = await _client.PullRequest.Get(owner, repo, number);
            }
            catch (Exception ex)
            {
                _logger.LogError($"ex was of type {ex.GetType()}, message: {ex.Message}");
                _client = await _gitHubClientFactory.CreateAsync();
                iop = await _client.PullRequest.Get(owner, repo, number);
            }
            return iop;
        }

        public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestFiles(string owner, string repo, int number)
        {
            if (_client == null)
            {
                _client = await _gitHubClientFactory.CreateAsync();
            }
            IReadOnlyList<PullRequestFile> prFiles = null;
            try
            {
                prFiles = await _client.PullRequest.Files(owner, repo, number);

            }
            catch (Exception ex)
            {
                _logger.LogError($"ex was of type {ex.GetType()}, message: {ex.Message}");
                _client = await _gitHubClientFactory.CreateAsync();
                prFiles = await _client.PullRequest.Files(owner, repo, number);
            }
            return prFiles;
        }

        public async Task UpdateIssue(string owner, string repo, int number, IssueUpdate issueUpdate)
        {
            if (_client == null)
            {
                _client = await _gitHubClientFactory.CreateAsync();
            }
            try
            {
                await _client.Issue.Update(owner, repo, number, issueUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError($"ex was of type {ex.GetType()}, message: {ex.Message}");
                _client = await _gitHubClientFactory.CreateAsync();
                await _client.Issue.Update(owner, repo, number, issueUpdate);
            }
        }

        // lambda -> call and pass a lambda calls create, and if fails remake and call it again.

        public async Task CommentOn(string owner, string repo, int number, string comment)
        {
            if (_client == null)
            {
                _client = await _gitHubClientFactory.CreateAsync();
            }
            try
            {
                await _client.Issue.Comment.Create(owner, repo, number, comment);
            }
            catch (Exception ex)
            {
                _logger.LogError($"ex was of type {ex.GetType()}, message: {ex.Message}");
                _client = await _gitHubClientFactory.CreateAsync();
                await _client.Issue.Comment.Create(owner, repo, number, comment);
            }
        }
    }
}