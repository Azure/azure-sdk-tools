// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class SdkBuildToolTests
{
    #region Test Constants

    private const string EngDirectoryName = "eng";

    // Common test file contents
    private const string InvalidJsonContent = "{ invalid json }";

    // Common error message patterns
    private const string InvalidProjectPathError = "Failed to find the language from package path";
    private const string FailedToDiscoverRepoError = "Failed to discover local sdk repo";
    private const string ConfigFileNotFoundError = "Configuration file not found";
    private const string JsonParsingError = "Error parsing JSON configuration";

    #endregion

    private SdkBuildTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private Mock<IPythonHelper> _mockPythonHelper;
    private Mock<INpxHelper> _mockNpxHelper;
    private Mock<IPowershellHelper> _mockPowerShellHelper;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
    private TestLogger<SdkBuildTool> _logger;
    private TempDirectory _tempDirectory;
    private List<LanguageService> _languageServices;
    private Mock<ICommonValidationHelpers> _commonValidationHelpers;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockPythonHelper = new Mock<IPythonHelper>();
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _mockPowerShellHelper = new Mock<IPowershellHelper>();
        _logger = new TestLogger<SdkBuildTool>();
        _commonValidationHelpers = new Mock<ICommonValidationHelpers>();

        var languageLogger = new TestLogger<LanguageService>();
        var mockMicrohostAgent = new Mock<ICopilotAgentRunner>();
        // Create temp directory for tests
        _tempDirectory = TempDirectory.Create("SdkBuildToolTests");
        _languageServices = [
            new PythonLanguageService(_mockProcessHelper.Object, _mockPythonHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
            new JavaLanguageService(_mockProcessHelper.Object, _mockGitHelper.Object, new Mock<IMavenHelper>().Object, mockMicrohostAgent.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
            new JavaScriptLanguageService(_mockProcessHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
            new GoLanguageService(_mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
            new DotnetLanguageService(_mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>())
        ];

        // Create the tool instance
        _tool = new SdkBuildTool(
            _mockGitHelper.Object,
            _logger,
            _languageServices
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
    public async Task BuildSdkAsync_EmptyPath_ReturnsFailure()
    {
        // Act
        var result = await _tool.BuildSdkAsync(string.Empty);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("required and cannot be empty"));
    }

    [Test]
    public async Task BuildSdkAsync_RelativePath_ResolvesToAbsolute()
    {
        // Arrange - create a test directory structure
        var projectDir = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "project");
        Directory.CreateDirectory(projectDir);

        // Save the current directory
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            // Change to the temp directory
            Directory.SetCurrentDirectory(_tempDirectory.DirectoryPath);

            // Mock GitHelper for the resolved path
            _mockGitHelper
                .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("azure-sdk-for-python");
            _mockGitHelper
                .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_tempDirectory.DirectoryPath);

            // Act - use relative path
            var result = await _tool.BuildSdkAsync("./sdk/project");

            // Assert - should successfully resolve and process
            Assert.That(result.Result, Is.EqualTo("noop")); // Python project skips build
            Assert.That(result.Message, Does.Contain("Python SDK project detected"));
        }
        finally
        {
            // Restore the original directory
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Test]
    public async Task BuildSdkAsync_PythonProject_SkipsBuild()
    {
        // Arrange
        var pythonProjectPath = Path.Combine(_tempDirectory.DirectoryPath, "test-python-sdk");
        Directory.CreateDirectory(pythonProjectPath);

        // Mock GitHelper to return a Python SDK repo name
        _mockGitHelper
            .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-python");
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);

        // Act
        var result = await _tool.BuildSdkAsync(pythonProjectPath);

        // Assert
        Assert.That(result.Result, Is.EqualTo("noop"));
        Assert.That(result.Message, Does.Contain("Python SDK project detected"));
        Assert.That(result.Message, Does.Contain("Skipping build step"));
    }

    [Test]
    public async Task BuildSdkAsync_GitHelperFailsToDiscoverRepo_ReturnsFailure()
    {
        // Arrange
        _mockGitHelper
            .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-net");
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(FailedToDiscoverRepoError));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FailedToDiscoverRepoError));
    }

    [Test]
    public async Task BuildSdkAsync_ConfigFileNotFound_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_tempDirectory.DirectoryPath);

        // Mock the SpecGenSdkConfigHelper to throw an exception for missing config
        _mockSpecGenSdkConfigHelper
            .Setup(x => x.GetConfigurationAsync(It.IsAny<string>(), SpecGenSdkConfigType.Build))
            .ThrowsAsync(new InvalidOperationException("Neither 'packageOptions/buildScript/command' nor 'packageOptions/buildScript/path' found in configuration."));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Neither 'packageOptions/buildScript/command' nor 'packageOptions/buildScript/path' found in configuration"));
    }

    [Test]
    public async Task BuildSdkAsync_InvalidJsonConfig_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_tempDirectory.DirectoryPath);

        // Mock the SpecGenSdkConfigHelper to throw a JSON parsing exception
        _mockSpecGenSdkConfigHelper
            .Setup(x => x.GetConfigurationAsync(It.IsAny<string>(), SpecGenSdkConfigType.Build))
            .ThrowsAsync(new InvalidOperationException("Error parsing JSON configuration: Invalid JSON"));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Error parsing JSON configuration: Invalid JSON"));
    }

    #endregion

    #region New Build Configuration Tests

    [Test]
    public async Task BuildSdkAsync_ConfigurationFileNotFound_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
        _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_tempDirectory.DirectoryPath);

        // Mock the SpecGenSdkConfigHelper to throw when config file is not found
        _mockSpecGenSdkConfigHelper
            .Setup(x => x.GetConfigurationAsync(It.IsAny<string>(), SpecGenSdkConfigType.Build))
            .ThrowsAsync(new FileNotFoundException("Configuration file not found"));

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Configuration file not found"));
        _mockGitHelper.Verify(x => x.DiscoverRepoRootAsync(_tempDirectory.DirectoryPath, It.IsAny<CancellationToken>()), Times.AtMost(2));
        _mockGitHelper.Verify(x => x.GetRepoNameAsync(_tempDirectory.DirectoryPath, It.IsAny<CancellationToken>()), Times.AtMost(2));
        _mockSpecGenSdkConfigHelper.Verify(x => x.GetConfigurationAsync(_tempDirectory.DirectoryPath, SpecGenSdkConfigType.Build), Times.Once);
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
