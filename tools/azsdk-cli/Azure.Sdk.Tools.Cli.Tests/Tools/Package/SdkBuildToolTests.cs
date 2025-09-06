// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class SdkBuildToolTests
{
    #region Test Constants

    private const string EngDirectoryName = "eng";

    // Common test file contents
    private const string InvalidJsonContent = "{ invalid json }";

    // Common error message patterns
    private const string InvalidProjectPathError = "Path does not exist";
    private const string FailedToDiscoverRepoError = "Failed to discover local sdk repo";
    private const string ConfigFileNotFoundError = "Configuration file not found";
    private const string JsonParsingError = "Error parsing JSON configuration";

    #endregion

    private SdkBuildTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<IOutputHelper> _mockOutputHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private Mock<ISdkRepoConfigHelper> _mockSdkRepoConfigHelper;
    private TestLogger<SdkBuildTool> _logger;
    private string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockOutputHelper = new Mock<IOutputHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockSdkRepoConfigHelper = new Mock<ISdkRepoConfigHelper>();
        _logger = new TestLogger<SdkBuildTool>();

        // Create temp directory for tests
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SdkBuildToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Create the tool instance
        _tool = new SdkBuildTool(
            _mockGitHelper.Object,
            _logger,
            _mockOutputHelper.Object,
            _mockProcessHelper.Object,
            _mockSdkRepoConfigHelper.Object
        );
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up temp directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region BuildSdkAsync Tests

    [Test]
    public async Task BuildSdkAsync_InvalidProjectPath_ReturnsFailure()
    {
        // Act
        var result = await _tool.BuildSdkAsync("/nonexistent/path");

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(InvalidProjectPathError));
    }

    [Test]
    public async Task BuildSdkAsync_PythonProject_SkipsBuild()
    {
        // Arrange - Create a path and mock GitHelper to return Python repo URI
        var pythonProjectPath = Path.Combine(_tempDirectory, "sdk", "storage", "azure-storage-blob");
        Directory.CreateDirectory(pythonProjectPath);
        
        // Mock GitHelper to return a Python SDK remote URI
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRoot(pythonProjectPath))
            .Returns(_tempDirectory);
        _mockGitHelper
            .Setup(x => x.GetRepoRemoteUri(_tempDirectory))
            .Returns(new Uri("https://github.com/Azure/azure-sdk-for-python.git"));

        // Act
        var result = await _tool.BuildSdkAsync(pythonProjectPath);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain("Python SDK project detected"));
        Assert.That(result.Message, Does.Contain("Skipping build step"));
    }

    [Test]
    public async Task BuildSdkAsync_GitHelperFailsToDiscoverRepo_ReturnsFailure()
    {
        // Arrange
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRoot(_tempDirectory))
            .Throws(new Exception(FailedToDiscoverRepoError));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FailedToDiscoverRepoError));
    }

    [Test]
    public async Task BuildSdkAsync_ConfigFileNotFound_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        _mockGitHelper.Setup(x => x.GetRepoName(_tempDirectory)).Returns("azure-sdk-for-net");
        _mockGitHelper
            .Setup(x => x.GetRepoRemoteUri(_tempDirectory))
            .Returns(new Uri("https://github.com/Azure/azure-sdk-for-net.git"));

        // Mock the SdkRepoConfigHelper to throw an exception for missing config
        _mockSdkRepoConfigHelper
            .Setup(x => x.GetBuildConfigurationAsync(_tempDirectory, "azure-sdk-for-net"))
            .ThrowsAsync(new InvalidOperationException("Neither 'packageOptions/buildScript/command' nor 'packageOptions/buildScript/path' found in configuration."));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Failed to get build configuration"));
    }

    [Test]
    public async Task BuildSdkAsync_InvalidJsonConfig_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        _mockGitHelper.Setup(x => x.GetRepoName(_tempDirectory)).Returns("azure-sdk-for-net");
        _mockGitHelper
            .Setup(x => x.GetRepoRemoteUri(_tempDirectory))
            .Returns(new Uri("https://github.com/Azure/azure-sdk-for-net.git"));

        // Mock the SdkRepoConfigHelper to throw a JSON parsing exception
        _mockSdkRepoConfigHelper
            .Setup(x => x.GetBuildConfigurationAsync(_tempDirectory, "azure-sdk-for-net"))
            .ThrowsAsync(new InvalidOperationException("Error parsing JSON configuration: Invalid JSON"));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Failed to get build configuration"));
    }

    #endregion

    #region New Build Configuration Tests

    [Test]
    public async Task BuildSdkAsync_ConfigurationFileNotFound_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        _mockGitHelper.Setup(x => x.GetRepoName(_tempDirectory)).Returns("azure-sdk-for-net");
        _mockGitHelper
            .Setup(x => x.GetRepoRemoteUri(_tempDirectory))
            .Returns(new Uri("https://github.com/Azure/azure-sdk-for-net.git"));

        // Mock the SdkRepoConfigHelper to throw when config file is not found
        _mockSdkRepoConfigHelper
            .Setup(x => x.GetBuildConfigurationAsync(_tempDirectory, "azure-sdk-for-net"))
            .ThrowsAsync(new FileNotFoundException("Configuration file not found"));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Configuration file not found"));
        _mockGitHelper.Verify(x => x.DiscoverRepoRoot(_tempDirectory), Times.Once);
        _mockGitHelper.Verify(x => x.GetRepoName(_tempDirectory), Times.Once);
        _mockSdkRepoConfigHelper.Verify(x => x.GetBuildConfigurationAsync(_tempDirectory, "azure-sdk-for-net"), Times.Once);
    }

    #endregion

    #region Command Tests

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectStructure()
    {
        // Act
        var command = _tool.GetCommand();

        // Assert
        Assert.That(command.Name, Is.EqualTo("build"));
        Assert.That(command.Description, Does.Contain("Builds SDK source code"));
    }

    #endregion
}
