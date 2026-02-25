using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class PythonLanguageSpecificChecksTests
{
    private Mock<IProcessHelper> _processHelperMock = null!;
    private Mock<INpxHelper> _npxHelperMock = null!;
    private Mock<IPythonHelper> _pythonHelperMock = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<ICommonValidationHelpers> _commonValidationHelpersMock = null!;
    private PythonLanguageService _languageService = null!;

    [SetUp]
    public void SetUp()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _npxHelperMock = new Mock<INpxHelper>();
        _pythonHelperMock = new Mock<IPythonHelper>();
        _gitHelperMock = new Mock<IGitHelper>();
        _gitHelperMock.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-python");
        _commonValidationHelpersMock = new Mock<ICommonValidationHelpers>();

        _languageService = new PythonLanguageService(
            _processHelperMock.Object,
            _pythonHelperMock.Object,
            _npxHelperMock.Object,
            _gitHelperMock.Object,
            NullLogger<PythonLanguageService>.Instance,
            _commonValidationHelpersMock.Object,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    #region HasCustomizations Tests

    [Test]
    public void HasCustomizations_ReturnsTrue_WhenPatchFileHasNonEmptyAllExport()
    {
        using var tempDir = TempDirectory.Create("python-customization-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        File.WriteAllText(Path.Combine(azureDir, "_patch.py"), 
            "__all__ = [\"CustomClient\"]\n\nclass CustomClient:\n    pass");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasCustomizations_ReturnsTrue_WhenMultilineAllExport()
    {
        using var tempDir = TempDirectory.Create("python-multiline-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        File.WriteAllText(Path.Combine(azureDir, "_patch.py"), 
            "__all__ = [\n    \"CustomClient\",\n]");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasCustomizations_ReturnsFalse_WhenPatchFileHasEmptyAllExport()
    {
        using var tempDir = TempDirectory.Create("python-empty-patch-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        File.WriteAllText(Path.Combine(azureDir, "_patch.py"), "__all__: List[str] = []");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasCustomizations_ReturnsFalse_WhenNoPatchFilesExist()
    {
        using var tempDir = TempDirectory.Create("python-no-patch-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);
        
        File.WriteAllText(Path.Combine(azureDir, "client.py"), "# Client code");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.False);
    }

    #endregion

    #region AnalyzeDependencies Tests

    [Test]
    public async Task AnalyzeDependencies_ReturnsSuccess_WhenNoDependencyIssuesFound()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-dep-success-test");
        var packagePath = tempDir.DirectoryPath;

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            OutputDetails = [(StdioLevel.StandardOutput, "All dependencies are compatible")]
        };
        _pythonHelperMock.Setup(p => p.Run(
            It.IsAny<PythonOptions>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(processResult);

        // Act
        var result = await _languageService.AnalyzeDependencies(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.CheckStatusDetails, Does.Contain("Dependency analysis completed successfully - all minimum dependencies are compatible"));
            Assert.That(result.ResponseError, Is.Null.Or.Empty);
        });

        // Verify correct Python command was called
        _pythonHelperMock.Verify(x => x.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AnalyzeDependencies_ReturnsFailure_WhenDependencyIssuesFound()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-dep-failure-test");
        var packagePath = tempDir.DirectoryPath;

        var processResult = new ProcessResult
        {
            ExitCode = 1,
            OutputDetails = [(StdioLevel.StandardOutput, "Dependency conflicts detected"), (StdioLevel.StandardOutput, "Conflict in package version requirements")]
        };
        
        _pythonHelperMock.Setup(p => p.Run(
            It.IsAny<PythonOptions>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(processResult);

        // Act
        var result = await _languageService.AnalyzeDependencies(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.CheckStatusDetails, Does.Contain("Dependency conflicts detected"));
            Assert.That(result.ResponseError, Does.Contain("Dependency analysis found issues with minimum dependency versions"));
        });

        // Verify Python helper was called
        _pythonHelperMock.Verify(x => x.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AnalyzeDependencies_ReturnsError_WhenExceptionThrown()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-dep-exception-test");
        var packagePath = tempDir.DirectoryPath;

        _pythonHelperMock.Setup(p => p.Run(
            It.IsAny<PythonOptions>(),
            It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("Python execution failed"));

        // Act
        var result = await _languageService.AnalyzeDependencies(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Error running dependency analysis: Python execution failed"));
            Assert.That(result.CheckStatusDetails, Is.Empty);
        });

        // Verify Python command was attempted
        _pythonHelperMock.Verify(x => x.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AnalyzeDependencies_UsesCorrectTimeout_WhenCalled()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-dep-timeout-test");
        var packagePath = tempDir.DirectoryPath;

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            OutputDetails = [(StdioLevel.StandardOutput, "Success")]
        };
        _pythonHelperMock.Setup(p => p.Run(
            It.IsAny<PythonOptions>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(processResult);

        // Act
        await _languageService.AnalyzeDependencies(packagePath, false, CancellationToken.None);

        // Assert - Verify timeout is exactly 5 minutes
        _pythonHelperMock.Verify(x => x.Run(It.Is<PythonOptions>(p => 
            p.Timeout == TimeSpan.FromMinutes(5)), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
