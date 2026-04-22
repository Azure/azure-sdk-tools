using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models;
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
            Mock.Of<ICopilotAgentRunner>(),
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

    #region CheckSpelling Tests

    [Test]
    public async Task CheckSpelling_DelegatesToCommonValidationHelpers_WithCorrectPath()
    {
        _packagePath = "/tmp/repo-root/sdk/package";
        var expectedSuccess = new PackageCheckResponse(0, "No spelling errors found");

        string? capturedPackagePath = null;
        _commonValidationHelpersMock
            .Setup(c => c.CheckSpelling(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, CancellationToken>((pkgPath, _, _) =>
            {
                capturedPackagePath = pkgPath;
            })
            .ReturnsAsync(expectedSuccess);

        var response = await _languageChecks.CheckSpelling(_packagePath, false, CancellationToken.None);

        Assert.That(response.ExitCode, Is.EqualTo(0));
        Assert.That(capturedPackagePath, Is.EqualTo(_packagePath));

        _commonValidationHelpersMock.Verify(
            c => c.CheckSpelling(_packagePath, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CheckSpelling_WithFixEnabled_DelegatesToCommonValidationHelpers()
    {
        _packagePath = "/tmp/repo-root/sdk/package";
        var expectedSuccess = new PackageCheckResponse(0, "Spelling issues fixed");

        _commonValidationHelpersMock
            .Setup(c => c.CheckSpelling(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSuccess);

        var response = await _languageChecks.CheckSpelling(_packagePath, true, CancellationToken.None);

        Assert.That(response.ExitCode, Is.EqualTo(0));

        _commonValidationHelpersMock.Verify(
            c => c.CheckSpelling(_packagePath, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

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

    #region ApplyPatchesAsync Tests

    [Test]
    public async Task ApplyPatchesAsync_ReturnsEmpty_WhenSrcDirectoryDoesNotExist()
    {
        using var tempDir = TempDirectory.Create("js-apply-patches-no-src-test");

        var copilotRunnerMock = new Mock<ICopilotAgentRunner>();
        var service = CreateServiceWithCopilotRunner(copilotRunnerMock.Object);

        var result = await service.ApplyPatchesAsync(
            customizationRoot: Path.Combine(tempDir.DirectoryPath, "src"),
            packagePath: tempDir.DirectoryPath,
            buildContext: "some build error",
            ct: CancellationToken.None);

        Assert.That(result, Is.Empty);
        copilotRunnerMock.Verify(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ApplyPatchesAsync_ReturnsEmpty_WhenNoJsOrTsFilesInSrc()
    {
        using var tempDir = TempDirectory.Create("js-apply-patches-no-files-test");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);
        // Only a non-JS/TS file in src/
        await File.WriteAllTextAsync(Path.Combine(srcDir, "readme.md"), "# readme");

        var copilotRunnerMock = new Mock<ICopilotAgentRunner>();
        var service = CreateServiceWithCopilotRunner(copilotRunnerMock.Object);

        var result = await service.ApplyPatchesAsync(
            customizationRoot: srcDir,
            packagePath: tempDir.DirectoryPath,
            buildContext: "some build error",
            ct: CancellationToken.None);

        Assert.That(result, Is.Empty);
        copilotRunnerMock.Verify(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ApplyPatchesAsync_OnlyPassesSrcFiles_ToAgent_NotGeneratedFiles()
    {
        using var tempDir = TempDirectory.Create("js-apply-patches-src-only-test");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        var generatedDir = Path.Combine(tempDir.DirectoryPath, "generated");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(generatedDir);

        await File.WriteAllTextAsync(Path.Combine(srcDir, "customization.ts"), "export class MyClient {}");
        await File.WriteAllTextAsync(Path.Combine(generatedDir, "generated.ts"), "// generated code");

        CopilotAgent<string>? capturedAgent = null;
        var copilotRunnerMock = new Mock<ICopilotAgentRunner>();
        copilotRunnerMock
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Callback<CopilotAgent<string>, CancellationToken>((agent, _) => capturedAgent = agent)
            .ReturnsAsync(string.Empty);

        var service = CreateServiceWithCopilotRunner(copilotRunnerMock.Object);

        await service.ApplyPatchesAsync(
            customizationRoot: srcDir,
            packagePath: tempDir.DirectoryPath,
            buildContext: "some build error",
            ct: CancellationToken.None);

        Assert.That(capturedAgent, Is.Not.Null);
        Assert.That(capturedAgent!.Instructions, Does.Contain(Path.Combine("src", "customization.ts")));
        Assert.That(capturedAgent.Instructions, Does.Not.Contain(Path.Combine("generated", "generated.ts")));
        Assert.That(capturedAgent.Instructions, Does.Not.Contain("generated.ts"));
    }

    [Test]
    public async Task ApplyPatchesAsync_CollectsAllJsAndTsExtensions()
    {
        using var tempDir = TempDirectory.Create("js-apply-patches-extensions-test");
        var srcDir = Path.Combine(tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);

        // Create a file for each supported extension
        string[] extensions = ["ts", "tsx", "mts", "cts", "js", "jsx", "mjs", "cjs"];
        foreach (var ext in extensions)
        {
            await File.WriteAllTextAsync(Path.Combine(srcDir, $"file.{ext}"), $"// {ext} file");
        }

        CopilotAgent<string>? capturedAgent = null;
        var copilotRunnerMock = new Mock<ICopilotAgentRunner>();
        copilotRunnerMock
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Callback<CopilotAgent<string>, CancellationToken>((agent, _) => capturedAgent = agent)
            .ReturnsAsync(string.Empty);

        var service = CreateServiceWithCopilotRunner(copilotRunnerMock.Object);

        await service.ApplyPatchesAsync(
            customizationRoot: srcDir,
            packagePath: tempDir.DirectoryPath,
            buildContext: "some build error",
            ct: CancellationToken.None);

        Assert.That(capturedAgent, Is.Not.Null);
        foreach (var ext in extensions)
        {
            Assert.That(capturedAgent!.Instructions, Does.Contain($"file.{ext}"),
                $"Expected file.{ext} to be included in agent instructions");
        }
    }

    private JavaScriptLanguageService CreateServiceWithCopilotRunner(ICopilotAgentRunner copilotRunner)
        => new(
            _processHelperMock.Object,
            _npxHelperMock.Object,
            copilotRunner,
            _gitHelperMock.Object,
            NullLogger<JavaScriptLanguageService>.Instance,
            _commonValidationHelpersMock.Object,
            Mock.Of<IPackageInfoHelper>(),
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    
    #endregion
  
    #region RunAllTests Tests

    [Test]
    [TestCase(TestMode.Playback, "playback")]
    [TestCase(TestMode.Record, "record")]
    [TestCase(TestMode.Live, "live")]
    public async Task RunAllTests_SetsCorrectTestModeEnvironmentVariable(TestMode testMode, string expectedEnvValue)
    {
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        // For record mode, we need to handle the second call (asset push) too, but there's no assets.json
        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        var result = await _languageChecks.RunAllTests(_packagePath, testMode, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(capturedOptions, Is.Not.Null);
            Assert.That(capturedOptions!.EnvironmentVariables, Is.Not.Null);
            Assert.That(capturedOptions.EnvironmentVariables!["TEST_MODE"], Is.EqualTo(expectedEnvValue));
        });
    }

    [Test]
    public async Task RunAllTests_PassesThroughLiveTestEnvironmentVariables()
    {
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        var envVars = new Dictionary<string, string>
        {
            ["AZURE_SUBSCRIPTION_ID"] = "sub-123",
            ["AZURE_RESOURCE_GROUP"] = "rg-test",
        };

        var result = await _languageChecks.RunAllTests(_packagePath, TestMode.Live, envVars, ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(capturedOptions, Is.Not.Null);
            Assert.That(capturedOptions!.EnvironmentVariables!["AZURE_SUBSCRIPTION_ID"], Is.EqualTo("sub-123"));
            Assert.That(capturedOptions.EnvironmentVariables["AZURE_RESOURCE_GROUP"], Is.EqualTo("rg-test"));
            Assert.That(capturedOptions.EnvironmentVariables["TEST_MODE"], Is.EqualTo("live"));
        });
    }

    [Test]
    public async Task RunAllTests_UsesDefaultTimeoutForPlayback()
    {
        var processResult = new ProcessResult { ExitCode = 0 };

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(_packagePath, TestMode.Playback, ct: CancellationToken.None);

        Assert.That(capturedOptions!.Timeout, Is.EqualTo(ProcessOptions.DEFAULT_PROCESS_TIMEOUT));
    }

    [Test]
    [TestCase(TestMode.Record)]
    [TestCase(TestMode.Live)]
    public async Task RunAllTests_UsesLongerTimeoutForLiveAndRecordModes(TestMode testMode)
    {
        var processResult = new ProcessResult { ExitCode = 0 };

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(_packagePath, testMode, ct: CancellationToken.None);

        Assert.That(capturedOptions!.Timeout, Is.GreaterThan(ProcessOptions.DEFAULT_PROCESS_TIMEOUT));
    }

    [Test]
    public async Task RunAllTests_PushesAssetsAfterSuccessfulRecordMode()
    {
        using var tempDir = TempDirectory.Create("js-asset-push-test");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "assets.json"), "{}");

        var testResult = new ProcessResult { ExitCode = 0 };
        testResult.AppendStdout("Tests passed!");

        var pushResult = new ProcessResult { ExitCode = 0 };
        pushResult.AppendStdout("Assets pushed!");

        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResult);

        _npxHelperMock
            .Setup(p => p.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pushResult);

        var result = await _languageChecks.RunAllTests(tempDir.DirectoryPath, TestMode.Record, ct: CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        // Verify test run was called via processHelper
        _processHelperMock.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        // Verify asset push was called via npxHelper
        _npxHelperMock.Verify(p => p.Run(
            It.Is<NpxOptions>(o => o.Args.Contains("test-proxy") && o.Args.Contains("push")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_DoesNotPushAssetsInPlaybackMode()
    {
        using var tempDir = TempDirectory.Create("js-no-push-playback-test");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "assets.json"), "{}");

        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        await _languageChecks.RunAllTests(tempDir.DirectoryPath, TestMode.Playback, ct: CancellationToken.None);

        // Only the test run should be called, not asset push
        _processHelperMock.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_DoesNotPushAssetsWhenTestsFail()
    {
        using var tempDir = TempDirectory.Create("js-no-push-fail-test");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "assets.json"), "{}");

        var processResult = new ProcessResult { ExitCode = 1 };
        processResult.AppendStderr("Tests failed!");

        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        var result = await _languageChecks.RunAllTests(tempDir.DirectoryPath, TestMode.Record, ct: CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        // Only the test run should be called, not asset push
        _processHelperMock.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_DoesNotPushAssetsWhenNoAssetsJson()
    {
        var processResult = new ProcessResult { ExitCode = 0 };
        processResult.AppendStdout("Tests passed!");

        _processHelperMock
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processResult);

        // _packagePath doesn't have an assets.json file
        var result = await _languageChecks.RunAllTests(_packagePath, TestMode.Record, ct: CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        // Only the test run should be called
        _processHelperMock.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAllTests_DefaultMode_IsPlayback()
    {
        var processResult = new ProcessResult { ExitCode = 0 };

        ProcessOptions? capturedOptions = null;
        _processHelperMock
            .Setup(p => p.Run(It.Is<ProcessOptions>(o => o.Args.Contains("test")), It.IsAny<CancellationToken>()))
            .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        // Call without specifying testMode - should default to Playback
        await _languageChecks.RunAllTests(_packagePath, ct: CancellationToken.None);

        Assert.That(capturedOptions!.EnvironmentVariables!["TEST_MODE"], Is.EqualTo("playback"));
    }

    #endregion
}
