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

        #region GetRepoRemoteUriAsync Tests

        [Test]
        public async Task GetRepoRemoteUriAsync_WithSshOrigin_ReturnsHttpsUri()
        {
            using var repo = await CreateTestRepoWithRemoteAsync("git@github.com:Azure/azure-rest-api-specs.git");
            var result = await gitHelper.GetRepoRemoteUriAsync(repo.DirectoryPath, ct: CancellationToken.None);
            Assert.That(result.ToString(), Is.EqualTo("https://github.com/Azure/azure-rest-api-specs.git"));
        }

        [Test]
        public async Task GetRepoRemoteUriAsync_WithHttpsOrigin_ReturnsHttpsUri()
        {
            using var repo = await CreateTestRepoWithRemoteAsync("https://github.com/Azure/azure-rest-api-specs.git");
            var result = await gitHelper.GetRepoRemoteUriAsync(repo.DirectoryPath, ct: CancellationToken.None);
            Assert.That(result.ToString(), Is.EqualTo("https://github.com/Azure/azure-rest-api-specs.git"));
        }

        [Test]
        public async Task GetRepoRemoteUriAsync_WithNoOrigin_ThrowsException()
        {
            using var repo = await CreateTestRepoWithoutRemoteAsync();
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.GetRepoRemoteUriAsync(repo.DirectoryPath, ct: CancellationToken.None));
            Assert.That(ex.Message, Does.Contain("origin"));
        }

        [Test]
        public void GetRepoRemoteUriAsync_WithNonGitDirectory_ThrowsException()
        {
            using var tempDir = TempDirectory.Create("non_git");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.GetRepoRemoteUriAsync(tempDir.DirectoryPath, ct: CancellationToken.None));
        }

        #endregion

        #region GetRepoFullNameAsync Tests

        [Test]
        public async Task GetRepoFullNameAsync_WithSubdirectoryPath_ReturnsCorrectFullName()
        {
            using var repo = await CreateTestRepoWithRemoteAsync("git@github.com:Azure/azure-rest-api-specs.git");
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
            using var repo = await CreateTestRepoWithRemoteAsync("https://github.com/UserFork/azure-rest-api-specs.git");
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

        #region DiscoverRepoRootAsync Tests

        [Test]
        public async Task DiscoverRepoRootAsync_WithRepoRoot_ReturnsRepoRoot()
        {
            using var repo = await CreateTestRepoWithoutRemoteAsync();
            var result = await gitHelper.DiscoverRepoRootAsync(repo.DirectoryPath, CancellationToken.None);
            Assert.That(NormalizePath(result), Is.EqualTo(NormalizePath(repo.DirectoryPath)));
        }

        [Test]
        public async Task DiscoverRepoRootAsync_WithSubdirectory_ReturnsRepoRoot()
        {
            using var repo = await CreateTestRepoWithoutRemoteAsync();
            var subDir = Path.Combine(repo.DirectoryPath, "src", "nested", "deep");
            Directory.CreateDirectory(subDir);

            var result = await gitHelper.DiscoverRepoRootAsync(subDir, CancellationToken.None);
            Assert.That(NormalizePath(result), Is.EqualTo(NormalizePath(repo.DirectoryPath)));
        }

        [Test]
        public async Task DiscoverRepoRootAsync_WithFilePath_ReturnsRepoRoot()
        {
            using var repo = await CreateTestRepoWithoutRemoteAsync();
            var filePath = Path.Combine(repo.DirectoryPath, "test.txt");
            File.WriteAllText(filePath, "test content");

            var result = await gitHelper.DiscoverRepoRootAsync(filePath, CancellationToken.None);
            Assert.That(NormalizePath(result), Is.EqualTo(NormalizePath(repo.DirectoryPath)));
        }

        [Test]
        public void DiscoverRepoRootAsync_WithNonGitDirectory_ThrowsException()
        {
            using var tempDir = TempDirectory.Create("non_git");
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.DiscoverRepoRootAsync(tempDir.DirectoryPath, CancellationToken.None));
            Assert.That(ex.Message, Does.Contain("No git repository found"));
        }

        [Test]
        public void DiscoverRepoRootAsync_WithEmptyPath_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.DiscoverRepoRootAsync("", CancellationToken.None));
        }

        #endregion

        #region GetBranchNameAsync Tests

        [Test]
        public async Task GetBranchNameAsync_WithDefaultBranch_ReturnsBranchName()
        {
            using var repo = await CreateTestRepoWithoutRemoteAsync();
            await GitTestHelper.GitCommitAsync(repo.DirectoryPath, "Initial commit");

            var result = await gitHelper.GetBranchNameAsync(repo.DirectoryPath, CancellationToken.None);
            Assert.That(result, Is.EqualTo("master").Or.EqualTo("main"));
        }

        [Test]
        public async Task GetBranchNameAsync_WithCustomBranch_ReturnsBranchName()
        {
            using var repo = await CreateTestRepoWithoutRemoteAsync();
            await GitTestHelper.GitCommitAsync(repo.DirectoryPath, "Initial commit");
            await GitTestHelper.GitCreateBranchAsync(repo.DirectoryPath, "test-branch");

            var result = await gitHelper.GetBranchNameAsync(repo.DirectoryPath, CancellationToken.None);
            Assert.That(result, Is.EqualTo("test-branch"));
        }

        #endregion

        #region GetMergeBaseCommitShaAsync Tests

        [Test]
        public async Task GetMergeBaseCommitShaAsync_WithValidBranches_ReturnsMergeBaseSha()
        {
            using var repo = await CreateTestRepoWithoutRemoteAsync();

            // Create initial commit on default branch
            await GitTestHelper.GitCommitAsync(repo.DirectoryPath, "Initial commit");
            var defaultBranch = await gitHelper.GetBranchNameAsync(repo.DirectoryPath, CancellationToken.None);

            // Create and switch to feature branch
            await GitTestHelper.GitCreateBranchAsync(repo.DirectoryPath, "feature");
            await GitTestHelper.GitCommitAsync(repo.DirectoryPath, "Feature commit");

            // The merge base should be the initial commit
            var result = await gitHelper.GetMergeBaseCommitShaAsync(repo.DirectoryPath, defaultBranch, CancellationToken.None);

            // Result should be a valid SHA (40 hex characters)
            Assert.That(result, Has.Length.EqualTo(40));
            Assert.That(result, Does.Match("^[0-9a-f]{40}$"));
        }

        [Test]
        public async Task GetMergeBaseCommitShaAsync_WithInvalidBranch_ThrowsInvalidOperationException()
        {
            using var repo = await CreateTestRepoWithoutRemoteAsync();
            await GitTestHelper.GitCommitAsync(repo.DirectoryPath, "Initial commit");

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await gitHelper.GetMergeBaseCommitShaAsync(repo.DirectoryPath, "nonexistent-branch", CancellationToken.None));
        }

        #endregion

        #region GetRepoNameAsync Tests

        [Test]
        public async Task GetRepoNameAsync_WithValidRemote_ReturnsRepoName()
        {
            using var repo = await CreateTestRepoWithRemoteAsync("https://github.com/Azure/azure-sdk-tools.git");
            var result = await gitHelper.GetRepoNameAsync(repo.DirectoryPath, CancellationToken.None);
            Assert.That(result, Is.EqualTo("azure-sdk-tools"));
        }

        [Test]
        public void GetRepoNameAsync_WithEmptyPath_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await gitHelper.GetRepoNameAsync("", CancellationToken.None));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Normalizes a path for cross-platform comparison.
        /// Git CLI returns forward slashes on Windows, while .NET uses backslashes.
        /// </summary>
        private static string NormalizePath(string path) => path.Replace('\\', '/');

        private static async Task<TempDirectory> CreateTestRepoWithRemoteAsync(string url)
        {
            var temp = TempDirectory.Create("gitrepo");
            await GitTestHelper.GitInitAsync(temp.DirectoryPath);
            await GitTestHelper.GitRemoteAddAsync(temp.DirectoryPath, "origin", url);
            return temp;
        }

        private static async Task<TempDirectory> CreateTestRepoWithoutRemoteAsync()
        {
            var temp = TempDirectory.Create("gitrepo");
            await GitTestHelper.GitInitAsync(temp.DirectoryPath);
            return temp;
        }

        #endregion
    }
}
