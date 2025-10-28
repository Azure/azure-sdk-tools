using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using LibGit2Sharp;
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
            gitHelper = new GitHelper(mockGitHubService.Object, logger);
        }

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
            Assert.That(ex.Message, Is.EqualTo("Unable to determine remote URL."));
        }

        [Test]
        public void GetRepoRemoteUri_WithNonGitDirectory_ThrowsException()
        {
            using var tempDir = TempDirectory.Create("non_git");
            Assert.Throws<InvalidOperationException>(() => gitHelper.GetRepoRemoteUri(tempDir.DirectoryPath));
        }

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

        #region Helper Methods

        private static TempDirectory CreateTestRepoWithRemote(string url)
        {
            var temp = TempDirectory.Create("gitrepo");
            Repository.Init(temp.DirectoryPath);
            using var repo = new Repository(temp.DirectoryPath);
            repo.Network.Remotes.Add("origin", url);
            return temp;
        }

        private static TempDirectory CreateTestRepoWithoutRemote()
        {
            var temp = TempDirectory.Create("gitrepo");
            Repository.Init(temp.DirectoryPath);
            return temp;
        }

        #endregion
    }
}
