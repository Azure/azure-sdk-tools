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
            Mock.Of<IPackageInfoHelper>(),
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    #region HasCustomizations Tests

    [Test]
    public void HasCustomizations_ReturnsPath_WhenPatchFileHasNonEmptyAllExport()
    {
        using var tempDir = TempDirectory.Create("python-customization-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);

        File.WriteAllText(Path.Combine(azureDir, "_patch.py"),
            "__all__ = [\"CustomClient\"]\n\nclass CustomClient:\n    pass");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(tempDir.DirectoryPath));
    }

    [Test]
    public void HasCustomizations_ReturnsPath_WhenMultilineAllExport()
    {
        using var tempDir = TempDirectory.Create("python-multiline-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);

        File.WriteAllText(Path.Combine(azureDir, "_patch.py"),
            "__all__ = [\n    \"CustomClient\",\n]");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(tempDir.DirectoryPath));
    }

    [Test]
    public void HasCustomizations_ReturnsNull_WhenPatchFileHasEmptyAllExport()
    {
        using var tempDir = TempDirectory.Create("python-empty-patch-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);

        File.WriteAllText(Path.Combine(azureDir, "_patch.py"), "__all__: List[str] = []");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void HasCustomizations_ReturnsNull_WhenNoPatchFilesExist()
    {
        using var tempDir = TempDirectory.Create("python-no-patch-test");
        var azureDir = Path.Combine(tempDir.DirectoryPath, "azure", "test");
        Directory.CreateDirectory(azureDir);

        File.WriteAllText(Path.Combine(azureDir, "client.py"), "# Client code");

        var result = _languageService.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Null);
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
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
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
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("azsdk_verify_setup"));
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

    #region LintCode Tests

    [Test]
    public async Task LintCode_ReturnsSuccess_WhenAllLintingToolsPass()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-lint-success-test");
        var packagePath = tempDir.DirectoryPath;

        var successResult = new ProcessResult
        {
            ExitCode = 0,
            OutputDetails = [(StdioLevel.StandardOutput, "No issues found")]
        };
        _pythonHelperMock.Setup(p => p.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        // Act
        var result = await _languageService.LintCode(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.CheckStatusDetails, Does.Contain("All linting tools completed successfully"));
            Assert.That(result.NextSteps, Is.Null.Or.Empty);
        });
    }

    [Test]
    public async Task LintCode_ReturnsNextStepsWithPylintGuidance_WhenPylintFails()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-lint-pylint-fail-test");
        var packagePath = tempDir.DirectoryPath;

        _pythonHelperMock
            .Setup(p => p.Run(It.Is<PythonOptions>(o => o.Args.Contains("pylint")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "pylint errors")] });
        _pythonHelperMock
            .Setup(p => p.Run(It.Is<PythonOptions>(o => o.Args.Contains("mypy")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Success")] });

        // Act
        var result = await _languageService.LintCode(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("pylint"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("pylint"));
        });
    }

    [Test]
    public async Task LintCode_ReturnsNextStepsWithMypyGuidance_WhenMypyFails()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-lint-mypy-fail-test");
        var packagePath = tempDir.DirectoryPath;

        _pythonHelperMock
            .Setup(p => p.Run(It.Is<PythonOptions>(o => o.Args.Contains("pylint")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Success")] });
        _pythonHelperMock
            .Setup(p => p.Run(It.Is<PythonOptions>(o => o.Args.Contains("mypy")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "mypy errors")] });

        // Act
        var result = await _languageService.LintCode(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("mypy"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("mypy"));
        });
    }

    [Test]
    public async Task LintCode_ReturnsNextStepsWithInstallGuidance_WhenExceptionThrown()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-lint-exception-test");
        var packagePath = tempDir.DirectoryPath;

        _pythonHelperMock.Setup(p => p.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("azpysdk not found"));

        // Act
        var result = await _languageService.LintCode(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Error running code linting"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("azsdk_verify_setup"));
        });
    }

    #endregion

    #region FormatCode Tests

    [Test]
    public async Task FormatCode_ReturnsSuccess_WhenFormattingSucceeds()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-format-success-test");
        var packagePath = tempDir.DirectoryPath;

        _pythonHelperMock.Setup(p => p.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "All done!")] });

        // Act
        var result = await _languageService.FormatCode(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.CheckStatusDetails, Does.Contain("formatting applied"));
            Assert.That(result.NextSteps, Is.Null.Or.Empty);
        });
    }

    [Test]
    public async Task FormatCode_ReturnsNextSteps_WhenFormattingFails()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-format-fail-test");
        var packagePath = tempDir.DirectoryPath;

        _pythonHelperMock.Setup(p => p.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "error: cannot format file.py")] });

        // Act
        var result = await _languageService.FormatCode(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("failed to apply"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public async Task FormatCode_ReturnsNextStepsWithInstallGuidance_WhenExceptionThrown()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-format-exception-test");
        var packagePath = tempDir.DirectoryPath;

        _pythonHelperMock.Setup(p => p.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("black not found"));

        // Act
        var result = await _languageService.FormatCode(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Error running code formatting"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("azsdk_verify_setup"));
        });
    }

    #endregion

    #region UpdateSnippets Tests

    [Test]
    public async Task UpdateSnippets_ReturnsNextStepsWithRepoGuidance_WhenScriptNotFound()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-snippets-no-script-test");
        var packagePath = tempDir.DirectoryPath;

        // Git helper will return the temp dir as repo root - so script won't exist there
        _gitHelperMock.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempDir.DirectoryPath);

        // Act
        var result = await _languageService.UpdateSnippets(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("not found"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("azure-sdk-for-python"));
        });
    }

    [Test]
    public async Task UpdateSnippets_ReturnsNextStepsWithPythonInstallGuidance_WhenPythonNotAvailable()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-snippets-no-python-test");
        var packagePath = tempDir.DirectoryPath;

        // Create the script path so we get past that check
        _gitHelperMock.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempDir.DirectoryPath);

        var scriptDir = Path.Combine(tempDir.DirectoryPath, "eng", "tools", "azure-sdk-tools", "ci_tools", "snippet_update");
        Directory.CreateDirectory(scriptDir);
        File.WriteAllText(Path.Combine(scriptDir, "python_snippet_updater.py"), "# placeholder");

        // Python check fails
        _pythonHelperMock.Setup(p => p.Run(
                It.Is<PythonOptions>(o => o.Args.Contains("--version")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "python: command not found")] });

        // Act
        var result = await _languageService.UpdateSnippets(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Python is not installed"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("Python"));
            Assert.That(result.NextSteps, Has.Some.Contains("azsdk_verify_setup"));
        });
    }

    [Test]
    public async Task UpdateSnippets_ReturnsNextStepsWithSnippetGuidance_WhenUpdateFails()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-snippets-update-fail-test");
        var packagePath = tempDir.DirectoryPath;

        _gitHelperMock.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempDir.DirectoryPath);

        var scriptDir = Path.Combine(tempDir.DirectoryPath, "eng", "tools", "azure-sdk-tools", "ci_tools", "snippet_update");
        Directory.CreateDirectory(scriptDir);
        File.WriteAllText(Path.Combine(scriptDir, "python_snippet_updater.py"), "# placeholder");

        // Python check succeeds
        _pythonHelperMock.Setup(p => p.Run(
                It.Is<PythonOptions>(o => o.Args.Contains("--version")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Python 3.11.0")] });

        // Snippet script fails
        _pythonHelperMock.Setup(p => p.Run(
                It.Is<PythonOptions>(o => !o.Args.Contains("--version")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "Snippet mismatch found")] });

        // Act
        var result = await _languageService.UpdateSnippets(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("snippets"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("snippet"));
        });
    }

    [Test]
    public async Task UpdateSnippets_ReturnsNextStepsWithInstallGuidance_WhenExceptionThrown()
    {
        // Arrange
        using var tempDir = TempDirectory.Create("python-snippets-exception-test");
        var packagePath = tempDir.DirectoryPath;

        _gitHelperMock.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Git not found"));

        // Act
        var result = await _languageService.UpdateSnippets(packagePath, false, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Error updating snippets"));
            Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.NextSteps, Has.Some.Contains("azsdk_verify_setup"));
        });
    }

    #endregion
}
