// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Models;
using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Commands;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class ConfigBasedToolTests
{
    #region Test Constants

    private const string EngDirectoryName = "eng";
    private const string TestScriptPath = "test_script.ps1";

    #endregion

    private TestConfigBasedTool _tool;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private TestLogger<TestConfigBasedTool> _logger;
    private string _tempDirectory;
    private string _packagePath;
    private string _sdkRepoRoot;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _logger = new TestLogger<TestConfigBasedTool>();

        // Create temp directory for tests
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ConfigBasedToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Setup common paths
        _packagePath = Path.Combine(_tempDirectory, "sdk", "storage", "Azure.Storage.Blobs");
        _sdkRepoRoot = _tempDirectory;
        Directory.CreateDirectory(_packagePath);

        // Create the tool instance
        _tool = new TestConfigBasedTool(
            _mockSpecGenSdkConfigHelper.Object,
            _logger,
            _mockProcessHelper.Object
        );
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region Validation Tests

    [Test]
    public void ValidatePackagePath_ValidPath_ReturnsNull()
    {
        // Act
        var result = _tool.TestValidatePackagePath(_packagePath);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ValidatePackagePath_EmptyPath_ReturnsFailure()
    {
        // Act
        var result = _tool.TestValidatePackagePath(string.Empty);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("Package path is required"));
    }

    [Test]
    public void ValidatePackagePath_NonExistentPath_ReturnsFailure()
    {
        // Arrange
        var invalidPath = "/path/that/does/not/exist";

        // Act
        var result = _tool.TestValidatePackagePath(invalidPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("Path does not exist"));
    }

    [Test]
    public void ValidateFilePath_ValidFile_ReturnsNull()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        var result = _tool.TestValidateFilePath(testFile);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ValidateFilePath_EmptyPath_ReturnsFailure()
    {
        // Act
        var result = _tool.TestValidateFilePath(string.Empty);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("file path is required"));
    }

    [Test]
    public void ValidateFilePath_NonExistentFile_ReturnsFailure()
    {
        // Arrange
        var invalidPath = "/path/that/does/not/exist.txt";

        // Act
        var result = _tool.TestValidateFilePath(invalidPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("file does not exist"));
    }

    #endregion

    #region Response Creation Tests

    [Test]
    public void CreateSuccessResponse_WithMessage_ReturnsSuccess()
    {
        // Act
        var result = _tool.TestCreateSuccessResponse("Test message");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Is.EqualTo("Test message"));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps, Is.Empty);
    }

    [Test]
    public void CreateSuccessResponse_WithMessageAndNextSteps_ReturnsSuccess()
    {
        // Arrange
        var nextSteps = new[] { "Step 1", "Step 2" };

        // Act
        var result = _tool.TestCreateSuccessResponse("Test message", nextSteps);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Is.EqualTo("Test message"));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps, Contains.Item("Step 1"));
        Assert.That(result.NextSteps, Contains.Item("Step 2"));
    }

    [Test]
    public void CreateSuccessResponse_WithNullNextSteps_ReturnsEmptyList()
    {
        // Act
        var result = _tool.TestCreateSuccessResponse("Test message", null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Is.EqualTo("Test message"));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps, Is.Empty);
    }

    [Test]
    public void CreateFailureResponse_WithMessage_ReturnsFailure()
    {
        // Act
        var result = _tool.TestCreateFailureResponse("Test error");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Contains.Item("Test error"));
    }

    #endregion

    #region Process Execution Tests

    [Test]
    public async Task ExecuteProcessAsync_SuccessfulExecution_ReturnsSuccess()
    {
        // Arrange
        var processOptions = new ProcessOptions("echo", new[] { "test" });
        var mockResult = new ProcessResult { ExitCode = 0 };
        mockResult.AppendStdout("test output");
        _mockProcessHelper.Setup(x => x.Run(processOptions, It.IsAny<CancellationToken>())).ReturnsAsync(mockResult);

        // Act
        var result = await _tool.TestExecuteProcessAsync(processOptions, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain("Process completed successfully"));
        Assert.That(result.Message, Does.Contain("test output"));
    }

    [Test]
    public async Task ExecuteProcessAsync_FailedExecution_ReturnsFailure()
    {
        // Arrange
        var processOptions = new ProcessOptions("echo", new[] { "test" });
        var mockResult = new ProcessResult { ExitCode = 1 };
        mockResult.AppendStderr("error output");
        _mockProcessHelper.Setup(x => x.Run(processOptions, It.IsAny<CancellationToken>())).ReturnsAsync(mockResult);

        // Act
        var result = await _tool.TestExecuteProcessAsync(processOptions, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("Process failed with exit code 1"));
        Assert.That(result.ResponseErrors, Has.Some.Contains("error output"));
    }

    [Test]
    public async Task ExecuteProcessAsync_WithCustomMessage_ReturnsCustomMessage()
    {
        // Arrange
        var processOptions = new ProcessOptions("echo", new[] { "test" });
        var mockResult = new ProcessResult { ExitCode = 0 };
        mockResult.AppendStdout("test output");
        _mockProcessHelper.Setup(x => x.Run(processOptions, It.IsAny<CancellationToken>())).ReturnsAsync(mockResult);
        var nextSteps = new[] { "Next step 1" };

        // Act
        var result = await _tool.TestExecuteProcessAsync(processOptions, CancellationToken.None, "Custom success message", nextSteps);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain("Custom success message"));
        Assert.That(result.NextSteps, Contains.Item("Next step 1"));
    }

    [Test]
    public async Task ExecuteProcessAsync_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var processOptions = new ProcessOptions("echo", new[] { "test" });
        _mockProcessHelper.Setup(x => x.Run(processOptions, It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _tool.TestExecuteProcessAsync(processOptions, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("An error occurred: Test exception"));
    }

    #endregion

    #region CreateProcessOptions Tests

    [Test]
    public async Task CreateProcessOptions_CommandConfigType_ReturnsCommandProcessOptions()
    {
        // Arrange
        var parameters = new Dictionary<string, string> { { "packagePath", _packagePath } };
        var command = "dotnet build {packagePath}";
        
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(_sdkRepoRoot, ConfigType.Build))
            .ReturnsAsync((ConfigContentType.Command, command));
        _mockSpecGenSdkConfigHelper.Setup(x => x.SubstituteCommandVariables(command, parameters))
            .Returns($"dotnet build {_packagePath}");
        _mockSpecGenSdkConfigHelper.Setup(x => x.ParseCommand($"dotnet build {_packagePath}"))
            .Returns(new[] { "dotnet", "build", _packagePath });

        // Act
        var result = await _tool.TestCreateProcessOptions(ConfigType.Build, _sdkRepoRoot, _packagePath, parameters, 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShortName, Is.EqualTo("dotnet"));
        Assert.That(result.Args, Contains.Item("build"));
        Assert.That(result.Args, Contains.Item(_packagePath));
    }

    [Test]
    public async Task CreateProcessOptions_ScriptPathConfigType_ReturnsScriptProcessOptions()
    {
        // Arrange
        var parameters = new Dictionary<string, string> { { "SdkRepoPath", _sdkRepoRoot } };
        var scriptPath = "eng/scripts/build.ps1";
        var fullScriptPath = Path.Combine(_sdkRepoRoot, scriptPath);
        
        // Create the script file
        Directory.CreateDirectory(Path.GetDirectoryName(fullScriptPath)!);
        File.WriteAllText(fullScriptPath, "# Test script");

        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(_sdkRepoRoot, ConfigType.Build))
            .ReturnsAsync((ConfigContentType.ScriptPath, scriptPath));

        // Act
        var result = await _tool.TestCreateProcessOptions(ConfigType.Build, _sdkRepoRoot, _packagePath, parameters, 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ShortName, Is.EqualTo("build.ps1"));
        Assert.That(result.Args, Contains.Item("-File"));
        Assert.That(result.Args, Contains.Item(fullScriptPath));
    }

    #endregion
}

/// <summary>
/// Test implementation of ConfigBasedTool to expose protected methods for testing
/// </summary>
public class TestConfigBasedTool : ConfigBasedTool
{
    public TestConfigBasedTool(
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        ILogger<TestConfigBasedTool> logger,
        IProcessHelper? processHelper = null)
        : base(specGenSdkConfigHelper, logger, processHelper)
    {
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [];

    protected override Command GetCommand() => new("test", "Test command");

    public override Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        return Task.FromResult<CommandResponse>(new DefaultCommandResponse { Result = "test" });
    }

    // Expose protected methods for testing
    public DefaultCommandResponse? TestValidatePackagePath(string packagePath)
        => ValidatePackagePath<DefaultCommandResponse>(packagePath);

    public DefaultCommandResponse? TestValidateFilePath(string filePath)
        => ValidateFilePath<DefaultCommandResponse>(filePath);

    public DefaultCommandResponse TestCreateSuccessResponse(string message, string[]? nextSteps = null)
        => CreateSuccessResponse(message, nextSteps);

    public DefaultCommandResponse TestCreateFailureResponse(string message)
        => CreateFailureResponse<DefaultCommandResponse>(message);

    public async Task<DefaultCommandResponse> TestExecuteProcessAsync(
        ProcessOptions options,
        CancellationToken ct,
        string successMessage = "Process completed successfully.",
        string[]? nextSteps = null)
        => await ExecuteProcessAsync(options, ct, successMessage, nextSteps);

    public async Task<ProcessOptions> TestCreateProcessOptions(
        ConfigType configType,
        string sdkRepoRoot,
        string packagePath,
        Dictionary<string, string> parameters,
        int timeoutMinutes)
        => await CreateProcessOptions(configType, sdkRepoRoot, packagePath, parameters, timeoutMinutes);
}
