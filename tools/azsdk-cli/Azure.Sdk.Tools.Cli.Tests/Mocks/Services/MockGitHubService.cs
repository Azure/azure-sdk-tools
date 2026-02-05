using Octokit;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Mocks.Services
{
    public class MockGitHubService : IGitHubService
    {
        public Task<CreateBranchStatus> CreateBranchAsync(string repoOwner, string repoName, string branchName, string baseBranchName = "main")
        {
            // Default mock: creating a branch succeeds
            return Task.FromResult(CreateBranchStatus.Created);
        }

        public Task<bool> IsExistingBranchAsync(string repoOwner, string repoName, string branchName)
        {
            // Default mock: branch does not exist
            return Task.FromResult(false);
        }


        public Task<User> GetGitUserDetailsAsync()
        {
            // Create a simple mock user - we'll use reflection to set properties if needed
            var user = CreateMockUser("testuser", 123456);
            return Task.FromResult(user);
        }

        public Task<List<string>> GetPullRequestChecksAsync(int pullRequestNumber, string repoName, string repoOwner)
        {
            // Default: no checks
            return Task.FromResult(new List<string>());
        }

        public Task<PullRequest> GetPullRequestAsync(string repoOwner, string repoName, int pullRequestNumber)
        {
            // Create a minimal pull request mock
            var pr = CreateMockPullRequest(repoOwner, repoName, pullRequestNumber);
            return Task.FromResult(pr);
        }

        public Task<string> GetGitHubParentRepoUrlAsync(string owner, string repoName)
        {
            return Task.FromResult($"https://github.com/{owner}/{repoName}");
        }

        public Task<PullRequestResult> CreatePullRequestAsync(string repoName, string repoOwner, string baseBranch, string headBranch, string title, string body, bool draft = true)
        {
            // Return a simple successful PullRequestResult
            var url = $"https://example/{repoOwner}/{repoName}/pulls/{Guid.NewGuid():N}";
            return Task.FromResult(new PullRequestResult
            {
                Url = url,
                Messages = new List<string> { draft ? "Pull request created successfully as draft PR." : "Pull request created successfully." }
            });
        }

        public Task<List<string>> GetPullRequestCommentsAsync(string repoOwner, string repoName, int pullRequestNumber)
        {
            var comments = new List<string>
            {
                "Comment by testuser: This looks good to me! The implementation is solid.",
                "Comment by reviewer: Please add more unit tests for the edge cases we discussed."
            };
            return Task.FromResult(comments);
        }

        public Task<PullRequest?> GetPullRequestForBranchAsync(string repoOwner, string repoName, string remoteBranch)
        {
            // Default: no existing PR for branch
            return Task.FromResult<PullRequest?>(null);
        }

        public Task<IReadOnlyList<PullRequest?>> SearchPullRequestsByTitleAsync(string repoOwner, string repoName, string titleSearchTerm, ItemState? state = ItemState.Open)
        {
            // Default: no matching PRs
            return Task.FromResult<IReadOnlyList<PullRequest?>>(new List<PullRequest?>().AsReadOnly());
        }

        public Task<Issue> GetIssueAsync(string repoOwner, string repoName, int issueNumber)
        {
            var issue = CreateMockIssue(repoOwner, repoName, issueNumber);
            return Task.FromResult(issue);
        }

        public Task UpdatePullRequestAsync(string repoOwner, string repoName, int pullRequestNumber, string title, string body, ItemState state)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RepositoryContent>?> GetContentsAsync(string owner, string repoName, string path)
        {
            return Task.FromResult<IReadOnlyList<RepositoryContent>?>(new List<RepositoryContent>().AsReadOnly());
        }

        public Task<IReadOnlyList<RepositoryContent>?> GetContentsAsync(string owner, string repoName, string path, string? branch = null)
        {
            return Task.FromResult<IReadOnlyList<RepositoryContent>?>(new List<RepositoryContent>().AsReadOnly());
        }

        public Task UpdateFileAsync(string owner, string repoName, string path, string message, string content, string sha, string branch)
        {
            // No-op for tests
            return Task.CompletedTask;
        }

        public Task<RepositoryContent> GetContentsSingleAsync(string owner, string repoName, string path, string? branch = null)
        {
            // Return a simple RepositoryContent for the requested path
            var fileName = Path.GetFileName(path);
            var content = CreateMockRepositoryContent(fileName, path, string.Empty);
            return Task.FromResult(content);
        }

        public Task<HashSet<string>?> GetPublicOrgMembership(string username)
        {
            throw new Exception("Not implemented.");
        }
        public Task<bool> HasWritePermission(string owner, string repo, string username)
        {
            return Task.FromResult(true);
        }

        private RepositoryContent CreateMockRepositoryContent(string name, string path, string encodedContent)
        {
            return new RepositoryContent(
                name: name,
                path: path,
                sha: $"sha{name.GetHashCode():x}",
                size: encodedContent.Length,
                type: ContentType.File,
                downloadUrl: $"https://raw.githubusercontent.com/testowner/testrepo/main/{path}",
                url: $"https://api.github.com/repos/testowner/testrepo/contents/{path}",
                htmlUrl: $"https://github.com/testowner/testrepo/blob/main/{path}",
                gitUrl: $"https://api.github.com/repos/testowner/testrepo/git/blobs/sha{name.GetHashCode():x}",
                encoding: "base64",
                encodedContent: encodedContent,
                target: null,
                submoduleGitUrl: null
            );
        }

        // Helper methods to create mock objects using minimal constructors
        private User CreateMockUser(string login, int id)
        {
            // Use the simplest possible constructor and let Octokit handle the rest
            return new User(
                url: $"https://api.github.com/users/{login}",
                htmlUrl: $"https://github.com/{login}",
                avatarUrl: $"https://avatars.githubusercontent.com/u/{id}",
                id: id,
                login: login,
                nodeId: $"U_{id}",
                bio: "",
                siteAdmin: false,
                blog: "", // Added missing 'blog' parameter
                createdAt: DateTimeOffset.Now.AddYears(-1),
                updatedAt: DateTimeOffset.Now,
                followers: 0,
                following: 0,
                hireable: null,
                email: "",
                publicRepos: 0,
                publicGists: 0,
                totalPrivateRepos: 0,
                ownedPrivateRepos: 0,
                diskUsage: 0,
                collaborators: 0,
                plan: null,
                privateGists: 0,
                company: "",
                location: "",
                name: "",
                suspendedAt: null,
                ldapDistinguishedName: "",
                permissions: null
            );
        }

        private PullRequest CreateMockPullRequest(string repoOwner, string repoName, int pullRequestNumber)
        {
            var user = CreateMockUser("testuser", 123456);

            // Create minimal pull request
            return new PullRequest(
                url: $"https://api.github.com/repos/{repoOwner}/{repoName}/pulls/{pullRequestNumber}",
                htmlUrl: $"https://github.com/{repoOwner}/{repoName}/pull/{pullRequestNumber}",
                diffUrl: $"https://github.com/{repoOwner}/{repoName}/pull/{pullRequestNumber}.diff",
                patchUrl: $"https://github.com/{repoOwner}/{repoName}/pull/{pullRequestNumber}.patch",
                issueUrl: $"https://api.github.com/repos/{repoOwner}/{repoName}/issues/{pullRequestNumber}",
                statusesUrl: $"https://api.github.com/repos/{repoOwner}/{repoName}/statuses/abc123",
                number: pullRequestNumber,
                state: ItemState.Open,
                title: "Test Pull Request",
                body: "This is a test pull request",
                createdAt: DateTimeOffset.Now.AddDays(-1),
                updatedAt: DateTimeOffset.Now,
                closedAt: null,
                mergedAt: null,
                head: CreateMockGitReference($"{repoOwner}:feature-branch", "feature-branch", "abc123", user),
                @base: CreateMockGitReference($"{repoOwner}:main", "main", "def456", user),
                user: user,
                assignee: null,
                assignees: new List<User>(),
                draft: false,
                mergeable: true,
                mergeableState: MergeableState.Clean,
                mergedBy: null,
                mergeCommitSha: null,
                comments: 0,
                maintainerCanModify: true,
                commits: 3,
                additions: 15,
                deletions: 5,
                changedFiles: 2,
                milestone: null,
                locked: false,
                activeLockReason: null,
                labels: new List<Label>(),
                requestedReviewers: new List<User>(),
                requestedTeams: new List<Team>(),
                id: pullRequestNumber,
                nodeId: $"PR_{pullRequestNumber}"
            );
        }

        private GitReference CreateMockGitReference(string label, string @ref, string sha, User user)
        {
            return new GitReference(
                label: label,
                @ref: $"refs/heads/{@ref}",
                sha: sha,
                nodeId: $"REF_{sha}",
                url: $"https://api.github.com/repos/testowner/testrepo/git/refs/heads/{@ref}",
                user: user,
                repository: CreateMockRepository("testowner", "testrepo", user)
            );
        }

        private Repository CreateMockRepository(string owner, string name, User user)
        {
            return new Repository(
                url: $"https://api.github.com/repos/{owner}/{name}",
                htmlUrl: $"https://github.com/{owner}/{name}",
                cloneUrl: $"https://github.com/{owner}/{name}.git",
                gitUrl: $"git://github.com/{owner}/{name}.git",
                sshUrl: $"git@github.com:{owner}/{name}.git",
                svnUrl: $"https://github.com/{owner}/{name}",
                mirrorUrl: null,
                archiveUrl: "",
                id: 12345,
                nodeId: "REPO_12345",
                owner: user,
                name: name,
                fullName: $"{owner}/{name}",
                isTemplate: false,
                description: "Test repository",
                homepage: null,
                language: "C#",
                @private: false,
                fork: false,
                forksCount: 0,
                stargazersCount: 0,
                watchersCount: 0,
                defaultBranch: "main",
                openIssuesCount: 0,
                pushedAt: DateTimeOffset.Now,
                createdAt: DateTimeOffset.Now.AddMonths(-6),
                updatedAt: DateTimeOffset.Now,
                permissions: null,
                parent: null,
                source: null,
                license: null,
                hasDiscussions: false, // Added missing parameter
                hasIssues: true,
                hasWiki: true,
                hasPages: false,
                hasDownloads: true,
                archived: false,
                visibility: RepositoryVisibility.Public,
                allowRebaseMerge: true,
                allowSquashMerge: true,
                allowMergeCommit: true,
                subscribersCount: 0,
                size: 100,
                webCommitSignoffRequired: false,
                topics: new List<string>(),
                deleteBranchOnMerge: null,
                allowAutoMerge: null,
                allowUpdateBranch: null,
                securityAndAnalysis: null
            );
        }

        private Issue CreateMockIssue(string repoOwner, string repoName, int issueNumber)
        {
            var user = CreateMockUser("testuser", 123456);

            return new Issue(
                url: $"https://api.github.com/repos/{repoOwner}/{repoName}/issues/{issueNumber}",
                htmlUrl: $"https://github.com/{repoOwner}/{repoName}/issues/{issueNumber}",
                commentsUrl: $"https://api.github.com/repos/{repoOwner}/{repoName}/issues/{issueNumber}/comments",
                eventsUrl: $"https://api.github.com/repos/{repoOwner}/{repoName}/issues/{issueNumber}/events",
                id: issueNumber,
                nodeId: $"ISSUE_{issueNumber}",
                number: issueNumber,
                title: "Test Issue",
                body: "This is a test issue created by the mock service for testing purposes.",
                user: user,
                labels: new List<Label>(),
                assignee: null,
                assignees: new List<User>(),
                milestone: null,
                comments: 0,
                pullRequest: null,
                closedAt: null,
                createdAt: DateTimeOffset.Now.AddDays(-2),
                updatedAt: DateTimeOffset.Now,
                closedBy: null,
                state: ItemState.Open,
                locked: false,
                activeLockReason: null,
                reactions: new ReactionSummary(),
                repository: null,
                stateReason: null
            );
        }

        public Task<Issue> CreateIssueAsync(string repoOwner, string repoName, string title, string body, List<string>? assignees = null)
        {
            // Create a mock issue
            var issue = CreateMockIssue(repoOwner, repoName, 1);
            return Task.FromResult(issue);
        }

        public Task<string?> GetPullRequestHeadSha(string repoOwner, string repoName, int pullRequestNumber)
        {
            // Return a mock SHA
            return Task.FromResult<string?>("abc123def456");
        }

        public Task<string?> GetFileFromPullRequest(string repoOwner, string repoName, int pullRequestNumber, string filePath)
        {
            // Return null to indicate file not found (default mock behavior)
            return Task.FromResult<string?>(null);
        }

        public Task<string?> GetFileFromBranch(string repoOwner, string repoName, string branch, string filePath)
        {
            // Return null to indicate file not found (default mock behavior)
            return Task.FromResult<string?>(null);
        }
    }
}
