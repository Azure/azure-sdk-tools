// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class SdkGenerationToolTests
{
    #region Test Constants

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
    private TestLogger<SdkGenerationTool> _logger;
    private string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _logger = new TestLogger<SdkGenerationTool>();

        // Create temp directory for tests
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SdkGenerationToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Create the tool instance
        _tool = new SdkGenerationTool(
            _mockGitHelper.Object,
            _logger,
            _mockNpxHelper.Object
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
        var result = await _tool.GenerateSdkAsync("/some/path", null, null, null);

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
        var result = await _tool.GenerateSdkAsync("/some/path", null, tspLocationPath, null);

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
        var result = await _tool.GenerateSdkAsync("/some/path", null, tspLocationPath, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("does not exist"));
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
        var result = await _tool.GenerateSdkAsync(_tempDirectory, RemoteTspConfigUrl, null, null);

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
        _mockGitHelper.Setup(x => x.GetRepoFullNameAsync(tspConfigPath, true)).ReturnsAsync(DefaultSpecRepo);

        var expectedResult = new ProcessResult { ExitCode = 0 };
        expectedResult.AppendStdout(ProcessSuccessOutput);
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.GenerateSdkAsync(_tempDirectory, tspConfigPath, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain(SdkGenerationSuccessMessage));
        _mockNpxHelper.Verify(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdkAsync_WithLocalTspConfigPath_FileNotExists_ReturnsError()
    {
        // Arrange
        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        // Act - Use a non-existent file path
        var result = await _tool.GenerateSdkAsync(_tempDirectory, "/nonexistent/" + TspConfigFileName, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(FileNotExistError));
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
        var result = await _tool.GenerateSdkAsync(_tempDirectory, RemoteTspConfigUrl, null, null);

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
        var result = await _tool.GenerateSdkAsync(_tempDirectory, InvalidRemoteTspConfigUrl, null, null);

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
        var result = await _tool.GenerateSdkAsync(_tempDirectory, RemoteTspConfigUrl, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Test exception"));
    }

    [Test]
    public async Task GenerateSdkAsync_WithInvalidSdkRepoPath_ReturnsError()
    {
        // Act - Use a non-existent directory path
        var result = await _tool.GenerateSdkAsync("/this/path/does/not/exist", RemoteTspConfigUrl, null, null);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(DirectoryNotExistError));
    }

    [Test]
    public async Task GenerateSdkAsync_WithLocalTspConfigAndEmitterOptions_PassesOptionsToNpx()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);
        var emitterOptions = "package-version=1.0.0-beta.1";

        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        _mockGitHelper.Setup(x => x.GetRepoFullNameAsync(tspConfigPath, false)).ReturnsAsync(DefaultSpecRepo);

        var expectedResult = new ProcessResult { ExitCode = 0 };
        expectedResult.AppendStdout(ProcessSuccessOutput);
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.GenerateSdkAsync(_tempDirectory, tspConfigPath, null, emitterOptions);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));

        // Verify the NPX arguments match expected pattern
        _mockNpxHelper.Verify(x => x.Run(
            It.Is<NpxOptions>(opts =>
                opts.Args.Contains("tsp-client") &&
                opts.Args.Contains("init") &&
                opts.Args.Contains("--update-if-exists") &&
                opts.Args.Contains("--tsp-config") &&
                opts.Args.Contains(tspConfigPath) &&
                opts.Args.Contains("--repo") &&
                opts.Args.Contains(DefaultSpecRepo) &&
                opts.Args.Contains("--emitter-options") &&
                opts.Args.Contains(emitterOptions)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdkAsync_WithRemoteTspConfigAndEmitterOptions_PassesOptionsToNpx()
    {
        // Arrange
        var emitterOptions = "package-version=1.0.0-beta.1";

        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);

        var expectedResult = new ProcessResult { ExitCode = 0 };
        expectedResult.AppendStdout(ProcessSuccessOutput);
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.GenerateSdkAsync(_tempDirectory, RemoteTspConfigUrl, null, emitterOptions);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        _mockNpxHelper.Verify(x => x.Run(
            It.Is<NpxOptions>(opts =>
                opts.Args.Contains("--emitter-options") &&
                opts.Args.Contains(emitterOptions) &&
                !opts.Args.Contains("--repo")), // Remote URLs should not include --repo
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GenerateSdkAsync_WithLocalTspConfigNoEmitterOptions_DoesNotPassEmitterOptionsToNpx()
    {
        // Arrange
        var tspConfigPath = Path.Combine(_tempDirectory, TspConfigFileName);
        File.WriteAllText(tspConfigPath, TestTspConfigContent);

        // Mock GitHelper to return valid repo root
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_tempDirectory)).Returns(_tempDirectory);
        _mockGitHelper.Setup(x => x.GetRepoFullNameAsync(tspConfigPath, false)).ReturnsAsync(DefaultSpecRepo);

        var expectedResult = new ProcessResult { ExitCode = 0 };
        expectedResult.AppendStdout(ProcessSuccessOutput);
        _mockNpxHelper
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.GenerateSdkAsync(_tempDirectory, tspConfigPath, null, null);

        // Assert
        Assert.That(result.Result, Is.EqualTo("succeeded"));

        // Verify the NPX arguments match expected pattern but don't include emitter options
        _mockNpxHelper.Verify(x => x.Run(
            It.Is<NpxOptions>(opts =>
                opts.Args.Contains("tsp-client") &&
                opts.Args.Contains("init") &&
                opts.Args.Contains("--update-if-exists") &&
                opts.Args.Contains("--tsp-config") &&
                opts.Args.Contains(tspConfigPath) &&
                opts.Args.Contains("--repo") &&
                opts.Args.Contains(DefaultSpecRepo) &&
                !opts.Args.Contains("--emitter-options")), // Should not include emitter options when null
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Command Tests

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectStructure()
    {
        // Act
        var command = _tool.GetCommandInstances().First();

        // Assert
        Assert.That(command.Name, Is.EqualTo("generate"));
        Assert.That(command.Description, Does.Contain("Generates SDK code"));
    }

    #endregion
}
