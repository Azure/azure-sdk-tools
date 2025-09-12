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
            var testRepoPath = CreateTestRepoWithRemote("git@github.com:Azure/azure-rest-api-specs.git");

            try
            {
                var result = gitHelper.GetRepoRemoteUri(testRepoPath);

                Assert.That(result.ToString(), Is.EqualTo("https://github.com/Azure/azure-rest-api-specs.git"));
            }
            finally
            {
                CleanupTestRepo(testRepoPath);
            }
        }

        [Test]
        public void GetRepoRemoteUri_WithHttpsOrigin_ReturnsHttpsUri()
        {
            var testRepoPath = CreateTestRepoWithRemote("https://github.com/Azure/azure-rest-api-specs.git");

            try
            {
                var result = gitHelper.GetRepoRemoteUri(testRepoPath);

                Assert.That(result.ToString(), Is.EqualTo("https://github.com/Azure/azure-rest-api-specs.git"));
            }
            finally
            {
                CleanupTestRepo(testRepoPath);
            }
        }

        [Test]
        public void GetRepoRemoteUri_WithNoOrigin_ThrowsException()
        {
            var testRepoPath = CreateTestRepoWithoutRemote();

            try
            {
                var ex = Assert.Throws<InvalidOperationException>(() => gitHelper.GetRepoRemoteUri(testRepoPath));
                Assert.That(ex.Message, Is.EqualTo("Unable to determine remote URL."));
            }
            finally
            {
                CleanupTestRepo(testRepoPath);
            }
        }

        [Test]
        public void GetRepoRemoteUri_WithNonGitDirectory_ThrowsException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                Assert.Throws<InvalidOperationException>(() => gitHelper.GetRepoRemoteUri(tempDir));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public async Task GetRepoFullNameAsync_WithSubdirectoryPath_ReturnsCorrectFullName()
        {
            var testRepoPath = CreateTestRepoWithRemote("git@github.com:Azure/azure-rest-api-specs.git");
            var subDir = Path.Combine(testRepoPath, "subdirectory");
            Directory.CreateDirectory(subDir);
            mockGitHubService.Setup(x => x.GetGitHubParentRepoUrlAsync("Azure", "azure-rest-api-specs"))
                           .ReturnsAsync(string.Empty); // Not a fork

            try
            {
                var result = await gitHelper.GetRepoFullNameAsync(subDir);

                Assert.That(result, Is.EqualTo("Azure/azure-rest-api-specs"));
            }
            finally
            {
                CleanupTestRepo(testRepoPath);
            }
        }

        [Test]
        public async Task GetRepoFullNameAsync_WithForkRepoButDontFindUpstream_ReturnsDirectFullName()
        {
            var testRepoPath = CreateTestRepoWithRemote("https://github.com/UserFork/azure-rest-api-specs.git");
            
            try
            {
                var result = await gitHelper.GetRepoFullNameAsync(testRepoPath, findUpstreamParent: false);

                Assert.That(result, Is.EqualTo("UserFork/azure-rest-api-specs"));
            }
            finally
            {
                CleanupTestRepo(testRepoPath);
            }
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
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.GetRepoFullNameAsync(tempDir));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        #region Helper Methods

        private static string CreateTestRepoWithRemote(string url)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            Repository.Init(tempDir);
            using var repo = new Repository(tempDir);
            repo.Network.Remotes.Add("origin", url);

            return tempDir;
        }

        private static string CreateTestRepoWithoutRemote()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            Repository.Init(tempDir);
            return tempDir;
        }

        private static void CleanupTestRepo(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    // Remove read-only attributes from .git folder
                    var gitDir = Path.Combine(path, ".git");
                    if (Directory.Exists(gitDir))
                    {
                        foreach (var file in Directory.GetFiles(gitDir, "*", SearchOption.AllDirectories))
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                        }
                    }

                    Directory.Delete(path, true);
                }
                catch (Exception)
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        #endregion
    }
}
