// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using System;
using System.Collections.Generic;
using LibGit2Sharp;


namespace AzureSDKDevToolsMCP.Helpers
{
    public interface IGitHelper
    {
        public string GetRepoOwnerName(string path);
        public string GetRepoName(string path);
        public string GetRepoRootPath(string path);
        public bool IsRepoPathForPublicSpecRepo(string path);
        public string GetCurrentCommitSha(string path);
        public string GetCurrentBranchName(string path);
        public IList<string> GetChangedFiles(string path);
    }
    public class GitHelper : IGitHelper
    {
        readonly static string SPEC_REPO_NAME = "azure-rest-api-specs";
        public IList<string> GetChangedFiles(string repoPath)
        {
            var changedFiles = new List<string>();
            using (var repo = new Repository(repoPath))
            {
                // Get the current branch
                Branch currentBranch = repo.Head;
                Console.WriteLine($"Current branch: {currentBranch.FriendlyName}");
                // Get the changes in the working directory
                var changes = repo.Diff.Compare<TreeChanges>(currentBranch.Tip.Tree, DiffTargets.WorkingDirectory);
                // List changed files
                foreach (var change in changes)
                {
                    changedFiles.Add(change.Path);
                    Console.WriteLine($"Changed file: {change.Path}");
                }
            }
            return changedFiles;
        }

        public string GetCurrentBranchName(string repoPath)
        {
            using var repo = new Repository(repoPath);
            var currentBranch = repo.Head;
            return currentBranch.FriendlyName;
        }

        public string GetCurrentCommitSha(string repoPath)
        {
            using var repo = new Repository(repoPath);
            var currentCommit = repo.Head.Tip;
            return currentCommit.Sha;
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
                return segments[segments.Length - 1].TrimEnd('/');
            }
            throw new InvalidOperationException("Unable to determine repository name.");
        }

        public string GetRepoOwnerName(string path)
        {
            var uri = GetRepoRemoteUri(path);
            var segments = uri.Segments;
            if (segments.Length > 2)
            {
                return segments[segments.Length - 2].TrimEnd('/');
            }
            throw new InvalidOperationException("Unable to determine repository owner.");
        }

        public bool IsRepoPathForPublicSpecRepo(string path)
        {
            // Check if GitHub repo name of cloned path is "azure-rest-api-specs"
            var uri = GetRepoRemoteUri(path);
            return uri.ToString().Contains(SPEC_REPO_NAME);            
        }
        public string GetRepoRootPath(string specPath)
        {
            // Get absolute path for repo root from given path.
            // Repo root is the parent of "specification" folder.
            var currentDirectory = new DirectoryInfo(specPath);
            while (currentDirectory != null && !currentDirectory.Name.Equals("specification"))
            {
                currentDirectory = currentDirectory.Parent;
            }
            return currentDirectory?.Parent?.FullName ?? string.Empty;
        }
    }
}
