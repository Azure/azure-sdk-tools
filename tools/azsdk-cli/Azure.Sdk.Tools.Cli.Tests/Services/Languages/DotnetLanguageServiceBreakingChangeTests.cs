// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class DotnetLanguageServiceBreakingChangeTests
{
    private TempDirectory _directory = null!;
    private DotnetLanguageService _service = null!;
    private string _configPath = null!;
    private string _defaultCatalogPath = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = TempDirectory.Create("dotnet-breaking-change-tests");
        _configPath = Path.Combine(_directory.DirectoryPath, "eng", "swagger_to_sdk_config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllText(_configPath, "{}");
        _defaultCatalogPath = Path.Combine(_directory.DirectoryPath, "doc", "dev", "SDKBreakingChanges.md");
        Directory.CreateDirectory(Path.GetDirectoryName(_defaultCatalogPath)!);
        File.WriteAllText(_defaultCatalogPath, "Default .NET patterns");
        _service = CreateService(new SpecGenSdkConfigHelper(
            new TestLogger<SpecGenSdkConfigHelper>(), Mock.Of<IProcessHelper>()));
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [TestCase("{}")]
    [TestCase("{\"packageOptions\":{}}")]
    [TestCase("{\"packageOptions\":{\"buildScript\":{\"command\":\"build\"}}}")]
    public async Task PatternCatalog_DefaultsOnlyForAbsentProperty(string config)
    {
        await File.WriteAllTextAsync(_configPath, config);

        Assert.That(await _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None),
            Is.EqualTo("Default .NET patterns"));
    }

    [Test]
    public async Task PatternCatalog_UsesConfiguredFileInsteadOfDefault()
    {
        await File.WriteAllTextAsync(_configPath, """{"packageOptions":{"sdkBreakingChangePatternFile":"custom.md"}}""");
        await File.WriteAllTextAsync(Path.Combine(_directory.DirectoryPath, "custom.md"), "Configured patterns");

        Assert.That(await _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None),
            Is.EqualTo("Configured patterns"));
    }

    [TestCase("not json")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{\"packageOptions\":null}")]
    [TestCase("{\"packageOptions\":42}")]
    [TestCase("{\"packageOptions\":{\"sdkBreakingChangePatternFile\":null}}")]
    [TestCase("{\"packageOptions\":{\"sdkBreakingChangePatternFile\":\"\"}}")]
    [TestCase("{\"packageOptions\":{\"sdkBreakingChangePatternFile\":\"  \"}}")]
    [TestCase("{\"packageOptions\":{\"sdkBreakingChangePatternFile\":42}}")]
    public void PatternCatalog_InvalidConfigurationNeverUsesDefault(string config)
    {
        File.WriteAllText(_configPath, config);

        Assert.CatchAsync<JsonException>(() =>
            _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None));
    }

    [Test]
    public void PatternCatalog_MissingConfigurationNeverUsesDefault()
    {
        File.Delete(_configPath);

        Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None));
    }

    [Test]
    public void PatternCatalog_UnreadableConfigurationNeverUsesDefault()
    {
        File.Delete(_configPath);
        Directory.CreateDirectory(_configPath);

        Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PatternCatalog_MissingCatalogIsExplicitFailure(bool configured)
    {
        if (configured)
        {
            File.WriteAllText(_configPath, """{"packageOptions":{"sdkBreakingChangePatternFile":"missing.md"}}""");
        }
        else
        {
            File.Delete(_defaultCatalogPath);
        }

        Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None));
    }

    [Test]
    public void PatternCatalog_EmptyCatalogIsExplicitFailure()
    {
        File.WriteAllText(_defaultCatalogPath, "  \n");

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None));
    }

    [Test]
    public void PatternCatalog_CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _service.GetSdkBreakingPattern(_directory.DirectoryPath, cts.Token));
    }

    [TestCase(SdkBreakingChangeMitigation.Generator)]
    [TestCase(SdkBreakingChangeMitigation.ClientCustomization)]
    [TestCase(SdkBreakingChangeMitigation.Manual)]
    public void Classification_AcceptsEverySupportedRoute(SdkBreakingChangeMitigation route)
    {
        Assert.That(_service.ValidateBreakingChangeClassification(Classification(route)), Is.Null);
    }

    [TestCase(null)]
    [TestCase((SdkBreakingChangeMitigation)999)]
    public void Classification_RejectsMissingOrUndefinedRoute(SdkBreakingChangeMitigation? route)
    {
        Assert.That(_service.ValidateBreakingChangeClassification(Classification(route)),
            Does.Contain("supported mitigation route"));
    }

    [TestCase("false")]
    [TestCase("empty")]
    [TestCase("null-list")]
    [TestCase("null-entry")]
    [TestCase("mixed-routes")]
    public void Classification_RejectsIncompleteResults(string scenario)
    {
        var result = Classification(SdkBreakingChangeMitigation.Manual);
        switch (scenario)
        {
            case "false": result.HasBreakingChange = false; break;
            case "empty": result.BreakingChanges = []; break;
            case "null-list": result.BreakingChanges = null!; break;
            case "null-entry": result.BreakingChanges.Add(null!); break;
            case "mixed-routes": result.BreakingChanges.Add(new SdkBreakingChange()); break;
        }

        Assert.That(_service.ValidateBreakingChangeClassification(result), Is.Not.Null);
    }

    [Test]
    public void BaseLanguageClassification_DoesNotRequireDotnetRoute()
    {
        var otherLanguage = new Mock<LanguageService> { CallBase = true };

        Assert.That(otherLanguage.Object.ValidateBreakingChangeClassification(Classification(null)), Is.Null);
    }

    private static SdkBreakingChangeDetectionResult Classification(SdkBreakingChangeMitigation? route) => new()
    {
        HasBreakingChange = true,
        BreakingChanges =
        [
            new SdkBreakingChange { BreakingChange = "Widget removed", Category = SdkBreakingChangeCategory.Unknown, Mitigation = route },
        ],
    };

    internal static DotnetLanguageService CreateService(ISpecGenSdkConfigHelper configHelper) => new(
        Mock.Of<IProcessHelper>(), Mock.Of<IPowershellHelper>(), Mock.Of<ICopilotAgentRunner>(),
        Mock.Of<IGitHelper>(), new TestLogger<DotnetLanguageService>(),
        Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
        configHelper, Mock.Of<IChangelogHelper>());
}
