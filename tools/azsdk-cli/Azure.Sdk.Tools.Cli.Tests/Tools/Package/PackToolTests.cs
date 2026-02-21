// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class PackToolTests
{
    #region Test Constants

    private const string InvalidProjectPathError = "Failed to detect the language from package path";
    private const string EmptyPathError = "Package path is required and cannot be empty";

    #endregion

    private PackTool _tool;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<IProcessHelper> _mockProcessHelper;
    private Mock<IPythonHelper> _mockPythonHelper;
    private Mock<INpxHelper> _mockNpxHelper;
    private Mock<IPowershellHelper> _mockPowerShellHelper;
    private Mock<IMavenHelper> _mockMavenHelper;
    private Mock<ISpecGenSdkConfigHelper> _mockSpecGenSdkConfigHelper;
    private TestLogger<PackTool> _logger;
    private TempDirectory _tempDirectory;
    private List<LanguageService> _languageServices;
    private Mock<ICommonValidationHelpers> _commonValidationHelpers;

    [SetUp]
    public void Setup()
    {
        _mockGitHelper = new Mock<IGitHelper>();
        _mockProcessHelper = new Mock<IProcessHelper>();
        _mockPythonHelper = new Mock<IPythonHelper>();
        _mockSpecGenSdkConfigHelper = new Mock<ISpecGenSdkConfigHelper>();
        _mockNpxHelper = new Mock<INpxHelper>();
        _mockPowerShellHelper = new Mock<IPowershellHelper>();
        _mockMavenHelper = new Mock<IMavenHelper>();
        _logger = new TestLogger<PackTool>();
        _commonValidationHelpers = new Mock<ICommonValidationHelpers>();

        var languageLogger = new TestLogger<LanguageService>();
        var mockMicrohostAgent = new Mock<IMicroagentHostService>();

        _tempDirectory = TempDirectory.Create("PackToolTests");
        _languageServices = [
            new PythonLanguageService(_mockProcessHelper.Object, _mockPythonHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
            new JavaLanguageService(_mockProcessHelper.Object, _mockGitHelper.Object, _mockMavenHelper.Object, mockMicrohostAgent.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
            new JavaScriptLanguageService(_mockProcessHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
            new GoLanguageService(_mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>()),
            new DotnetLanguageService(_mockProcessHelper.Object, _mockPowerShellHelper.Object, _mockGitHelper.Object, languageLogger, _commonValidationHelpers.Object, Mock.Of<IFileHelper>(), _mockSpecGenSdkConfigHelper.Object, Mock.Of<IChangelogHelper>())
        ];

        _tool = new PackTool(
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

    #region Input Validation Tests

    [Test]
    public async Task PackAsync_EmptyPath_ReturnsFailure()
    {
        // Act
        var result = await _tool.PackAsync(string.Empty);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(EmptyPathError));
    }

    [Test]
    public async Task PackAsync_NullPath_ReturnsFailure()
    {
        // Act
        var result = await _tool.PackAsync(null!);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(EmptyPathError));
    }

    [Test]
    public async Task PackAsync_WhitespacePath_ReturnsFailure()
    {
        // Act
        var result = await _tool.PackAsync("   ");

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(EmptyPathError));
    }

    [Test]
    public async Task PackAsync_NonexistentPath_ReturnsFailure()
    {
        // Act
        var result = await _tool.PackAsync("/nonexistent/path");

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain(InvalidProjectPathError));
    }

    #endregion

    #region Language Detection Tests

    [Test]
    public async Task PackAsync_UnknownLanguage_ReturnsFailure()
    {
        // Arrange
        _mockGitHelper
            .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("unknown-repo");

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Failed to detect the language"));
    }

    [Test]
    public async Task PackAsync_GoProject_ReturnsNoop()
    {
        // Arrange
        _mockGitHelper
            .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-go");

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.Result, Is.EqualTo("noop"));
        Assert.That(result.Message, Does.Contain("Go SDK does not produce distributable artifacts"));
    }

    #endregion

    #region .NET Pack Tests

    [Test]
    public async Task PackAsync_DotNet_Success_ReturnsSuccessWithArtifact()
    {
        // Arrange
        SetupDotNetRepo();

        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(p => p.Command == "dotnet" && p.Args.Contains("pack")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 0,
                OutputDetails = [
                    (StdioLevel.StandardOutput, $"Successfully created package '{Path.Combine(_tempDirectory.DirectoryPath, "bin", "Release", "MyPackage.1.0.0.nupkg")}'."),
                ]
            });

        // Create the nupkg file for fallback detection
        var releaseDir = Path.Combine(_tempDirectory.DirectoryPath, "bin", "Release");
        Directory.CreateDirectory(releaseDir);
        File.WriteAllText(Path.Combine(releaseDir, "MyPackage.1.0.0.nupkg"), "fake nupkg");

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors, Is.Null.Or.Empty);
        Assert.That(result.Message, Does.Contain("Pack completed successfully"));
    }

    [Test]
    public async Task PackAsync_DotNet_WithOutputPath_PassesOutputArg()
    {
        // Arrange
        SetupDotNetRepo();
        var outputDir = Path.Combine(_tempDirectory.DirectoryPath, "output");
        Directory.CreateDirectory(outputDir);

        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(p =>
                p.Command == "dotnet" &&
                p.Args.Contains("pack") &&
                p.Args.Contains("--output") &&
                p.Args.Contains(outputDir)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [] });

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath, outputDir);

        // Assert
        Assert.That(result.ResponseErrors, Is.Null.Or.Empty);
        Assert.That(result.Message, Does.Contain("Pack completed successfully"));
        _mockProcessHelper.Verify(x => x.Run(
            It.Is<ProcessOptions>(p => p.Args.Contains("--output") && p.Args.Contains(outputDir)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PackAsync_DotNet_BuildFails_ReturnsFailure()
    {
        // Arrange
        SetupDotNetRepo();

        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(p => p.Command == "dotnet" && p.Args.Contains("pack")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                OutputDetails = [(StdioLevel.StandardError, "error CS1234: Something went wrong")]
            });

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("dotnet pack failed"));
        Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task PackAsync_DotNet_UsesNoBuildFlag()
    {
        // Arrange
        SetupDotNetRepo();

        _mockProcessHelper
            .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [] });

        // Act
        await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert - verify --no-build is passed
        _mockProcessHelper.Verify(x => x.Run(
            It.Is<ProcessOptions>(p => p.Args.Contains("--no-build")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Java Pack Tests

    [Test]
    public async Task PackAsync_Java_Success_ReturnsSuccess()
    {
        // Arrange
        SetupJavaRepo();

        _mockMavenHelper
            .Setup(x => x.Run(It.Is<MavenOptions>(m => m.Goal == "clean" && m.Args.Contains("package")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [] });

        // Create target directory with a jar file
        var targetDir = Path.Combine(_tempDirectory.DirectoryPath, "target");
        Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(targetDir, "my-package-1.0.0.jar"), "fake jar");

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors, Is.Null.Or.Empty);
        Assert.That(result.Message, Does.Contain("Pack completed successfully"));
    }

    [Test]
    public async Task PackAsync_Java_WithOutputPath_PassesPackageOutputDirectory()
    {
        // Arrange
        SetupJavaRepo();
        var outputDir = Path.Combine(_tempDirectory.DirectoryPath, "output");
        Directory.CreateDirectory(outputDir);

        _mockMavenHelper
            .Setup(x => x.Run(It.Is<MavenOptions>(m =>
                m.Goal == "clean" &&
                m.Args.Contains("package") &&
                m.Args.Any(a => a.Contains($"-DpackageOutputDirectory={outputDir}"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [] });

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath, outputDir);

        // Assert
        Assert.That(result.ResponseErrors, Is.Null.Or.Empty);
        _mockMavenHelper.Verify(x => x.Run(
            It.Is<MavenOptions>(m => m.Args.Any(a => a.Contains($"-DpackageOutputDirectory={outputDir}"))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PackAsync_Java_Failure_ReturnsError()
    {
        // Arrange
        SetupJavaRepo();

        _mockMavenHelper
            .Setup(x => x.Run(It.Is<MavenOptions>(m => m.Goal == "clean" && m.Args.Contains("package")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                OutputDetails = [(StdioLevel.StandardError, "BUILD FAILURE")]
            });

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("mvn clean package failed"));
    }

    #endregion

    #region JavaScript Pack Tests

    [Test]
    public async Task PackAsync_JavaScript_Success_ReturnsSuccess()
    {
        // Arrange
        SetupJavaScriptRepo();

        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(p => p.Command == "pnpm" && p.Args.Contains("pack")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 0,
                OutputDetails = [(StdioLevel.StandardOutput, "my-package-1.0.0.tgz")]
            });

        // Create the tgz file so path detection works
        File.WriteAllText(Path.Combine(_tempDirectory.DirectoryPath, "my-package-1.0.0.tgz"), "fake tgz");

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors, Is.Null.Or.Empty);
        Assert.That(result.Message, Does.Contain("Pack completed successfully"));
    }

    [Test]
    public async Task PackAsync_JavaScript_WithOutputPath_PassesPackDestination()
    {
        // Arrange
        SetupJavaScriptRepo();
        var outputDir = Path.Combine(_tempDirectory.DirectoryPath, "output");
        Directory.CreateDirectory(outputDir);

        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(p =>
                p.Command == "pnpm" &&
                p.Args.Contains("pack") &&
                p.Args.Contains("--pack-destination") &&
                p.Args.Contains(outputDir)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [] });

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath, outputDir);

        // Assert
        Assert.That(result.ResponseErrors, Is.Null.Or.Empty);
        _mockProcessHelper.Verify(x => x.Run(
            It.Is<ProcessOptions>(p => p.Args.Contains("--pack-destination") && p.Args.Contains(outputDir)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PackAsync_JavaScript_Failure_ReturnsError()
    {
        // Arrange
        SetupJavaScriptRepo();

        _mockProcessHelper
            .Setup(x => x.Run(It.Is<ProcessOptions>(p => p.Command == "pnpm"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                OutputDetails = [(StdioLevel.StandardError, "ERR_PNPM_NO_PACKAGE_JSON")]
            });

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("pnpm pack failed"));
    }

    #endregion

    #region Python Pack Tests

    [Test]
    public async Task PackAsync_Python_Success_ReturnsSuccess()
    {
        // Arrange
        SetupPythonRepo();

        _mockPythonHelper
            .Setup(x => x.Run(It.Is<PythonOptions>(p => p.Args.Contains("-m") && p.Args.Contains("build")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [] });

        // Create dist directory with wheel file
        var distDir = Path.Combine(_tempDirectory.DirectoryPath, "dist");
        Directory.CreateDirectory(distDir);
        File.WriteAllText(Path.Combine(distDir, "my_package-1.0.0-py3-none-any.whl"), "fake wheel");

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors, Is.Null.Or.Empty);
        Assert.That(result.Message, Does.Contain("Pack completed successfully"));
    }

    [Test]
    public async Task PackAsync_Python_WithOutputPath_PassesOutdir()
    {
        // Arrange
        SetupPythonRepo();
        var outputDir = Path.Combine(_tempDirectory.DirectoryPath, "output");
        Directory.CreateDirectory(outputDir);

        _mockPythonHelper
            .Setup(x => x.Run(It.Is<PythonOptions>(p =>
                p.Args.Contains("-m") &&
                p.Args.Contains("build") &&
                p.Args.Contains("--outdir") &&
                p.Args.Contains(outputDir)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [] });

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath, outputDir);

        // Assert
        Assert.That(result.ResponseErrors, Is.Null.Or.Empty);
        _mockPythonHelper.Verify(x => x.Run(
            It.Is<PythonOptions>(p => p.Args.Contains("--outdir") && p.Args.Contains(outputDir)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PackAsync_Python_Failure_ReturnsError()
    {
        // Arrange
        SetupPythonRepo();

        _mockPythonHelper
            .Setup(x => x.Run(It.IsAny<PythonOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult
            {
                ExitCode = 1,
                OutputDetails = [(StdioLevel.StandardError, "ModuleNotFoundError: No module named 'build'")]
            });

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("python -m build failed"));
    }

    #endregion

    #region Exception Handling Tests

    [Test]
    public async Task PackAsync_UnhandledException_ReturnsFailure()
    {
        // Arrange
        SetupDotNetRepo();

        _mockProcessHelper
            .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await _tool.PackAsync(_tempDirectory.DirectoryPath);

        // Assert
        Assert.That(result.ResponseErrors?.First(), Does.Contain("Unexpected error"));
        Assert.That(result.NextSteps, Is.Not.Null.And.Not.Empty);
    }

    #endregion

    #region Command Tests

    [Test]
    public void GetCommand_ReturnsCommandWithCorrectStructure()
    {
        // Act
        var command = _tool.GetCommandInstances().First();

        // Assert
        Assert.That(command.Name, Is.EqualTo("pack"));
        Assert.That(command.Description, Does.Contain("distributable artifacts"));
    }

    [Test]
    public void CommandHierarchy_IsPackage()
    {
        // Assert
        Assert.That(_tool.CommandHierarchy, Has.Length.EqualTo(1));
        Assert.That(_tool.CommandHierarchy[0].Verb, Is.EqualTo("pkg"));
    }

    #endregion

    #region Helper Methods

    private void SetupDotNetRepo()
    {
        _mockGitHelper
            .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-net");
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);
    }

    private void SetupJavaRepo()
    {
        _mockGitHelper
            .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-java");
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);
    }

    private void SetupJavaScriptRepo()
    {
        _mockGitHelper
            .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-js");
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);
    }

    private void SetupPythonRepo()
    {
        _mockGitHelper
            .Setup(x => x.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-python");
        _mockGitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);
    }

    #endregion
}
