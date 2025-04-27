// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using System;
using System.Collections.Generic;
using AzureSDKDevToolsMCP.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;


namespace AzureSDKDevToolsMCP.Helpers
{
    public interface IGitHelper
    {
        // Get the owner 
        public Task<string> GetRepoOwnerName(string path, bool findForkParent = true);
        public string GetRepoName(string path);
        public string GetRepoRootPath(string path);
        public bool IsRepoPathForPublicSpecRepo(string path);
        public string GetBranchName(string path);
        public string GetMergeBaseCommitSha(string path, string targetBranch);
    }
    public class GitHelper(IGitHubService gitHubService, ILogger<GitHelper> logger) : IGitHelper
    {
        readonly ILogger<GitHelper> logger = logger;
        readonly IGitHubService gitHubService = gitHubService;
        readonly static string SPEC_REPO_NAME = "azure-rest-api-specs";

        public string GetMergeBaseCommitSha(string path, string targetBranchName)
        {
            using (var repo = new Repository(path))
            {
                // Get the current branch
                Branch currentBranch = repo.Head;
                var targetBranch = repo.Branches[targetBranchName];

                // Find the merge base (common ancestor) (git merge-base main HEAD)
                var mergeBaseCommit = repo.ObjectDatabase.FindMergeBase(currentBranch.Tip, targetBranch.Tip);
                logger.LogInformation($"Current branch  :{currentBranch.FriendlyName}, Target branch SHA: {mergeBaseCommit?.Sha}");
                return mergeBaseCommit?.Sha ?? "";
            }
        }

        public string GetBranchName(string repoPath)
        {
            using var repo = new Repository(repoPath);
            var branchName = repo.Head.FriendlyName;
            return branchName;
        }

        private static Uri GetRepoRemoteUri(string path)
        {
            using var repo = new Repository(path);
            var remote = repo.Network?.Remotes["origin"];
            if (remote != null)
            {
                return new Uri(remote.Url);
            }
            throw new InvalidOperationException("Unable to determine remote URL.");
        }

        public string GetRepoName(string path)
        {
            var uri = GetRepoRemoteUri(path);
            var segments = uri.Segments;
            if (segments.Length > 1)
            {
                return segments[^1].TrimEnd(".git".ToCharArray());
            }
            throw new InvalidOperationException("Unable to determine repository name.");
        }

        public async Task<string> GetRepoOwnerName(string path, bool findForkParent = true)
        {
            var uri = GetRepoRemoteUri(path);
            var segments = uri.Segments;
            string repoOwner = string.Empty;
            string repoName = string.Empty;
            if (segments.Length > 2)
            {
                repoOwner = segments[^2].TrimEnd('/');
                repoName = segments[^1].TrimEnd(".git".ToCharArray());
            }

            if(findForkParent) {
                // Check if the repo is a fork and get the parent repo
                var parentRepoUrl = await gitHubService.GetGitHubParentRepoUrl(repoOwner, repoName);
                logger.LogInformation($"Parent repo URL: {parentRepoUrl}");
                if (!string.IsNullOrEmpty(parentRepoUrl))
                {
                    var parentSegments = new Uri(parentRepoUrl).Segments;
                    if (parentSegments.Length > 2)
                    {
                        repoOwner = parentSegments[^2].TrimEnd('/');
                    }
                }
            }

            if (!string.IsNullOrEmpty(repoOwner))
            {
                return repoOwner;
            }

            throw new InvalidOperationException("Unable to determine repository owner.");
        }

        public bool IsRepoPathForPublicSpecRepo(string path)
        {
            // Check if GitHub repo name of cloned path is "azure-rest-api-specs"
            var uri = GetRepoRemoteUri(path);
            return uri.ToString().Contains(SPEC_REPO_NAME);            
        }
        public string GetRepoRootPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path cannot be null or empty.", nameof(path));
            }

            if (Directory.Exists(Path.Combine(path, "specification")))
            {
                return path;
            }

            // Get absolute path for repo root from given path.
            // Repo root is the parent of "specification" folder.
            var currentDirectory = new DirectoryInfo(path);
            while (currentDirectory != null && !currentDirectory.Name.Equals("specification"))
            {
                currentDirectory = currentDirectory.Parent;
            }
            return currentDirectory?.Parent?.FullName ?? string.Empty;
        }
    }
}
