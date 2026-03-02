using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
internal class JavaScriptLanguageSpecificChecksTests
{
    private Mock<IProcessHelper> _processHelperMock = null!;
    private Mock<INpxHelper> _npxHelperMock = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<ICommonValidationHelpers> _commonValidationHelpersMock = null!;
    private JavaScriptLanguageService _languageChecks = null!;
    private string _packagePath = null!;

    [SetUp]
    public void SetUp()
    {
        _processHelperMock = new Mock<IProcessHelper>();
        _npxHelperMock = new Mock<INpxHelper>();
        _gitHelperMock = new Mock<IGitHelper>();
        _gitHelperMock.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-js");
        _commonValidationHelpersMock = new Mock<ICommonValidationHelpers>();

        _languageChecks = new JavaScriptLanguageService(
            _processHelperMock.Object,
            _npxHelperMock.Object,
            _gitHelperMock.Object,
            NullLogger<JavaScriptLanguageService>.Instance,
            _commonValidationHelpersMock.Object,
            Mock.Of<IPackageInfoHelper>(),
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());

        _packagePath = "/tmp/javascript-package";
    }

    [Test]
    public async Task UpdateSnippetsAsync_ReturnsSuccess_WhenProcessCompletes()
    {
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("snippets updated");

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        var response = await _languageChecks.UpdateSnippets(_packagePath, false, CancellationToken.None);

        Assert.That(response.ExitCode, Is.EqualTo(0));
        Assert.That(response.CheckStatusDetails, Is.EqualTo("snippets updated"));
        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.NextSteps, Is.Null);

        Assert.That(capturedOptions, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(capturedOptions!.WorkingDirectory, Is.EqualTo(_packagePath));
            Assert.That(
                capturedOptions.Command == "pnpm" || capturedOptions.Args.Contains("pnpm"),
                Is.True,
                "Expected pnpm command to be invoked");
            Assert.That(capturedOptions.Args.Contains("update-snippets"), Is.True);
        });

        _processHelperMock.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateSnippetsAsync_ReturnsNextSteps_WhenProcessFails()
    {
        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr("failure output");

        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var response = await _languageChecks.UpdateSnippets(_packagePath, false, CancellationToken.None);

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.CheckStatusDetails, Is.EqualTo("failure output"));
        Assert.That(response.NextSteps, Is.Not.Null);
        Assert.That(response.NextSteps, Has.Count.EqualTo(1));
        Assert.That(response.NextSteps![0], Does.Contain("Review the error output"));
    }

    [Test]
    public async Task UpdateSnippetsAsync_ReturnsErrorResponse_WhenProcessThrows()
    {
        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("process failed"));

        var response = await _languageChecks.UpdateSnippets(_packagePath, false, CancellationToken.None);

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.CheckStatusDetails, Is.EqualTo(string.Empty));
        Assert.That(response.ResponseError, Does.Contain("Error updating snippets: process failed"));
        Assert.That(response.NextSteps, Is.Null);
    }

    #region HasCustomizations Tests

    [Test]
    public void HasCustomizations_ReturnsPath_WhenGeneratedFolderExists()
    {
        using var tempDir = TempDirectory.Create("js-customization-test");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        var generatedDir = Path.Combine(tempDir.DirectoryPath, "generated");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(generatedDir);

        var result = _languageChecks.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(srcDir));
    }

    [Test]
    public void HasCustomizations_ReturnsNull_WhenNoGeneratedFolderExists()
    {
        using var tempDir = TempDirectory.Create("js-no-customization-test");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);

        var result = _languageChecks.HasCustomizations(tempDir.DirectoryPath, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion
}
