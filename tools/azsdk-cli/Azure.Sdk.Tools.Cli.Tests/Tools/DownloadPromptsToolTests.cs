using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.MockServices;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools;
using Moq;
using System.IO;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    /// <summary>
    /// Simplified test suite for DownloadPromptsTool focusing on core functionality.
    /// Covers: basic download, file lists, error handling, and edge cases.
    /// </summary>
    [TestFixture]
    internal class DownloadPromptsToolTests
    {
        private TestLogger<DownloadPromptsTool> logger;
        private DownloadPromptsTool? downloadPromptsTool;
        private string tempDirectory;

        [SetUp]
        public void Setup()
        {
            logger = new TestLogger<DownloadPromptsTool>();

            var outputServiceMock = new Mock<IOutputService>();
            outputServiceMock.Setup(x => x.Format(It.IsAny<object>())).Returns<object>(obj => obj?.ToString() ?? "");

            // Create a temporary directory for testing
            tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public async Task Download_Prompts_Valid()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);

            // Act
            var result = await downloadPromptsTool.DownloadPrompts(
                "Azure", "azure-rest-api-specs", ".github/prompts", tempDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.That(result.TotalFiles, Is.EqualTo(3));
            Assert.That(result.DownloadedCount, Is.EqualTo(3));
            Assert.IsTrue(File.Exists(Path.Combine(tempDirectory, "README.md")));
        }

        [Test]
        public async Task Download_Prompts_With_Specific_File_List_Valid()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);

            // Act
            var result = await downloadPromptsTool.DownloadPrompts(
                "Azure", "azure-rest-api-specs", ".github/prompts", tempDirectory, "prompt1.md,prompt2.md");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.That(result.TotalFiles, Is.EqualTo(2));
            Assert.That(result.DownloadedCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Download_Prompts_Non_Existent_Path()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);

            // Act
            var result = await downloadPromptsTool.DownloadPrompts(
                "Azure", "azure-rest-api-specs", "non-existent-path", tempDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
            Assert.That(result.TotalFiles, Is.EqualTo(0));
        }

        [Test]
        public async Task Download_Prompts_In_Source_Repository_Skip_Download()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);

            // Create directory structure simulating source repository
            var repoDirectory = Path.Combine(tempDirectory, "azure-rest-api-specs");
            Directory.CreateDirectory(Path.Combine(repoDirectory, ".git"));
            var destinationPath = Path.Combine(repoDirectory, "test-folder");
            Directory.CreateDirectory(destinationPath);

            // Act
            var result = await downloadPromptsTool.DownloadPrompts(
                "Azure", "azure-rest-api-specs", ".github/prompts", destinationPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.That(result.TotalFiles, Is.EqualTo(0));
            Assert.IsTrue(result.Message.Contains("We are in source repository"));
        }

        [Test]
        public async Task Download_Prompts_Empty_Directory_Invalid()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);

            // Act
            var result = await downloadPromptsTool.DownloadPrompts(
                "testowner", "testrepo", "empty-directory", tempDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
            Assert.That(result.TotalFiles, Is.EqualTo(0));
            Assert.That(result.DownloadedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Download_Prompts_Single_File_Valid()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);

            // Act
            var result = await downloadPromptsTool.DownloadPrompts(
                "testowner", "testrepo", ".github/prompts", tempDirectory, "single-file.md");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.That(result.TotalFiles, Is.EqualTo(1));
            Assert.That(result.DownloadedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Download_Prompts_Existing_File_Skip_Download()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);

            // Pre-create a file to simulate existing file
            var existingFilePath = Path.Combine(tempDirectory, "README.md");
            await File.WriteAllTextAsync(existingFilePath, "existing content");

            // Act
            var result = await downloadPromptsTool.DownloadPrompts(
                "testowner", "testrepo", ".github/prompts", tempDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.That(result.TotalFiles, Is.EqualTo(3));
            Assert.That(result.DownloadedCount, Is.EqualTo(2)); // Should skip README.md
            
            // Verify existing file wasn't overwritten
            var fileContent = await File.ReadAllTextAsync(existingFilePath);
            Assert.That(fileContent, Is.EqualTo("existing content"));
        }

        [Test]
        public async Task Download_Prompts_Valid_Parameters_Should_Create_Destination_Directory()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);
            
            var nonExistentDirectory = Path.Combine(tempDirectory, "new-directory");

            // Act
            var result = await downloadPromptsTool.DownloadPrompts(
                "testowner", "testrepo", ".github/prompts", nonExistentDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsTrue(Directory.Exists(nonExistentDirectory));
            Assert.That(result.TotalFiles, Is.EqualTo(3));
            Assert.That(result.DownloadedCount, Is.EqualTo(3));
        }

        [Test]
        public async Task Download_Prompts_Default_Parameters_Should_Use_Defaults()
        {
            // Arrange
            var gitHubService = new MockGitHubService();
            downloadPromptsTool = new DownloadPromptsTool(logger, Mock.Of<IOutputService>(), gitHubService);

            // Act - using minimal parameters, others should default
            var result = await downloadPromptsTool.DownloadPrompts(
                "Azure", "azure-rest-api-specs", ".github/prompts", tempDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.TotalFiles > 0);
            Assert.That(result.TotalFiles, Is.EqualTo(result.DownloadedCount));
        }
    }
}
