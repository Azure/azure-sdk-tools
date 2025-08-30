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
public class SdkGenerationToolTests
{
    #region Test Constants
    
    private const string ValidCommitSha = "1234567890abcdef1234567890abcdef12345678";
    private const string InvalidCommitSha = "abc123";
    private const string DefaultSpecRepo = "Azure/azure-rest-api-specs";
    private const string RemoteTspConfigUrl = "https://github.com/Azure/azure-rest-api-specs/blob/dee71463cbde1d416c47cf544e34f7966a94ddcb/specification/contosowidgetmanager/Contoso.Management/tspconfig.yaml";
    private const string InvalidRemoteTspConfigUrl = "https://example.com/tspconfig.yaml";
    private const string TspConfigFileName = "tspconfig.yaml";
    private const string TspLocationFileName = "tsp-location.yaml";
    
    // Common test file contents
    private const string TestTspConfigContent = "# test tspconfig.yaml";
    private const string TestTspLocationContent = "# test tsp-location.yaml";
    
    // Common error message patterns
    private const string BothPathsEmptyError = "Both 'tspconfig.yaml' and 'tsp-location.yaml' paths aren't provided";
    private const string FileNotExistError = "does not exist";
    private const string InvalidShaWithRepoDiscoveryError = "Invalid commit SHA provided and failed to discover local azure-rest-api-specs repo";
    private const string RepoNameNotProvidedError = "repository name is not provided";
    private const string DirectoryNotExistError = "does not provide or exist";
    private const string TspClientInitFailedError = "tsp-client init failed";
    
    // Common success messages
    private const string SdkGenerationSuccessMessage = "SDK generation completed successfully";
    private const string SdkRegenerationSuccessMessage = "SDK re-generation completed successfully";
    private const string ProcessSuccessOutput = "Success";
    private const string ProcessErrorOutput = "Error occurred";
    
    #endregion

    private SdkGenerationTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<INpxHelper> _mockNpxHelper;
    private Mock<IOutputHelper> _mockOutputHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private TestLogger<SdkGenerationTool> _logger;
    private string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _mockOutputHelper = new Mock<IOutputHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _logger = new TestLogger<SdkGenerationTool>();

        // Create temp directory for tests
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SdkGenerationToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Create the tool instance
        _tool = new SdkGenerationTool(
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

    #region GenerateSdkAsync Tests

    [Test]
    public async Task GenerateSdkAsync_BothPathsEmpty_ReturnsFailure()
    {
        // Act
        var result = await _tool.GenerateSdkAsync("/some/path", null, null, null, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(BothPathsEmptyError));
    }

    [Test]
    public async Task GenerateSdkAsync_WithTspLocationPath_CallsRunTspUpdate()
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
        var result = await _tool.GenerateSdkAsync("/some/path", null, null, null, tspLocationPath, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain(SdkRegenerationSuccessMessage));
        _mockNpxHelper.Verify(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdkAsync_WithTspLocationPath_FileNotExists_ReturnsFailure()
    {
        // Arrange
        var tspLocationPath = Path.Combine(_tempDirectory, "nonexistent-" + TspLocationFileName);

        // Act
        var result = await _tool.GenerateSdkAsync("/some/path", null, null, null, tspLocationPath, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FileNotExistError));
    }

    [Test]
    public async Task GenerateSdkAsync_WithTspConfigPath_RemoteUrl_CallsRunTspInit()
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
        var result = await _tool.GenerateSdkAsync(_tempDirectory, RemoteTspConfigUrl, InvalidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain(SdkGenerationSuccessMessage));
        _mockNpxHelper.Verify(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdkAsync_WithLocalTspConfigPath_ValidInputs_CallsRunTspInit()
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
        var result = await _tool.GenerateSdkAsync(_tempDirectory, tspConfigPath, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain(SdkGenerationSuccessMessage));
        _mockNpxHelper.Verify(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdkAsync_WithLocalTspConfigPath_EmptySpecCommitSha_ReturnsFailure()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);
        
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act
        var result = await _tool.GenerateSdkAsync(_tempDirectory, tspConfigPath, null, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(InvalidShaWithRepoDiscoveryError));
    }

    [Test]
    public async Task GenerateSdkAsync_WithLocalTspConfigPath_InvalidSpecCommitSha_ReturnsError()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);
        
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act - Use an invalid SHA
        var result = await _tool.GenerateSdkAsync(_tempDirectory, tspConfigPath, InvalidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(InvalidShaWithRepoDiscoveryError));
    }

    [Test]
    public async Task GenerateSdkAsync_WithLocalTspConfigPath_FileNotExists_ReturnsError()
    {
        // Arrange
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        
        // Act - Use a non-existent file path
        var result = await _tool.GenerateSdkAsync(_tempDirectory, "/nonexistent/" + TspConfigFileName, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FileNotExistError));
    }

    [Test]
    public async Task GenerateSdkAsync_WithLocalTspConfigPath_EmptySpecRepoFullName_ReturnsError()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);
        
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act - Use empty repo name
        var result = await _tool.GenerateSdkAsync(_tempDirectory, tspConfigPath, ValidCommitSha, null, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(RepoNameNotProvidedError));
    }

    [Test]
    public async Task GenerateSdkAsync_TspClientInitFails_ReturnsFailure()
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
        var result = await _tool.GenerateSdkAsync(_tempDirectory, RemoteTspConfigUrl, InvalidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(TspClientInitFailedError));
    }

    [Test]
    public async Task GenerateSdkAsync_WithInvalidRemoteUrl_ReturnsFailure()
    {
        // Arrange
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act - Use invalid remote URL that doesn't match GitHub blob pattern
        var result = await _tool.GenerateSdkAsync(_tempDirectory, InvalidRemoteTspConfigUrl, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Invalid remote GitHub URL with commit"));
    }

    [Test]
    public async Task GenerateSdkAsync_ExceptionThrown_ReturnsFailureWithExceptionMessage()
    {
        // Arrange
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act - Now remote URLs work properly
        var result = await _tool.GenerateSdkAsync(_tempDirectory, RemoteTspConfigUrl, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Test exception"));
    }

    [Test]
    public async Task GenerateSdkAsync_WithInvalidSdkRepoPath_ReturnsError()
    {
        // Act - Use a non-existent directory path
        var result = await _tool.GenerateSdkAsync("/this/path/does/not/exist", RemoteTspConfigUrl, ValidCommitSha, DefaultSpecRepo, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(DirectoryNotExistError));
    }

    #endregion

    #region Command Tests

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectStructure()
    {
        // Act
        var command = _tool.GetCommand();

        // Assert
        Assert.That(command.Name, Is.EqualTo("generate"));
        Assert.That(command.Description, Does.Contain("Generates SDK code"));
    }

    #endregion
}
