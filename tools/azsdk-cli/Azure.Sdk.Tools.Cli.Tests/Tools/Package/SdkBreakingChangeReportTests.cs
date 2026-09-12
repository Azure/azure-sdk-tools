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
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class SdkBreakingChangeReportTests
{
    private TempDirectory _directory = null!;
    private string _reportPath = null!;
    private SdkBreakingChangeDetectTool _tool = null!;
    private Mock<LanguageService> _language = null!;
    private Mock<IGitHelper> _git = null!;
    private Mock<ISpecGenSdkConfigHelper> _config = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = TempDirectory.Create("sdk-change-report");
        _reportPath = Path.Combine(_directory.DirectoryPath, "changes.json");
        _language = new Mock<LanguageService> { CallBase = true };
        _language.SetupGet(s => s.Language).Returns(SdkLanguage.Go);
        _language.Setup(s => s.GetPackageInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageInfo { PackageName = "test", Language = SdkLanguage.Go });
        _language.Setup(s => s.GetSdkBreakingPattern(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Patterns");
        _git = new Mock<IGitHelper>();
        _git.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-go");
        _git.Setup(g => g.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_directory.DirectoryPath);
        _config = new Mock<ISpecGenSdkConfigHelper>(MockBehavior.Strict);
        var classifier = new Mock<ISdkBreakingChangeClassificationService>();
        classifier.Setup(c => c.ClassifySdkBreakingChangesAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SdkBreakingChangeDetectionResult
            {
                HasBreakingChange = true,
                BreakingChanges = [new SdkBreakingChange { BreakingChange = "Widget removed", Category = SdkBreakingChangeCategory.Unknown }],
            });
        _tool = new SdkBreakingChangeDetectTool(_git.Object, new TestLogger<SdkBreakingChangeDetectTool>(),
            [_language.Object], _config.Object, classifier.Object);
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [TestCase("not json")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"changes\":\"text\"}")]
    [TestCase("{\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":\"text\",\"hasBreakingChange\":null}")]
    [TestCase("{\"changes\":\"text\",\"hasBreakingChange\":\"false\"}")]
    [TestCase("{\"changes\":\"text\",\"hasBreakingChange\":0}")]
    [TestCase("{\"changes\":42,\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":[],\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":\"text\",\"hasBreakingChange\":false,\"details\":[]}")]
    public async Task MalformedReport_IsFailureNotCleanOrUnsupported(string report)
    {
        await File.WriteAllTextAsync(_reportPath, report);

        var response = await ReadReportAsync();

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
        Assert.That(response.OperationStatus, Is.EqualTo(Status.Failed));
        _config.VerifyNoOtherCalls();
        _language.Verify(s => s.DetectSdkBreakingChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("  \r\n\t", false)]
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("  \r\n\t", true)]
    public async Task EmptyMarkdown_IsRejectedRegardlessOfBreakingFlag(string? changes, bool breaking)
    {
        await File.WriteAllTextAsync(_reportPath, JsonSerializer.Serialize(new { changes, hasBreakingChange = breaking }));

        var response = await ReadReportAsync();

        Assert.That(response.ResponseErrors.Single(), Does.Contain("changes (Markdown)").And.Not.Contain("details"));
        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
    }

    [TestCase("No SDK breaking changes detected.", false)]
    [TestCase("### Features Added\n- Widget", false)]
    [TestCase("### Breaking Changes\n- Widget removed", true)]
    public async Task ValidLegacyReport_DoesNotRequireOptionalDetails(string changes, bool breaking)
    {
        await File.WriteAllTextAsync(_reportPath, JsonSerializer.Serialize(new { changes, hasBreakingChange = breaking }));

        var response = await ReadReportAsync();
        var result = (SdkBreakingChangeDetectionResult)response.Result!;

        Assert.That(response.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.SdkChangeMD, Is.EqualTo(changes));
        Assert.That(result.HasBreakingChange, Is.EqualTo(breaking));
        Assert.That(result.Details, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task DotnetNoBaselineReport_PreservesLimitationsWithoutInventingProvenance(string? baseline)
    {
        UseDotnet();
        await File.WriteAllTextAsync(_reportPath, JsonSerializer.Serialize(new
        {
            changes = "No released baseline.",
            hasBreakingChange = false,
            details = new { baselineVersion = baseline, limitations = new[] { "Compatibility not evaluated." } },
        }));

        var response = await ReadReportAsync();
        var details = JsonSerializer.SerializeToElement(((SdkBreakingChangeDetectionResult)response.Result!).Details);

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Inconclusive));
        Assert.That(details.GetProperty("baselineVersion").GetString(), Is.EqualTo(baseline));
        Assert.That(details.GetProperty("limitations")[0].GetString(), Is.EqualTo("Compatibility not evaluated."));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OtherLanguageMetadata_RoundTripsWithoutDotnetSchema(bool breaking)
    {
        const string metadata = """
            {"baselineVersion":{"release":"tag-v1"},"apiChanges":{"format":"language-specific"},
             "diagnostics":42,"limitations":{"scope":"symbols"},"extra":[true,null,{"key":"value"}]}
            """;
        await File.WriteAllTextAsync(_reportPath, $$"""
            {"changes":"Original evidence","hasBreakingChange":{{breaking.ToString().ToLowerInvariant()}},"details":{{metadata}}}
            """);

        var response = await ReadReportAsync();
        var result = (SdkBreakingChangeDetectionResult)response.Result!;

        Assert.That(response.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Details, Is.TypeOf<SdkChangeDetails>());
        Assert.That(JsonSerializer.Serialize(result.Details), Is.EqualTo(JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(metadata))));
        Assert.That(JsonSerializer.Serialize(result.Details), Does.Not.Contain("$type"));
    }

    [Test]
    public async Task NativeEvidence_RoundTripsUnknownProvenanceAndApiFields()
    {
        UseDotnet();
        await File.WriteAllTextAsync(_reportPath, """
            {"changes":"### Features Added\n- Widget","hasBreakingChange":false,
             "details":{"baselineVersion":"1.2.3","baselineSource":{"feed":"NuGet"},"detectorVersion":2,
               "apiChanges":[{"kind":"added","symbol":"Widget","description":"Added","isBreaking":false,
                 "targetFramework":"net8.0","newSignature":"class Widget","oldSignature":null}],
               "diagnostics":[],"limitations":["Behavior not compared"]}}
            """);

        var response = await ReadReportAsync();
        var details = JsonSerializer.SerializeToElement(((SdkBreakingChangeDetectionResult)response.Result!).Details);
        var dotnet = details.Deserialize<DotnetSdkChangeDetails>()!;

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Clean));
        Assert.That(dotnet.ApiChanges.Single(), Is.TypeOf<DotnetSdkApiChange>());
        Assert.That(details.GetProperty("baselineSource").GetProperty("feed").GetString(), Is.EqualTo("NuGet"));
        Assert.That(details.GetProperty("detectorVersion").GetInt32(), Is.EqualTo(2));
        Assert.That(details.GetProperty("apiChanges")[0].GetProperty("newSignature").GetString(), Is.EqualTo("class Widget"));
        Assert.That(details.GetProperty("apiChanges")[0].GetProperty("oldSignature").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task DotnetMetadata_IsValidatedOnlyInDotnetContext()
    {
        UseDotnet();
        await File.WriteAllTextAsync(_reportPath, """
            {"changes":"Evidence","hasBreakingChange":false,"details":{"baselineVersion":42}}
            """);

        Assert.That((await ReadReportAsync()).BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
    }

    [Test]
    public void Cancellation_Propagates()
    {
        File.WriteAllText(_reportPath, """{"changes":"No changes","hasBreakingChange":false}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => ReadReportAsync(cts.Token));
    }

    private void UseDotnet()
    {
        _language.SetupGet(s => s.Language).Returns(SdkLanguage.DotNet);
        _git.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-net");
    }

    private Task<PackageOperationResponse> ReadReportAsync(CancellationToken ct = default) =>
        _tool.DetectSDKBreakingChangesAsync(_directory.DirectoryPath, localSdkChangeJsonFilePath: _reportPath, ct: ct);
}
