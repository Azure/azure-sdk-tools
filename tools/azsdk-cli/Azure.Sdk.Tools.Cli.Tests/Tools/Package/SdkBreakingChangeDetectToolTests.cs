// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tests.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class SdkBreakingChangeDetectToolTests
{
    private const string BreakingChanges = "### Breaking Changes\n\n- CP0002: Member 'Azure.Test.Widget.Name' was removed.\n\n### Features Added\n\n- Property 'Azure.Test.Widget.DisplayName' was added.";
    private const string Additions = "### Features Added\n\n- Property 'Azure.Test.Widget.DisplayName' was added.";
    private TempDirectory _tempDirectory = null!;
    private Mock<LanguageService> _languageService = null!;
    private Mock<IGitHelper> _gitHelper = null!;
    private Mock<ISpecGenSdkConfigHelper> _configHelper = null!;
    private Mock<ISdkBreakingChangeClassificationService> _classifier = null!;
    private SdkBreakingChangeDetectTool _tool = null!;
    private string _packagePath = null!;
    private string? _scriptOutputPath;
    private SdkChangeDetails? _details;
    private TestLogger<SdkBreakingChangeDetectTool> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = TempDirectory.Create("sdk-breaking-change-tests");
        _packagePath = Path.Combine(_tempDirectory.DirectoryPath, "sdk", "test", "Azure.Test");
        Directory.CreateDirectory(_packagePath);
        _scriptOutputPath = null;
        _details = null;
        _languageService = new Mock<LanguageService> { CallBase = true };
        _languageService.SetupGet(s => s.Language).Returns(SdkLanguage.DotNet);
        _languageService.Setup(s => s.GetPackageInfo(_packagePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageInfo
            {
                PackageName = "Azure.Test",
                PackagePath = _packagePath,
                ServiceName = "test",
                Language = SdkLanguage.DotNet,
            });
        _languageService.Setup(s => s.GetSdkBreakingPattern(_tempDirectory.DirectoryPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Verified .NET compatibility patterns");
        _gitHelper = new Mock<IGitHelper>();
        _gitHelper.Setup(g => g.GetRepoNameAsync(_packagePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-net");
        _gitHelper.Setup(g => g.DiscoverRepoRootAsync(_packagePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory.DirectoryPath);
        _configHelper = new Mock<ISpecGenSdkConfigHelper>();
        _configHelper.Setup(c => c.GetConfigurationAsync(_tempDirectory.DirectoryPath, SpecGenSdkConfigType.GetSdkChanges, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpecGenSdkConfigContentType.Unknown, string.Empty));
        _classifier = new Mock<ISdkBreakingChangeClassificationService>();
        var dotnetService = DotnetLanguageServiceBreakingChangeTests.CreateService(_configHelper.Object);
        _languageService.Setup(s => s.ValidateBreakingChangeClassification(It.IsAny<SdkBreakingChangeDetectionResult>()))
            .Returns<SdkBreakingChangeDetectionResult>(dotnetService.ValidateBreakingChangeClassification);
        _logger = new TestLogger<SdkBreakingChangeDetectTool>();
        _tool = new SdkBreakingChangeDetectTool(
            _gitHelper.Object,
            _logger,
            [_languageService.Object],
            _configHelper.Object,
            _classifier.Object);
    }

    [TearDown]
    public void TearDown()
    {
        if (_scriptOutputPath != null)
        {
            if (Directory.Exists(_scriptOutputPath))
            {
                Directory.Delete(_scriptOutputPath);
            }
            else
            {
                File.Delete(_scriptOutputPath);
            }
        }
        _tempDirectory.Dispose();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ConfiguredDetector_ChangesOnly_PreservesChangesWithoutClassification(bool hasBreakingChange)
    {
        ConfigureDetectorReport(hasBreakingChange);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath, changesOnly: true);

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(hasBreakingChange ? SdkBreakingChangeStatus.Detected : SdkBreakingChangeStatus.Clean));
        Assert.That(response.Language, Is.EqualTo(SdkLanguage.DotNet));
        var result = GetResult(response);
        Assert.That(result.HasBreakingChange, Is.EqualTo(hasBreakingChange));
        Assert.That(result.SdkChangeMD, Is.EqualTo(hasBreakingChange ? BreakingChanges : Additions));
        Assert.That(JsonSerializer.Serialize(result.Details), Is.EqualTo(JsonSerializer.Serialize(_details)));
        Assert.That(result.BreakingChanges, Is.Empty);
        _classifier.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ConfiguredDetector_BreakingChanges_UsesCommonClassificationAndPreservesEvidence()
    {
        ConfigureDetectorReport(true);
        var tspConfigPath = Path.Combine(_tempDirectory.DirectoryPath, "tspconfig.yaml");
        _classifier.Setup(c => c.ClassifySdkBreakingChangesAsync(
                BreakingChanges, "Verified .NET compatibility patterns", "DotNet",
                _tempDirectory.DirectoryPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SdkBreakingChangeDetectionResult
            {
                HasBreakingChange = true,
                SdkChangeMD = "Untrusted rewritten evidence",
                Details = new SdkChangeDetails { BaselineVersion = "invented" },
                BreakingChanges =
                [
                    new SdkBreakingChange
                    {
                        BreakingChange = "Widget.Name was removed; a possible rename requires review.",
                        Category = SdkBreakingChangeCategory.Unknown,
                        Mitigation = SdkBreakingChangeMitigation.Manual,
                        OriginBreaks = ["CP0002: Member 'Azure.Test.Widget.Name' was removed."],
                    },
                ],
            });

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath, tspConfigPath);

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Classified));
        Assert.That(GetResult(response).SdkChangeMD, Is.EqualTo(BreakingChanges));
        Assert.That(JsonSerializer.Serialize(GetResult(response).Details), Is.EqualTo(JsonSerializer.Serialize(_details)));
        Assert.That(GetResult(response).BreakingChanges, Has.Count.EqualTo(1));
        _classifier.VerifyAll();
    }

    [Test]
    public async Task ConfiguredDetector_NoBreaks_DoesNotInvokeClassifier()
    {
        ConfigureDetectorReport(false);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Clean));
        Assert.That(GetResult(response).HasBreakingChange, Is.False);
        _classifier.VerifyNoOtherCalls();
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task ConfiguredDetector_NoGaBaseline_DoesNotClaimCompatibilityPass(bool breaking, bool changesOnly)
    {
        ConfigureScript(JsonSerializer.Serialize(new SdkChange
            {
                HasBreakingChange = breaking,
                SdkChangeMD = "No GA release.",
                Details = new SdkChangeDetails { Limitations = ["No GA baseline is available."] },
            }));

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath, changesOnly: changesOnly);

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Inconclusive));
        Assert.That(GetResult(response).HasBreakingChange, Is.EqualTo(breaking));
        Assert.That(response.Message, Does.Contain("compatibility was not evaluated"));
        Assert.That(GetResult(response).Details!.Limitations, Is.Not.Empty);
        _classifier.VerifyNoOtherCalls();
    }

    [Test]
    public async Task MissingPatternCatalog_PreservesDetectedCompatibilityEvidence()
    {
        ConfigureDetectorReport(true);
        _languageService.Setup(s => s.GetSdkBreakingPattern(_tempDirectory.DirectoryPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Pattern catalog is missing."));

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.ExitCode, Is.Not.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(response.Language, Is.EqualTo(SdkLanguage.DotNet));
        Assert.That(GetResult(response).HasBreakingChange, Is.True);
        Assert.That(JsonSerializer.Serialize(GetResult(response).Details), Is.EqualTo(JsonSerializer.Serialize(_details)));
        Assert.That(GetResult(response).SdkChangeMD, Is.EqualTo(BreakingChanges));
        _classifier.VerifyNoOtherCalls();
    }

    [TestCase("null")]
    [TestCase("noBreaks")]
    [TestCase("empty")]
    public async Task ClassificationFailure_PreservesDetectedBreaks(string classification)
    {
        ConfigureDetectorReport(true);
        _classifier.Setup(c => c.ClassifySdkBreakingChangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(classification == "null" ? null : new SdkBreakingChangeDetectionResult
            {
                HasBreakingChange = classification != "noBreaks",
                BreakingChanges = [],
            });

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.ExitCode, Is.Not.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(GetResult(response).HasBreakingChange, Is.True);
        Assert.That(GetResult(response).SdkChangeMD, Is.EqualTo(BreakingChanges));
        Assert.That(JsonSerializer.Serialize(GetResult(response).Details), Is.EqualTo(JsonSerializer.Serialize(_details)));
    }

    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net")]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python")]
    public async Task ConfiguredScript_TakesPrecedenceAndCleansOutput(SdkLanguage language, string repoName)
    {
        _languageService.SetupGet(s => s.Language).Returns(language);
        _gitHelper.Setup(g => g.GetRepoNameAsync(_packagePath, It.IsAny<CancellationToken>())).ReturnsAsync(repoName);
        ConfigureDetectorReport(true);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath, changesOnly: true);

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(GetResult(response).HasBreakingChange, Is.True);
        Assert.That(GetResult(response).SdkChangeMD, Is.EqualTo(BreakingChanges));
        Assert.That(response.Language, Is.EqualTo(language));
        Assert.That(File.Exists(_scriptOutputPath), Is.False);
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("not json")]
    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("{\"changes\":\"text\"}")]
    [TestCase("{\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":null,\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":\"\",\"hasBreakingChange\":true}")]
    [TestCase("{\"changes\":\"\",\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":\" \\t\\n\",\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":\" \\t\\n\",\"hasBreakingChange\":true}")]
    [TestCase("{\"changes\":\"text\",\"hasBreakingChange\":\"false\"}")]
    public async Task InvalidScriptOutput_FailsWithoutFallbackAndCleansOutput(string output)
    {
        ConfigureScript(output);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.ExitCode, Is.Not.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(string.Join("\n", response.ResponseErrors), Does.Not.Contain("Object reference"));
        Assert.That(response.Result, Is.Not.InstanceOf<SdkBreakingChangeDetectionResult>());
        Assert.That(File.Exists(_scriptOutputPath), Is.False);
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _classifier.VerifyNoOtherCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ScriptFailure_PreservesErrorAndDoesNotConsumeOutput(bool singleError)
    {
        ConfigureScript("{\"changes\":\"\",\"hasBreakingChange\":false}", "ApiCompat could not resolve the baseline assembly.", singleError);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(string.Join("\n", response.ResponseErrors), Does.Contain("could not resolve the baseline assembly"));
        Assert.That(File.Exists(_scriptOutputPath), Is.False);
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _classifier.VerifyNoOtherCalls();
    }

    [Test]
    public async Task MissingScriptOutput_FailsRatherThanReportingNoBreaks()
    {
        ConfigureScript(null);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(string.Join("\n", response.ResponseErrors), Does.Contain("did not produce its output"));
        Assert.That(response.Result, Is.Not.InstanceOf<SdkBreakingChangeDetectionResult>());
    }

    [Test]
    public async Task LocalReport_IsUsedWithoutRunningDetection()
    {
        var path = Path.Combine(_tempDirectory.DirectoryPath, "changes.json");
        await File.WriteAllTextAsync(path, "{\"changes\":\"### Features Added\\n- Widget\",\"hasBreakingChange\":false}");

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath, localSdkChangeJsonFilePath: path);

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Inconclusive));
        Assert.That(GetResult(response).SdkChangeMD, Does.Contain("Widget"));
        _configHelper.VerifyNoOtherCalls();
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LocalReport_PreservesStructuredDetailsInCommonJsonResult()
    {
        var path = Path.Combine(_tempDirectory.DirectoryPath, "changes.json");
        await File.WriteAllTextAsync(path, """
            {
                "changes": "### Features Added\n- Widget.DisplayName",
                "hasBreakingChange": false,
                "details": {
                    "baselineVersion": "1.2.3",
                    "apiChanges": [{
                        "kind": "added",
                        "symbol": "P:Azure.Test.Widget.DisplayName",
                        "description": "Property added",
                        "isBreaking": false,
                        "targetFramework": "netstandard2.0"
                    }],
                    "diagnostics": ["Original ApiCompat output"],
                    "limitations": ["Behavior changes require review."]
                }
            }
            """);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath, localSdkChangeJsonFilePath: path);

        Assert.That(response.ExitCode, Is.Zero);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var details = document.RootElement.GetProperty("result").GetProperty("details");
        Assert.That(details.GetProperty("baselineVersion").GetString(), Is.EqualTo("1.2.3"));
        Assert.That(details.GetProperty("apiChanges")[0].GetProperty("symbol").GetString(), Is.EqualTo("P:Azure.Test.Widget.DisplayName"));
        Assert.That(details.GetProperty("diagnostics")[0].GetString(), Is.EqualTo("Original ApiCompat output"));
        Assert.That(details.GetProperty("limitations")[0].GetString(), Is.EqualTo("Behavior changes require review."));
    }

    [Test]
    public async Task InvalidLocalReport_DoesNotSilentlyReplaceEvidence()
    {
        var path = Path.Combine(_tempDirectory.DirectoryPath, "changes.json");
        await File.WriteAllTextAsync(path, "{}");

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath, localSdkChangeJsonFilePath: path);

        Assert.That(response.ExitCode, Is.Not.Zero);
        _configHelper.VerifyNoOtherCalls();
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ChangesOnly_IgnoresLocalReport()
    {
        ConfigureDetectorReport(true);
        var path = Path.Combine(_tempDirectory.DirectoryPath, "changes.json");
        await File.WriteAllTextAsync(path, "not json");

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath, changesOnly: true, localSdkChangeJsonFilePath: path);

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(GetResult(response).HasBreakingChange, Is.True);
        _configHelper.Verify(s => s.ExecuteProcessAsync(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>(),
            It.IsAny<PackageInfo?>(), It.IsAny<string>(), It.IsAny<string[]?>()), Times.Once);
    }

    [Test]
    public async Task MissingDotnetDetectorConfiguration_IsBlockedWithoutDefaultBuild()
    {
        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.ExitCode, Is.Not.Zero);
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Blocked));
        Assert.That(string.Join("\n", response.ResponseErrors), Does.Contain("packageOptions.getSdkChangesScript"));
        Assert.That(response.Result, Is.Not.InstanceOf<SdkBreakingChangeDetectionResult>());
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Cancellation_IsNotConvertedIntoDetectionResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => _tool.DetectSDKBreakingChangesAsync(_packagePath, ct: cts.Token));
    }

    [Test]
    public void ScriptCancellation_CleansPartialReportEvenIfProcessHelperReturnsAnError()
    {
        using var cts = new CancellationTokenSource();
        ConfigureScript("{}");
        _configHelper.Setup(c => c.ExecuteProcessAsync(
                It.IsAny<ProcessOptions>(), cts.Token, It.IsAny<PackageInfo?>(),
                It.IsAny<string>(), It.IsAny<string[]?>()))
            .ReturnsAsync(() =>
            {
                File.WriteAllText(_scriptOutputPath!, "{}");
                cts.Cancel();
                return PackageOperationResponse.CreateFailure("Process was canceled.");
            });

        Assert.ThrowsAsync<OperationCanceledException>(() => _tool.DetectSDKBreakingChangesAsync(_packagePath, ct: cts.Token));

        Assert.That(File.Exists(_scriptOutputPath), Is.False);
        _classifier.VerifyNoOtherCalls();
    }

    [TestCase(SdkLanguage.Go, "azure-sdk-for-go", true)]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", true)]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", true)]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python", true)]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go", false)]
    public async Task MissingConfiguration_OtherLanguagesRetainDefaultDetection(SdkLanguage language, string repository, bool success)
    {
        _languageService.SetupGet(s => s.Language).Returns(language);
        _gitHelper.Setup(g => g.GetRepoNameAsync(_packagePath, It.IsAny<CancellationToken>())).ReturnsAsync(repository);
        var fallback = success ? PackageOperationResponse.CreateSuccess("Detected")
            : PackageOperationResponse.CreateFailure("Detection failed");
        _languageService.Setup(s => s.DetectSdkBreakingChangeAsync(_packagePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallback);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response, Is.SameAs(fallback));
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(success ? SdkBreakingChangeStatus.Clean : SdkBreakingChangeStatus.Failed));
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(_packagePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(null)]
    [TestCase((SdkBreakingChangeMitigation)999)]
    public async Task DotnetClassification_RejectsInvalidRoutesAndPreservesRawEvidence(SdkBreakingChangeMitigation? mitigation)
    {
        ConfigureDetectorReport(true);
        ConfigureClassification(mitigation);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(response.ResponseError, Does.Contain("supported mitigation route"));
        Assert.That(GetResult(response).HasBreakingChange, Is.True);
        Assert.That(GetResult(response).SdkChangeMD, Is.EqualTo(BreakingChanges));
        Assert.That(GetResult(response).BreakingChanges, Is.Empty);
    }

    [TestCase(SdkBreakingChangeMitigation.Generator)]
    [TestCase(SdkBreakingChangeMitigation.ClientCustomization)]
    [TestCase(SdkBreakingChangeMitigation.Manual)]
    public async Task DotnetClassification_AcceptsValidRoutes(SdkBreakingChangeMitigation route)
    {
        ConfigureDetectorReport(true);
        ConfigureClassification(route);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Classified));
        Assert.That(GetResult(response).BreakingChanges.Single().Mitigation, Is.EqualTo(route));
    }

    [TestCase(SdkLanguage.Go, "azure-sdk-for-go")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python")]
    public async Task OtherLanguageClassification_DoesNotRequireDotnetMitigation(SdkLanguage language, string repository)
    {
        _languageService.SetupGet(s => s.Language).Returns(language);
        _gitHelper.Setup(g => g.GetRepoNameAsync(_packagePath, It.IsAny<CancellationToken>())).ReturnsAsync(repository);
        _languageService.Setup(s => s.ValidateBreakingChangeClassification(It.IsAny<SdkBreakingChangeDetectionResult>())).CallBase();
        ConfigureDetectorReport(true);
        ConfigureClassification(null);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Classified));
        Assert.That(GetResult(response).BreakingChanges.Single().Mitigation, Is.Null);
    }

    [Test]
    public async Task Classification_UsesLanguageValidationExtensionPoint()
    {
        ConfigureDetectorReport(true);
        ConfigureClassification(SdkBreakingChangeMitigation.Manual);
        _languageService.Setup(s => s.ValidateBreakingChangeClassification(It.IsAny<SdkBreakingChangeDetectionResult>()))
            .Returns("Language-specific classification requirement failed.");

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(response.ResponseError, Does.Contain("Language-specific"));
        Assert.That(GetResult(response).SdkChangeMD, Is.EqualTo(BreakingChanges));
        _languageService.Verify(s => s.ValidateBreakingChangeClassification(It.IsAny<SdkBreakingChangeDetectionResult>()), Times.Once);
    }

    [TestCase("null")]
    [TestCase("\"\"")]
    [TestCase("\"  \"")]
    public async Task DotnetReport_UnknownBaselineIsInconclusive(string baseline)
    {
        ConfigureScript("""{"changes":"No changes","hasBreakingChange":false,"details":{"baselineVersion":""" + baseline + "}}");

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Inconclusive));
        Assert.That(response.OperationStatus, Is.EqualTo(Status.Succeeded));
        _classifier.VerifyNoOtherCalls();
    }

    [Test]
    public async Task InvalidConfiguration_IsFailureNotBlockedOrClean()
    {
        _configHelper.Setup(c => c.GetConfigurationAsync(It.IsAny<string>(), SpecGenSdkConfigType.GetSdkChanges, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("Invalid detector command."));

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(response.ResponseErrors.Single(), Does.Contain("Invalid detector command"));
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ClassificationException_PreservesRawEvidenceAndReportsFailure()
    {
        ConfigureDetectorReport(true);
        _classifier.Setup(c => c.ClassifySdkBreakingChangesAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Classifier unavailable."));

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(response.ResponseErrors.Single(), Does.Contain("Classifier unavailable"));
        Assert.That(GetResult(response).HasBreakingChange, Is.True);
        Assert.That(GetResult(response).SdkChangeMD, Is.EqualTo(BreakingChanges));
        Assert.That(JsonSerializer.Serialize(GetResult(response).Details), Is.EqualTo(JsonSerializer.Serialize(_details)));
    }

    [TestCase(SpecGenSdkConfigContentType.Command)]
    [TestCase(SpecGenSdkConfigContentType.ScriptPath)]
    public async Task ConfiguredDetector_UsesCommonProcessOptionsForBothConfigForms(SpecGenSdkConfigContentType contentType)
    {
        ConfigureScript(JsonSerializer.Serialize(new SdkChange
        {
            SdkChangeMD = Additions,
            Details = new SdkChangeDetails { BaselineVersion = "1.0.0" },
        }), contentType: contentType);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Clean));
        _configHelper.Verify(c => c.CreateProcessOptions(contentType, It.IsAny<string>(),
            _tempDirectory.DirectoryPath, _packagePath, It.IsAny<Dictionary<string, string>>(), 5), Times.Once);
    }

    [TestCase(SdkLanguage.Go, "azure-sdk-for-go")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python")]
    public async Task OtherLanguageCleanReport_DoesNotRequireDotnetBaseline(SdkLanguage language, string repository)
    {
        _languageService.SetupGet(s => s.Language).Returns(language);
        _gitHelper.Setup(g => g.GetRepoNameAsync(_packagePath, It.IsAny<CancellationToken>())).ReturnsAsync(repository);
        ConfigureScript("""{"changes":"No breaking changes","hasBreakingChange":false}""");

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Clean));
        Assert.That(GetResult(response).Details, Is.Null);
        _classifier.VerifyNoOtherCalls();
    }

    [Test]
    public async Task MissingProcessOptions_IsFailureWithoutFallback()
    {
        ConfigureDetectorReport(false);
        _configHelper.Setup(c => c.CreateProcessOptions(It.IsAny<SpecGenSdkConfigContentType>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), 5))
            .Returns((ProcessOptions?)null);

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        _languageService.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _classifier.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CleanupAccessFailure_LogsWarningWithoutReplacingPrimaryError()
    {
        ConfigureScript(null);
        _configHelper.Setup(c => c.ExecuteProcessAsync(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>(),
                It.IsAny<PackageInfo?>(), It.IsAny<string>(), It.IsAny<string[]?>()))
            .ReturnsAsync(() =>
            {
                Directory.CreateDirectory(_scriptOutputPath!);
                return PackageOperationResponse.CreateFailure("Primary detector failure.");
            });

        var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

        Assert.That(response.ResponseErrors.Single(), Does.Contain("Primary detector failure"));
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(_logger.Logs.Any(log => log.ToString()!.Contains("Could not delete SDK change report")), Is.True);
    }

    [Test]
    public void CleanupAccessFailure_PreservesCancellation()
    {
        ConfigureScript(null);
        using var cts = new CancellationTokenSource();
        _configHelper.Setup(c => c.ExecuteProcessAsync(It.IsAny<ProcessOptions>(), cts.Token,
                It.IsAny<PackageInfo?>(), It.IsAny<string>(), It.IsAny<string[]?>()))
            .ReturnsAsync(() =>
            {
                Directory.CreateDirectory(_scriptOutputPath!);
                cts.Cancel();
                return PackageOperationResponse.CreateFailure("Canceled.");
            });

        var exception = Assert.ThrowsAsync<OperationCanceledException>(() =>
            _tool.DetectSDKBreakingChangesAsync(_packagePath, ct: cts.Token));

        Assert.That(exception!.CancellationToken, Is.EqualTo(cts.Token));
        Assert.That(_logger.Logs.Any(log => log.ToString()!.Contains("Could not delete SDK change report")), Is.True);
    }

    [Test, Platform("Win")]
    public async Task CleanupIoFailure_DoesNotReplaceSuccessfulReport()
    {
        ConfigureDetectorReport(false);
        var output = JsonSerializer.Serialize(new SdkChange { SdkChangeMD = Additions, Details = _details });
        FileStream? stream = null;
        _configHelper.Setup(c => c.ExecuteProcessAsync(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>(),
                It.IsAny<PackageInfo?>(), It.IsAny<string>(), It.IsAny<string[]?>()))
            .ReturnsAsync(() =>
            {
                File.WriteAllText(_scriptOutputPath!, output);
                stream = File.Open(_scriptOutputPath!, FileMode.Open, FileAccess.Read, FileShare.Read);
                return new PackageOperationResponse();
            });
        try
        {
            var response = await _tool.DetectSDKBreakingChangesAsync(_packagePath);

            Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Clean));
            Assert.That(GetResult(response).SdkChangeMD, Is.EqualTo(Additions));
            Assert.That(_logger.Logs.Any(log => log.ToString()!.Contains("Could not delete SDK change report")), Is.True);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    [Test]
    public void ClassificationCancellation_IsNotConvertedToFailure()
    {
        ConfigureDetectorReport(true);
        using var cts = new CancellationTokenSource();
        _classifier.Setup(c => c.ClassifySdkBreakingChangesAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), cts.Token))
            .ReturnsAsync(() =>
            {
                cts.Cancel();
                return null;
            });

        Assert.ThrowsAsync<OperationCanceledException>(() => _tool.DetectSDKBreakingChangesAsync(_packagePath, ct: cts.Token));
    }

    [TestCase(SdkBreakingChangeStatus.Clean, "clean")]
    [TestCase(SdkBreakingChangeStatus.Detected, "detected")]
    [TestCase(SdkBreakingChangeStatus.Classified, "classified")]
    [TestCase(SdkBreakingChangeStatus.Inconclusive, "inconclusive")]
    [TestCase(SdkBreakingChangeStatus.Blocked, "blocked")]
    [TestCase(SdkBreakingChangeStatus.Failed, "failed")]
    public void Status_IsAdditiveMachineReadableOutcome(SdkBreakingChangeStatus status, string expected)
    {
        var response = new PackageOperationResponse { BreakingChangeStatus = status };

        Assert.That(JsonSerializer.SerializeToElement(response).GetProperty("breaking_change_status").GetString(), Is.EqualTo(expected));
        Assert.That(JsonSerializer.SerializeToElement(new PackageOperationResponse()).TryGetProperty("breaking_change_status", out _), Is.False);
    }

    private void ConfigureClassification(SdkBreakingChangeMitigation? mitigation)
    {
        _classifier.Setup(c => c.ClassifySdkBreakingChangesAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SdkBreakingChangeDetectionResult
            {
                HasBreakingChange = true,
                BreakingChanges = [new SdkBreakingChange { BreakingChange = "Widget removed", Category = SdkBreakingChangeCategory.Unknown, Mitigation = mitigation }],
            });
    }

    private void ConfigureDetectorReport(bool hasBreakingChange)
    {
        _details = new SdkChangeDetails
        {
            BaselineVersion = "1.2.3",
            ApiChanges =
            [
                new DotnetSdkApiChange
                {
                    Kind = hasBreakingChange ? "removed" : "added",
                    Symbol = hasBreakingChange ? "P:Azure.Test.Widget.Name" : "P:Azure.Test.Widget.DisplayName",
                    Description = hasBreakingChange ? "Property removed" : "Property added",
                    DiagnosticId = hasBreakingChange ? "CP0002" : null,
                    TargetFramework = "netstandard2.0",
                    IsBreaking = hasBreakingChange,
                    AdditionalProperties = new Dictionary<string, JsonElement>
                    {
                        ["signature"] = JsonSerializer.SerializeToElement("Original native signature"),
                    },
                },
            ],
            Diagnostics = hasBreakingChange ? ["CP0002: Widget.Name was removed"] : [],
            Limitations = ["Potential renames require confirmation."],
            AdditionalProperties = new Dictionary<string, JsonElement>
            {
                ["baselineSource"] = JsonSerializer.SerializeToElement(new { feed = "NuGet" }),
            },
        };
        ConfigureScript(JsonSerializer.Serialize(new SdkChange
            {
                HasBreakingChange = hasBreakingChange,
                SdkChangeMD = hasBreakingChange ? BreakingChanges : Additions,
                Details = _details,
            }));
    }

    private void ConfigureScript(string? output, string? error = null, bool singleError = false,
        SpecGenSdkConfigContentType contentType = SpecGenSdkConfigContentType.ScriptPath)
    {
        var configValue = contentType == SpecGenSdkConfigContentType.Command
            ? "pwsh detect.ps1 -SdkRepoPath {SdkRepoPath} -PackagePath {PackagePath} -OutputJsonFile {OutputJsonFile}"
            : "eng/scripts/Get-SdkChanges.ps1";
        _configHelper.Setup(c => c.GetConfigurationAsync(_tempDirectory.DirectoryPath, SpecGenSdkConfigType.GetSdkChanges, It.IsAny<CancellationToken>()))
            .ReturnsAsync((contentType, configValue));
        _configHelper.Setup(c => c.CreateProcessOptions(
                contentType, configValue,
                _tempDirectory.DirectoryPath, _packagePath, It.IsAny<Dictionary<string, string>>(), 5))
            .Callback<SpecGenSdkConfigContentType, string, string, string, Dictionary<string, string>, int>(
                (_, _, _, _, parameters, _) =>
                {
                    Assert.That(parameters["SdkRepoPath"], Is.EqualTo(_tempDirectory.DirectoryPath));
                    Assert.That(parameters["PackagePath"], Is.EqualTo(_packagePath));
                    _scriptOutputPath = parameters["OutputJsonFile"];
                })
            .Returns(new ProcessOptions("pwsh", []));
        _configHelper.Setup(c => c.ExecuteProcessAsync(
                It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>(), It.IsAny<PackageInfo?>(),
                It.IsAny<string>(), It.IsAny<string[]?>()))
            .ReturnsAsync(() =>
            {
                if (output != null)
                {
                    File.WriteAllText(_scriptOutputPath!, output);
                }
                return error == null
                    ? new PackageOperationResponse()
                    : singleError ? new PackageOperationResponse { ResponseError = error } : PackageOperationResponse.CreateFailure(error);
            });
    }

    private static SdkBreakingChangeDetectionResult GetResult(PackageOperationResponse response)
    {
        Assert.That(response.Result, Is.TypeOf<SdkBreakingChangeDetectionResult>());
        return (SdkBreakingChangeDetectionResult)response.Result!;
    }
}
