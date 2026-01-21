using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    internal class GitHelperTests
    {
        private GitHelper gitHelper;
        private Mock<IGitHubService> mockGitHubService;
        private TestLogger<GitHelper> logger;

        [SetUp]
        public void Setup()
        {
            mockGitHubService = new Mock<IGitHubService>();
            logger = new TestLogger<GitHelper>();
            var gitCommandHelper = new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>());
            gitHelper = new GitHelper(mockGitHubService.Object, gitCommandHelper, logger);
        }

        #region GetRepoRemoteUri Tests

        [Test]
        public void GetRepoRemoteUri_WithSshOrigin_ReturnsHttpsUri()
        {
            using var repo = CreateTestRepoWithRemote("git@github.com:Azure/azure-rest-api-specs.git");
            var result = gitHelper.GetRepoRemoteUri(repo.DirectoryPath);
            Assert.That(result.ToString(), Is.EqualTo("https://github.com/Azure/azure-rest-api-specs.git"));
        }

        [Test]
        public void GetRepoRemoteUri_WithHttpsOrigin_ReturnsHttpsUri()
        {
            using var repo = CreateTestRepoWithRemote("https://github.com/Azure/azure-rest-api-specs.git");
            var result = gitHelper.GetRepoRemoteUri(repo.DirectoryPath);
            Assert.That(result.ToString(), Is.EqualTo("https://github.com/Azure/azure-rest-api-specs.git"));
        }

        [Test]
        public void GetRepoRemoteUri_WithNoOrigin_ThrowsException()
        {
            using var repo = CreateTestRepoWithoutRemote();
            var ex = Assert.Throws<InvalidOperationException>(() => gitHelper.GetRepoRemoteUri(repo.DirectoryPath));
            Assert.That(ex.Message, Does.Contain("origin"));
        }

        [Test]
        public void GetRepoRemoteUri_WithNonGitDirectory_ThrowsException()
        {
            using var tempDir = TempDirectory.Create("non_git");
            Assert.Throws<InvalidOperationException>(() => gitHelper.GetRepoRemoteUri(tempDir.DirectoryPath));
        }

        #endregion

        #region GetRepoFullNameAsync Tests

        [Test]
        public async Task GetRepoFullNameAsync_WithSubdirectoryPath_ReturnsCorrectFullName()
        {
            using var repo = CreateTestRepoWithRemote("git@github.com:Azure/azure-rest-api-specs.git");
            var subDir = Path.Combine(repo.DirectoryPath, "subdirectory");
            Directory.CreateDirectory(subDir);
            mockGitHubService.Setup(x => x.GetGitHubParentRepoUrlAsync("Azure", "azure-rest-api-specs"))
                           .ReturnsAsync(string.Empty); // Not a fork

            var result = await gitHelper.GetRepoFullNameAsync(subDir);
            Assert.That(result, Is.EqualTo("Azure/azure-rest-api-specs"));
        }

        [Test]
        public async Task GetRepoFullNameAsync_WithForkRepoButDontFindUpstream_ReturnsDirectFullName()
        {
            using var repo = CreateTestRepoWithRemote("https://github.com/UserFork/azure-rest-api-specs.git");
            var result = await gitHelper.GetRepoFullNameAsync(repo.DirectoryPath, findUpstreamParent: false);
            Assert.That(result, Is.EqualTo("UserFork/azure-rest-api-specs"));
        }

        [Test]
        public async Task GetRepoFullNameAsync_WithEmptyPath_ThrowsArgumentException()
        {
            // Test empty string
            try
            {
                await gitHelper.GetRepoFullNameAsync("");
                Assert.Fail("Expected ArgumentException was not thrown");
            }
            catch (ArgumentException ex)
            {
                Assert.That(ex.ParamName, Is.EqualTo("pathInRepo"));
            }
            
            // Test null
            try
            {
                await gitHelper.GetRepoFullNameAsync(null!);
                Assert.Fail("Expected ArgumentException was not thrown");
            }
            catch (ArgumentException ex)
            {
                Assert.That(ex.ParamName, Is.EqualTo("pathInRepo"));
            }
        }

        [Test]
        public void GetRepoFullNameAsync_WithNonGitDirectory_ThrowsException()
        {
            using var tempDir = TempDirectory.Create("non_git_fullname");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.GetRepoFullNameAsync(tempDir.DirectoryPath));
        }

        #endregion

        #region DiscoverRepoRoot Tests

        [Test]
        public void DiscoverRepoRoot_WithRepoRoot_ReturnsRepoRoot()
        {
            using var repo = CreateTestRepoWithoutRemote();
            var result = gitHelper.DiscoverRepoRoot(repo.DirectoryPath);
            Assert.That(NormalizePath(result), Is.EqualTo(NormalizePath(repo.DirectoryPath)));
        }

        [Test]
        public void DiscoverRepoRoot_WithSubdirectory_ReturnsRepoRoot()
        {
            using var repo = CreateTestRepoWithoutRemote();
            var subDir = Path.Combine(repo.DirectoryPath, "src", "nested", "deep");
            Directory.CreateDirectory(subDir);

            var result = gitHelper.DiscoverRepoRoot(subDir);
            Assert.That(NormalizePath(result), Is.EqualTo(NormalizePath(repo.DirectoryPath)));
        }

        [Test]
        public void DiscoverRepoRoot_WithFilePath_ReturnsRepoRoot()
        {
            using var repo = CreateTestRepoWithoutRemote();
            var filePath = Path.Combine(repo.DirectoryPath, "test.txt");
            File.WriteAllText(filePath, "test content");

            var result = gitHelper.DiscoverRepoRoot(filePath);
            Assert.That(NormalizePath(result), Is.EqualTo(NormalizePath(repo.DirectoryPath)));
        }

        [Test]
        public void DiscoverRepoRoot_WithNonGitDirectory_ThrowsException()
        {
            using var tempDir = TempDirectory.Create("non_git");
            var ex = Assert.Throws<InvalidOperationException>(() => gitHelper.DiscoverRepoRoot(tempDir.DirectoryPath));
            Assert.That(ex.Message, Does.Contain("No git repository found"));
        }

        [Test]
        public void DiscoverRepoRoot_WithEmptyPath_ThrowsException()
        {
            Assert.Throws<InvalidOperationException>(() => gitHelper.DiscoverRepoRoot(""));
        }

        #endregion

        #region GetBranchName Tests

        [Test]
        public void GetBranchName_WithDefaultBranch_ReturnsBranchName()
        {
            using var repo = CreateTestRepoWithoutRemote();
            GitTestHelper.GitCommit(repo.DirectoryPath, "Initial commit");

            var result = gitHelper.GetBranchName(repo.DirectoryPath);
            Assert.That(result, Is.EqualTo("master").Or.EqualTo("main"));
        }

        [Test]
        public void GetBranchName_WithCustomBranch_ReturnsBranchName()
        {
            using var repo = CreateTestRepoWithoutRemote();
            GitTestHelper.GitCommit(repo.DirectoryPath, "Initial commit");
            GitTestHelper.GitCreateBranch(repo.DirectoryPath, "test-branch");

            var result = gitHelper.GetBranchName(repo.DirectoryPath);
            Assert.That(result, Is.EqualTo("test-branch"));
        }

        #endregion

        #region GetMergeBaseCommitSha Tests

        [Test]
        public void GetMergeBaseCommitSha_WithValidBranches_ReturnsMergeBaseSha()
        {
            using var repo = CreateTestRepoWithoutRemote();
            
            // Create initial commit on default branch
            GitTestHelper.GitCommit(repo.DirectoryPath, "Initial commit");
            var defaultBranch = gitHelper.GetBranchName(repo.DirectoryPath);
            
            // Create and switch to feature branch
            GitTestHelper.GitCreateBranch(repo.DirectoryPath, "feature");
            GitTestHelper.GitCommit(repo.DirectoryPath, "Feature commit");
            
            // The merge base should be the initial commit
            var result = gitHelper.GetMergeBaseCommitSha(repo.DirectoryPath, defaultBranch);
            
            // Result should be a valid SHA (40 hex characters)
            Assert.That(result, Has.Length.EqualTo(40));
            Assert.That(result, Does.Match("^[0-9a-f]{40}$"));
        }

        [Test]
        public void GetMergeBaseCommitSha_WithInvalidBranch_ReturnsEmptyString()
        {
            using var repo = CreateTestRepoWithoutRemote();
            GitTestHelper.GitCommit(repo.DirectoryPath, "Initial commit");

            var result = gitHelper.GetMergeBaseCommitSha(repo.DirectoryPath, "nonexistent-branch");
            Assert.That(result, Is.Empty);
        }

        #endregion

        #region GetRepoName Tests

        [Test]
        public void GetRepoName_WithValidRemote_ReturnsRepoName()
        {
            using var repo = CreateTestRepoWithRemote("https://github.com/Azure/azure-sdk-tools.git");
            var result = gitHelper.GetRepoName(repo.DirectoryPath);
            Assert.That(result, Is.EqualTo("azure-sdk-tools"));
        }

        [Test]
        public void GetRepoName_WithEmptyPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => gitHelper.GetRepoName(""));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Normalizes a path for cross-platform comparison.
        /// Git CLI returns forward slashes on Windows, while .NET uses backslashes.
        /// </summary>
        private static string NormalizePath(string path) => path.Replace('\\', '/');

        private static TempDirectory CreateTestRepoWithRemote(string url)
        {
            var temp = TempDirectory.Create("gitrepo");
            GitTestHelper.GitInit(temp.DirectoryPath);
            GitTestHelper.GitRemoteAdd(temp.DirectoryPath, "origin", url);
            return temp;
        }

        private static TempDirectory CreateTestRepoWithoutRemote()
        {
            var temp = TempDirectory.Create("gitrepo");
            GitTestHelper.GitInit(temp.DirectoryPath);
            return temp;
        }

        #endregion
    }
}
