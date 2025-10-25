// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class ChangelogUpdateToolTests
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

    private ChangelogUpdateTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
    private TestLogger<ChangelogUpdateTool> _logger;
    private string _tempDirectory;
    private string _packagePath;
    private string _sdkRepoRoot;

    [SetUp]
    public void Setup()
    {
        // Create mocks
        _mockGitHelper = new Mock<IGitHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        _logger = new TestLogger<ChangelogUpdateTool>();

        // Create temp directory for tests
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ChangelogUpdateToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Setup common paths
        _packagePath = Path.Combine(_tempDirectory, "sdk", "storage", "Azure.Storage.Blobs");
        _sdkRepoRoot = _tempDirectory;
        Directory.CreateDirectory(_packagePath);

        // Create the tool instance
        _tool = new ChangelogUpdateTool(
            _mockSpecGenSdkConfigHelper.Object,
            _logger,
            _mockProcessHelper.Object,
            _mockGitHelper.Object
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

    #region UpdateChangelogAsync Tests

    [Test]
    public async Task UpdateChangelogAsync_ValidPackagePath_ManagementPlane_ReturnsSuccess()
    {
        // Arrange
        var mgmtPackagePath = Path.Combine(_tempDirectory, "sdk", "resourcemanager", "Azure.ResourceManager.Storage");
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(mgmtPackagePath)).Returns(_sdkRepoRoot);
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(_sdkRepoRoot, ConfigType.UpdateChangelog))
            .ReturnsAsync((ConfigContentType.Command, "python eng/scripts/update_changelog.py {SdkRepoPath} {PackagePath}"));
        
        // Setup command substitution and parsing
        _mockSpecGenSdkConfigHelper.Setup(x => x.SubstituteCommandVariables(
            "python eng/scripts/update_changelog.py {SdkRepoPath} {PackagePath}",
            It.IsAny<Dictionary<string, string>>()))
            .Returns($"python eng/scripts/update_changelog.py {_sdkRepoRoot} {mgmtPackagePath}");
        
        _mockSpecGenSdkConfigHelper.Setup(x => x.ParseCommand(It.IsAny<string>()))
            .Returns<string>(cmd => cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var mockProcessResult = new ProcessResult { ExitCode = 0 };
        mockProcessResult.AppendStdout("Changelog updated successfully");
        _mockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockProcessResult);

        // Create a management plane package path
        Directory.CreateDirectory(mgmtPackagePath);

        // Act
        var result = await _tool.UpdateChangelogAsync(mgmtPackagePath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("succeeded"));
        Assert.That(result.Message, Does.Contain("Changelog content is updated"));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps, Contains.Item("Update the version if it's a release."));
    }

    [Test]
    public async Task UpdateChangelogAsync_ValidPackagePath_DataPlane_ReturnsNoop()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_packagePath)).Returns(_sdkRepoRoot);

        // Act
        var result = await _tool.UpdateChangelogAsync(_packagePath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("noop"));
        Assert.That(result.Message, Is.EqualTo("Data-plane changelog untouched; manual edits required."));
        Assert.That(result.NextSteps, Is.Not.Null);
        Assert.That(result.NextSteps, Contains.Item("Update the version if it's a release."));
    }

    [Test]
    public async Task UpdateChangelogAsync_InvalidPackagePath_ReturnsFailure()
    {
        // Arrange
        var invalidPath = "/path/that/does/not/exist";

        // Act
        var result = await _tool.UpdateChangelogAsync(invalidPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains(invalidPath));
    }

    [Test]
    public async Task UpdateChangelogAsync_EmptyPackagePath_ReturnsFailure()
    {
        // Act
        var result = await _tool.UpdateChangelogAsync(string.Empty);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("Package path is required"));
    }

    [Test]
    public async Task UpdateChangelogAsync_FailedToDiscoverRepo_ReturnsFailure()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_packagePath)).Returns(string.Empty);

        // Act
        var result = await _tool.UpdateChangelogAsync(_packagePath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("Failed to discover local sdk repo"));
    }

    [Test]
    public async Task UpdateChangelogAsync_ProcessExecutionFails_ReturnsFailure()
    {
        // Arrange
        var mgmtPackagePath = Path.Combine(_tempDirectory, "sdk", "resourcemanager", "Azure.ResourceManager.Storage");
        Directory.CreateDirectory(mgmtPackagePath);

        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(mgmtPackagePath)).Returns(_sdkRepoRoot);
        _mockSpecGenSdkConfigHelper.Setup(x => x.GetConfigurationAsync(_sdkRepoRoot, ConfigType.UpdateChangelog))
            .ReturnsAsync((ConfigContentType.Command, "python eng/scripts/update_changelog.ps1 {SdkRepoPath} {PackagePath}"));

        // Setup command substitution and parsing
        _mockSpecGenSdkConfigHelper.Setup(x => x.SubstituteCommandVariables(
            "python eng/scripts/update_changelog.ps1 {SdkRepoPath} {PackagePath}",
            It.IsAny<Dictionary<string, string>>()))
            .Returns($"python eng/scripts/update_changelog.ps1 {_sdkRepoRoot} {mgmtPackagePath}");
        
        _mockSpecGenSdkConfigHelper.Setup(x => x.ParseCommand(It.IsAny<string>()))
            .Returns<string>(cmd => cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var mockProcessResult = new ProcessResult { ExitCode = 1 };
        mockProcessResult.AppendStderr("Script execution failed");
        _mockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockProcessResult);

        // Act
        var result = await _tool.UpdateChangelogAsync(mgmtPackagePath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("Process failed with exit code 1"));
    }

    [Test]
    public async Task UpdateChangelogAsync_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockGitHelper.Setup(x => x.DiscoverRepoRoot(_packagePath)).Throws(new Exception("Test exception"));

        // Act
        var result = await _tool.UpdateChangelogAsync(_packagePath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.EqualTo("failed"));
        Assert.That(result.ResponseErrors, Is.Not.Null);
        Assert.That(result.ResponseErrors, Has.Some.Contains("An error occurred: Test exception"));
    }

    #endregion

    #region DeterminePackageType Tests

    [Test]
    public void DeterminePackageType_ResourceManagerPath_ReturnsMgmt()
    {
        // Arrange
        var mgmtPackagePath = Path.Combine(_tempDirectory, "sdk", "resourcemanager", "Azure.ResourceManager.Storage");
        Directory.CreateDirectory(mgmtPackagePath);

        // Act
        var result = _tool.GetType()
            .GetMethod("DeterminePackageType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_tool, new object[] { mgmtPackagePath, _sdkRepoRoot }) as string;

        // Assert
        Assert.That(result, Is.EqualTo("mgmt"));
    }

    [Test]
    public void DeterminePackageType_ArmPath_ReturnsMgmt()
    {
        // Arrange
        var armPackagePath = Path.Combine(_tempDirectory, "sdk", "arm-storage", "Azure.ResourceManager.Storage");
        Directory.CreateDirectory(armPackagePath);

        // Act
        var result = _tool.GetType()
            .GetMethod("DeterminePackageType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_tool, new object[] { armPackagePath, _sdkRepoRoot }) as string;

        // Assert
        Assert.That(result, Is.EqualTo("mgmt"));
    }

    [Test]
    public void DeterminePackageType_MgmtPath_ReturnsMgmt()
    {
        // Arrange
        var mgmtPackagePath = Path.Combine(_tempDirectory, "sdk", "mgmt-storage", "Azure.ResourceManager.Storage");
        Directory.CreateDirectory(mgmtPackagePath);

        // Act
        var result = _tool.GetType()
            .GetMethod("DeterminePackageType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_tool, new object[] { mgmtPackagePath, _sdkRepoRoot }) as string;

        // Assert
        Assert.That(result, Is.EqualTo("mgmt"));
    }

    [Test]
    public void DeterminePackageType_DataPlanePath_ReturnsDataPlane()
    {
        // Arrange
        var dataPlanePackagePath = Path.Combine(_tempDirectory, "sdk", "storage", "Azure.Storage.Blobs");
        Directory.CreateDirectory(dataPlanePackagePath);

        // Act
        var result = _tool.GetType()
            .GetMethod("DeterminePackageType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_tool, new object[] { dataPlanePackagePath, _sdkRepoRoot }) as string;

        // Assert
        Assert.That(result, Is.EqualTo("data-plane"));
    }

    #endregion
}
