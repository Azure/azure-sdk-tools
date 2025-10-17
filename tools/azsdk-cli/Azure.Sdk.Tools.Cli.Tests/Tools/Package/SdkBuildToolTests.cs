// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;

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
    private Mock<IProcessHelper> _mockProcessHelper;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
    private TestLogger<SdkBuildTool> _logger;
    private TempDirectory _tempDirectory;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        _logger = new TestLogger<SdkBuildTool>();

        // Create temp directory for tests
        _tempDirectory = TempDirectory.Create("SdkBuildToolTests");

        // Create the tool instance
        _tool = new SdkBuildTool(
            _mockGitHelper.Object,
            _logger,
            _mockProcessHelper.Object,
            _mockSpecGenSdkConfigHelper.Object
        );
    }

    [TearDown]
    public void TearDown()
    {
        _tempDirectory.Dispose();
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
        // Arrange
    var pythonProjectPath = Path.Combine(_tempDirectory.DirectoryPath, "test-python-sdk");
        Directory.CreateDirectory(pythonProjectPath);

        // Mock GitHelper to return a Python SDK repo name
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRoot(pythonProjectPath))
            .Returns(_tempDirectory.DirectoryPath);
        _mockGitHelper
            .Setup(x => x.GetRepoName(_tempDirectory.DirectoryPath))
            .Returns("azure-sdk-for-python");

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
            .Setup(x => x.DiscoverRepoRoot(_tempDirectory.DirectoryPath))
            .Throws(new Exception(FailedToDiscoverRepoError));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FailedToDiscoverRepoError));
    }

    [Test]
    public async Task BuildSdkAsync_ConfigFileNotFound_ReturnsError()
    {
        // Arrange
    _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory.DirectoryPath)).Returns(_tempDirectory.DirectoryPath);
    _mockGitHelper.Setup(x => x.GetRepoName(_tempDirectory.DirectoryPath)).Returns("azure-sdk-for-net");
        _mockGitHelper
            .Setup(x => x.GetRepoRemoteUri(_tempDirectory.DirectoryPath))
            .Returns(new Uri("https://github.com/Azure/azure-sdk-for-net.git"));

        // Mock the SpecGenSdkConfigHelper to throw an exception for missing config
        _mockSpecGenSdkConfigHelper
            .Setup(x => x.GetBuildConfigurationAsync(_tempDirectory.DirectoryPath))
            .ThrowsAsync(new InvalidOperationException("Neither 'packageOptions/buildScript/command' nor 'packageOptions/buildScript/path' found in configuration."));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Failed to get build configuration"));
    }

    [Test]
    public async Task BuildSdkAsync_InvalidJsonConfig_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory.DirectoryPath)).Returns(_tempDirectory.DirectoryPath);
        _mockGitHelper.Setup(x => x.GetRepoName(_tempDirectory.DirectoryPath)).Returns("azure-sdk-for-net");
        _mockGitHelper
            .Setup(x => x.GetRepoRemoteUri(_tempDirectory.DirectoryPath))
            .Returns(new Uri("https://github.com/Azure/azure-sdk-for-net.git"));

        // Mock the SpecGenSdkConfigHelper to throw a JSON parsing exception
        _mockSpecGenSdkConfigHelper
            .Setup(x => x.GetBuildConfigurationAsync(_tempDirectory.DirectoryPath))
            .ThrowsAsync(new InvalidOperationException("Error parsing JSON configuration: Invalid JSON"));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Failed to get build configuration"));
    }

    #endregion

    #region New Build Configuration Tests

    [Test]
    public async Task BuildSdkAsync_ConfigurationFileNotFound_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory.DirectoryPath)).Returns(_tempDirectory.DirectoryPath);
        _mockGitHelper.Setup(x => x.GetRepoName(_tempDirectory.DirectoryPath)).Returns("azure-sdk-for-net");
        _mockGitHelper
            .Setup(x => x.GetRepoRemoteUri(_tempDirectory.DirectoryPath))
            .Returns(new Uri("https://github.com/Azure/azure-sdk-for-net.git"));

        // Mock the SpecGenSdkConfigHelper to throw when config file is not found
        _mockSpecGenSdkConfigHelper
            .Setup(x => x.GetBuildConfigurationAsync(_tempDirectory.DirectoryPath))
            .ThrowsAsync(new FileNotFoundException("Configuration file not found"));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Configuration file not found"));
        _mockGitHelper.Verify(x => x.DiscoverRepoRoot(_tempDirectory.DirectoryPath), Times.Once);
        _mockGitHelper.Verify(x => x.GetRepoName(_tempDirectory.DirectoryPath), Times.Once);
        _mockSpecGenSdkConfigHelper.Verify(x => x.GetBuildConfigurationAsync(_tempDirectory.DirectoryPath), Times.Once);
    }

    #endregion

    #region Command Tests

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectStructure()
    {
        // Act
        var command = _tool.GetCommandInstances().First();

        // Assert
        Assert.That(command.Name, Is.EqualTo("build"));
        Assert.That(command.Description, Does.Contain("Builds SDK source code"));
    }

    #endregion
}
