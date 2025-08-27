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
public class SdkCodeToolTests
{
    #region Test Constants
    
    private const string ValidCommitSha = "1234567890abcdef1234567890abcdef12345678";
    private const string InvalidCommitSha = "abc123";
    private const string DefaultSpecRepo = "Azure/azure-rest-api-specs";
    private const string RemoteTspConfigUrl = "https://example.com/tspconfig.yaml";
    private const string TspConfigFileName = "tspconfig.yaml";
    private const string TspLocationFileName = "tsp-location.yaml";
    private const string SpecGenConfigFileName = "spec-gen-sdk-config.json";
    private const string EngDirectoryName = "eng";
    private const string GitDirectoryName = ".git";
    
    // Common test file contents
    private const string TestTspConfigContent = "# test tspconfig.yaml";
    private const string TestTspLocationContent = "# test tsp-location.yaml";
    private const string InvalidJsonContent = "{ invalid json }";
    
    // Common error message patterns
    private const string BothPathsEmptyError = "Both 'tspconfig.yaml' and 'tsp-location.yaml' paths aren't provided";
    private const string FileNotExistError = "does not exist";
    private const string TspConfigNotExistError = "The 'tspconfig.yaml' file does not exist at the specified path";
    private const string InvalidShaError = "not a valid commit SHA";
    private const string InvalidShaWithRepoDiscoveryError = "Invalid commit SHA provided and failed to discover local azure-rest-api-specs repo";
    private const string RepoNameNotProvidedError = "repository name is not provided";
    private const string DirectoryNotExistError = "does not provide or exist";
    private const string TspClientInitFailedError = "tsp-client init failed";
    private const string InvalidProjectPathError = "Invalid project path";
    private const string FailedToDiscoverRepoError = "Failed to discover local sdk repo";
    private const string ConfigFileNotFoundError = "Configuration file not found";
    private const string JsonParsingError = "Error parsing JSON configuration";
    
    // Common success messages
    private const string SdkGenerationSuccessMessage = "SDK generation completed successfully";
    private const string SdkRegenerationSuccessMessage = "SDK re-generation completed successfully";
    private const string ProcessSuccessOutput = "Success";
    private const string ProcessErrorOutput = "Error occurred";
    
    #endregion

    private SdkCodeTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<INpxHelper> _mockNpxHelper;
    private Mock<IOutputHelper> _mockOutputHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private TestLogger<SdkCodeTool> _logger;
    private string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _mockOutputHelper = new Mock<IOutputHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _logger = new TestLogger<SdkCodeTool>();

        // Create temp directory for tests
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SdkCodeToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Create the tool instance
        _tool = new SdkCodeTool(
            _mockGitHelper.Object,
            _logger,
            _mockNpxHelper.Object,
            _mockOutputHelper.Object,
            _mockProcessHelper.Object
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

    #region GenerateSdk Tests

    [Test]
    public async Task GenerateSdk_BothPathsEmpty_ReturnsFailure()
    {
        // Act
        var result = await _tool.GenerateSdk("/some/path", null, null, null, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(BothPathsEmptyError));
    }

    [Test]
    public async Task GenerateSdk_WithTspLocationPath_CallsRunTspUpdate()
    {
        // Arrange
        var tspLocationPath = Path.Combine(_tempDirectory, TspLocationFileName);
        File.WriteAllText(tspLocationPath, TestTspLocationContent);

        var expectedResult = new ProcessResult { ExitCode = 0 };
        expectedResult.AppendStdout(ProcessSuccessOutput);
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.GenerateSdk("/some/path", null, null, null, tspLocationPath, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain(SdkRegenerationSuccessMessage));
        _mockNpxHelper.Verify(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdk_WithTspLocationPath_FileNotExists_ReturnsFailure()
    {
        // Arrange
        var tspLocationPath = Path.Combine(_tempDirectory, "nonexistent-" + TspLocationFileName);

        // Act
        var result = await _tool.GenerateSdk("/some/path", null, null, null, tspLocationPath, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FileNotExistError));
    }

    [Test]
    public async Task GenerateSdk_WithTspConfigPath_RemoteUrl_CallsRunTspInit()
    {
        // Arrange
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        var expectedResult = new ProcessResult { ExitCode = 0 };
        expectedResult.AppendStdout(ProcessSuccessOutput);
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.GenerateSdk(_tempDirectory, RemoteTspConfigUrl, InvalidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain(SdkGenerationSuccessMessage));
        _mockNpxHelper.Verify(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdk_WithLocalTspConfigPath_ValidInputs_CallsRunTspInit()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);
        
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        var expectedResult = new ProcessResult { ExitCode = 0 };
        expectedResult.AppendStdout(ProcessSuccessOutput);
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act - Use a valid SHA for this test
        var result = await _tool.GenerateSdk(_tempDirectory, tspConfigPath, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain(SdkGenerationSuccessMessage));
        _mockNpxHelper.Verify(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdk_WithLocalTspConfigPath_EmptySpecCommitSha_ReturnsFailure()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);
        
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act
        var result = await _tool.GenerateSdk(_tempDirectory, tspConfigPath, null, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(InvalidShaWithRepoDiscoveryError));
    }

    [Test]
    public async Task GenerateSdk_WithLocalTspConfigPath_InvalidSpecCommitSha_ReturnsError()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);
        
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act - Use an invalid SHA
        var result = await _tool.GenerateSdk(_tempDirectory, tspConfigPath, InvalidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(InvalidShaWithRepoDiscoveryError));
    }

    [Test]
    public async Task GenerateSdk_WithLocalTspConfigPath_FileNotExists_ReturnsError()
    {
        // Arrange
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        
        // Act - Use a non-existent file path
        var result = await _tool.GenerateSdk(_tempDirectory, "/nonexistent/" + TspConfigFileName, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FileNotExistError));
    }

    [Test]
    public async Task GenerateSdk_WithLocalTspConfigPath_EmptySpecRepoFullName_ReturnsError()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);
        
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act - Use empty repo name
        var result = await _tool.GenerateSdk(_tempDirectory, tspConfigPath, ValidCommitSha, null, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(RepoNameNotProvidedError));
    }

    [Test]
    public async Task GenerateSdk_TspClientInitFails_ReturnsFailure()
    {
        // Arrange
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        var failedResult = new ProcessResult { ExitCode = 1 };
        failedResult.AppendStderr(ProcessErrorOutput);
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        // Act
        var result = await _tool.GenerateSdk(_tempDirectory, RemoteTspConfigUrl, InvalidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(TspClientInitFailedError));
    }

    [Test]
    public async Task GenerateSdk_ExceptionThrown_ReturnsFailureWithExceptionMessage()
    {
        // Arrange
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act - Now remote URLs work properly
        var result = await _tool.GenerateSdk(_tempDirectory, RemoteTspConfigUrl, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Test exception"));
    }

    [Test]
    public async Task GenerateSdk_WithInvalidSdkRepoPath_ReturnsRealDirectoryError()
    {
        // Act - Use a non-existent directory path
        var result = await _tool.GenerateSdk("/this/path/does/not/exist", RemoteTspConfigUrl, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(DirectoryNotExistError));
    }

    #endregion

    #region BuildSdkAsync Tests

    [Test]
    public async Task BuildSdkAsync_InvalidProjectPath_ReturnsFailure()
    {
        // Act
        var result = await _tool.BuildSdkAsync("/nonexistent/path");

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(InvalidProjectPathError));
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
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FailedToDiscoverRepoError));
    }

    [Test]
    public async Task BuildSdkAsync_ConfigFileNotFound_ReturnsError()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act - Check for eng/spec-gen-sdk-config.json
        var result = await _tool.BuildSdkAsync(_tempDirectory);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(ConfigFileNotFoundError));
    }

    [Test]
    public async Task BuildSdkAsync_InvalidJsonConfig_ReturnsError()
    {
        // Arrange - Create invalid JSON config file to test real JSON parsing
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        
        var engDir = Path.Combine(_tempDirectory, EngDirectoryName);
        Directory.CreateDirectory(engDir);
        var configFile = Path.Combine(engDir, SpecGenConfigFileName);
        File.WriteAllText(configFile, InvalidJsonContent); // Invalid JSON

        // Act
        var result = await _tool.BuildSdkAsync(_tempDirectory);

        // Assert
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors?.First(), Does.Contain(JsonParsingError));
    }

    #endregion

    #region Command Tests

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectStructure()
    {
        // Act
        var command = _tool.GetCommand();

        // Assert
        Assert.That(command.Name, Is.EqualTo("code"));
        Assert.That(command.Description, Is.EqualTo("Azure SDK code tools"));
        Assert.That(command.Subcommands.Count, Is.EqualTo(2));
        
        var generateCommand = command.Subcommands.FirstOrDefault(c => c.Name == "generate");
        var buildCommand = command.Subcommands.FirstOrDefault(c => c.Name == "build");
        
        Assert.That(generateCommand, Is.Not.Null);
        Assert.That(buildCommand, Is.Not.Null);
    }

    #endregion
}
