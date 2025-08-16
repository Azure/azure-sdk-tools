using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    /// <summary>
    /// Test suite for FileValidationTool focusing on core validation functionality.
    /// Covers: basic validation, missing files detection, error handling, and edge cases.
    /// </summary>
    [TestFixture]
    internal class FileValidationToolTests
    {
        private TestLogger<FileValidationTool> logger;
        private FileValidationTool fileValidationTool;
        private string tempDirectory;
        private string localFilesDirectory;

        [SetUp]
        public void Setup()
        {
            logger = new TestLogger<FileValidationTool>();

            var outputServiceMock = new Mock<IOutputHelper>();
            outputServiceMock.Setup(x => x.Format(It.IsAny<object>())).Returns<object>(obj => obj?.ToString() ?? "");

            var gitHubService = new MockGitHubService();
            fileValidationTool = new FileValidationTool(logger, outputServiceMock.Object, gitHubService);

            // Create a temporary directory for testing
            tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);

            // Set up a local files directory within temp directory
            localFilesDirectory = Path.Combine(tempDirectory, ".github", "prompts");
            Directory.CreateDirectory(localFilesDirectory);
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
        public async Task Validate_Workspace_Files_All_Files_Present_Valid()
        {
            // Arrange - Create all expected files locally
            await File.WriteAllTextAsync(Path.Combine(localFilesDirectory, "README.md"), "content");
            await File.WriteAllTextAsync(Path.Combine(localFilesDirectory, "prompt1.md"), "content");
            await File.WriteAllTextAsync(Path.Combine(localFilesDirectory, "prompt2.md"), "content");

            // Act
            var result = await fileValidationTool.ValidateWorkspaceFiles(
                "Azure", "azure-rest-api-specs", ".github/prompts", localFilesDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.That(result.TotalSourceFiles, Is.EqualTo(3));
            Assert.That(result.MissingCount, Is.EqualTo(0));
            Assert.IsNull(result.MissingFiles);
            Assert.That(result.Message, Contains.Substring("All 3 files"));
            Assert.That(result.Message, Contains.Substring("are present in local workspace"));
        }

        [Test]
        public async Task Validate_Workspace_Files_Some_Files_Missing_Invalid()
        {
            // Arrange - Create only some of the expected files locally
            await File.WriteAllTextAsync(Path.Combine(localFilesDirectory, "README.md"), "content");
            // Missing: prompt1.md and prompt2.md

            // Act
            var result = await fileValidationTool.ValidateWorkspaceFiles(
                "Azure", "azure-rest-api-specs", ".github/prompts", localFilesDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.That(result.TotalSourceFiles, Is.EqualTo(3));
            Assert.That(result.MissingCount, Is.EqualTo(2));
            Assert.IsNotNull(result.MissingFiles);
            Assert.Contains("prompt1.md", result.MissingFiles);
            Assert.Contains("prompt2.md", result.MissingFiles);
            Assert.That(result.Message, Contains.Substring("Missing 2 files"));
            Assert.That(result.Message, Contains.Substring("prompt1.md, prompt2.md"));
        }

        [Test]
        public async Task Validate_Workspace_Files_All_Files_Missing_Invalid()
        {
            // Arrange - Don't create any files locally
            // Local directory exists but is empty

            // Act
            var result = await fileValidationTool.ValidateWorkspaceFiles(
                "Azure", "azure-rest-api-specs", ".github/prompts", localFilesDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.That(result.TotalSourceFiles, Is.EqualTo(3));
            Assert.That(result.MissingCount, Is.EqualTo(3));
            Assert.IsNotNull(result.MissingFiles);
            Assert.Contains("README.md", result.MissingFiles);
            Assert.Contains("prompt1.md", result.MissingFiles);
            Assert.Contains("prompt2.md", result.MissingFiles);
            Assert.That(result.Message, Contains.Substring("Missing 3 files"));
        }

        [Test]
        public async Task Validate_Workspace_Files_Local_Directory_Does_Not_Exist_Invalid()
        {
            // Arrange - Remove the local directory
            Directory.Delete(localFilesDirectory, true);

            // Act
            var result = await fileValidationTool.ValidateWorkspaceFiles(
                "Azure", "azure-rest-api-specs", ".github/prompts", localFilesDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.That(result.TotalSourceFiles, Is.EqualTo(3));
            Assert.That(result.MissingCount, Is.EqualTo(3));
            Assert.IsNotNull(result.MissingFiles);
            Assert.That(result.Message, Contains.Substring("Missing 3 files"));
        }

        [Test]
        public async Task Validate_Workspace_Files_Non_Existent_Source_Path_Invalid()
        {
            // Act
            var result = await fileValidationTool.ValidateWorkspaceFiles(
                "Azure", "azure-rest-api-specs", "non-existent-path", localFilesDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid); // Should be true because 0 files are "present"
            Assert.That(result.TotalSourceFiles, Is.EqualTo(0));
            Assert.That(result.MissingCount, Is.EqualTo(0));
            Assert.That(result.Message, Contains.Substring("All 0 files"));
        }

        [Test]
        public async Task Validate_Workspace_Files_Empty_Source_Directory_Valid()
        {
            // Act
            var result = await fileValidationTool.ValidateWorkspaceFiles(
                "testowner", "testrepo", "empty-directory", localFilesDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.That(result.TotalSourceFiles, Is.EqualTo(0));
            Assert.That(result.MissingCount, Is.EqualTo(0));
            Assert.IsNull(result.MissingFiles);
            Assert.That(result.Message, Contains.Substring("All 0 files"));
        }

        [Test]
        public async Task Validate_Workspace_Files_Different_Local_Path_Valid()
        {
            // Arrange - Create files in a different local path
            var customLocalPath = Path.Combine(tempDirectory, "custom", "prompts");
            Directory.CreateDirectory(customLocalPath);
            await File.WriteAllTextAsync(Path.Combine(customLocalPath, "README.md"), "content");
            await File.WriteAllTextAsync(Path.Combine(customLocalPath, "prompt1.md"), "content");
            await File.WriteAllTextAsync(Path.Combine(customLocalPath, "prompt2.md"), "content");

            // Act
            var result = await fileValidationTool.ValidateWorkspaceFiles(
                "Azure", "azure-rest-api-specs", ".github/prompts", customLocalPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.That(result.TotalSourceFiles, Is.EqualTo(3));
            Assert.That(result.MissingCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Validate_Workspace_Files_Partial_Match_Shows_Correct_Missing_Files()
        {
            // Arrange - Create only the README file
            await File.WriteAllTextAsync(Path.Combine(localFilesDirectory, "README.md"), "content");

            // Act
            var result = await fileValidationTool.ValidateWorkspaceFiles(
                "Azure", "azure-rest-api-specs", ".github/prompts", localFilesDirectory);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.That(result.TotalSourceFiles, Is.EqualTo(3));
            Assert.That(result.MissingCount, Is.EqualTo(2));
            Assert.IsNotNull(result.MissingFiles);
            Assert.That(result.MissingFiles.Count, Is.EqualTo(2));
            // Should not include README.md since it exists
            Assert.That(result.MissingFiles, Does.Not.Contain("README.md"));
            // Should include the missing files
            Assert.Contains("prompt1.md", result.MissingFiles);
            Assert.Contains("prompt2.md", result.MissingFiles);
        }
    }
}
