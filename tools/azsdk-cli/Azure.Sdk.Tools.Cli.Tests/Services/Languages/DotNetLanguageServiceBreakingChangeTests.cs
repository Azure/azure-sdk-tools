// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class DotNetLanguageServiceBreakingChangeTests
{
    private const string FirstBreak = "[CP0002] Member 'Azure.Test.Widget.Name' was removed. (target framework: netstandard2.0)";
    private const string SecondBreak = "[CP0002] Member 'Azure.Test.Widget.Name' was removed. (target framework: net8.0)";
    private TempDirectory _directory = null!;
    private DotNetLanguageService _service = null!;
    private string _configPath = null!;
    private string _defaultCatalogPath = null!;
    private TestLogger<DotNetLanguageService> _logger = null!;

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
        _logger = new TestLogger<DotNetLanguageService>();
        _service = CreateService(new SpecGenSdkConfigHelper(
            new TestLogger<SpecGenSdkConfigHelper>(), Mock.Of<IProcessHelper>()), _logger);
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [TestCase("{}")]
    [TestCase("{\"packageOptions\":{}}")]
    [TestCase("{\"packageOptions\":{\"buildScript\":{\"command\":\"build\"}}}")]
    public async Task PatternCatalog_AbsentPropertyDoesNotSelectDotNetDefault(string config)
    {
        await File.WriteAllTextAsync(_configPath, config);

        Assert.That(await _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None),
            Is.Empty);
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
    public async Task PatternCatalog_InvalidConfigurationIsLoggedAndNeverUsesDefault(string config)
    {
        File.WriteAllText(_configPath, config);

        await AssertCatalogReadFailureAsync();
    }

    [Test]
    public async Task PatternCatalog_MissingConfigurationIsLoggedAndNeverUsesDefault()
    {
        File.Delete(_configPath);

        await AssertCatalogReadFailureAsync();
    }

    [Test]
    public async Task PatternCatalog_UnreadableConfigurationIsLoggedAndNeverUsesDefault()
    {
        File.Delete(_configPath);
        Directory.CreateDirectory(_configPath);

        await AssertCatalogReadFailureAsync();
    }

    [Test]
    public async Task PatternCatalog_MissingCatalogIsLoggedAndReturnsEmpty()
    {
        File.WriteAllText(_configPath, """{"packageOptions":{"sdkBreakingChangePatternFile":"missing.md"}}""");

        await AssertCatalogReadFailureAsync();
    }

    [Test]
    public async Task PatternCatalog_EmptyCatalogIsLoggedAndReturnsEmpty()
    {
        File.WriteAllText(_configPath, """{"packageOptions":{"sdkBreakingChangePatternFile":"doc/dev/SDKBreakingChanges.md"}}""");
        File.WriteAllText(_defaultCatalogPath, "  \n");

        Assert.That(await _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None), Is.Empty);
        Assert.That(_logger.Logs.Any(log => log.ToString()!.Contains("catalog is empty")), Is.True);
    }

    [Test, Platform("Win")]
    public async Task PatternCatalog_LockedCatalogIsLoggedAndReturnsEmpty()
    {
        File.WriteAllText(_configPath, """{"packageOptions":{"sdkBreakingChangePatternFile":"doc/dev/SDKBreakingChanges.md"}}""");
        using var stream = File.Open(_defaultCatalogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await AssertCatalogReadFailureAsync();
    }

    [Test]
    public async Task PatternCatalog_UnreadableCatalogIsLoggedAndReturnsEmpty()
    {
        File.WriteAllText(_configPath, """{"packageOptions":{"sdkBreakingChangePatternFile":"unreadable"}}""");
        Directory.CreateDirectory(Path.Combine(_directory.DirectoryPath, "unreadable"));

        await AssertCatalogReadFailureAsync();
    }

    [Test]
    public void PatternCatalog_CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _service.GetSdkBreakingPattern(_directory.DirectoryPath, cts.Token));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PatternCatalog_CancellationDuringConfigurationReadIsNotAnEmptyCatalog(bool throwsReadError)
    {
        using var cts = new CancellationTokenSource();
        var config = new Mock<ISpecGenSdkConfigHelper>();
        config.Setup(c => c.GetSdkBreakingChangePatternFileConfigurationAsync(_directory.DirectoryPath, cts.Token))
            .ReturnsAsync(() =>
            {
                cts.Cancel();
                if (throwsReadError)
                {
                    throw new IOException("Read was interrupted.");
                }
                return string.Empty;
            });
        var service = CreateService(config.Object);

        Assert.CatchAsync<OperationCanceledException>(() =>
            service.GetSdkBreakingPattern(_directory.DirectoryPath, cts.Token));
    }

    [Test]
    public void PatternCatalog_UnexpectedErrorsAreNotSwallowed()
    {
        var config = new Mock<ISpecGenSdkConfigHelper>();
        config.Setup(c => c.GetSdkBreakingChangePatternFileConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotImplementedException("Unexpected implementation failure."));
        var service = CreateService(config.Object);

        Assert.ThrowsAsync<NotImplementedException>(() =>
            service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None));
    }

    [Test]
    public async Task PatternCatalog_CommonLanguageServiceUsesTheSameConfiguration()
    {
        await File.WriteAllTextAsync(_configPath, """{"packageOptions":{"sdkBreakingChangePatternFile":"custom.md"}}""");
        await File.WriteAllTextAsync(Path.Combine(_directory.DirectoryPath, "custom.md"), "Language-owned patterns");
        var configHelper = new SpecGenSdkConfigHelper(new TestLogger<SpecGenSdkConfigHelper>(), Mock.Of<IProcessHelper>());
        var service = new Mock<LanguageService>(
            Mock.Of<IProcessHelper>(), Mock.Of<IGitHelper>(), new TestLogger<LanguageService>(),
            Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
            configHelper, Mock.Of<IChangelogHelper>()) { CallBase = true };

        Assert.That(await service.Object.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None),
            Is.EqualTo(await _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None)));
    }

    [TestCase(SdkBreakingChangeMitigationStrategy.Generator)]
    [TestCase(SdkBreakingChangeMitigationStrategy.ClientCustomization)]
    [TestCase(SdkBreakingChangeMitigationStrategy.Manual)]
    public void Classification_AcceptsEverySupportedRoute(SdkBreakingChangeMitigationStrategy route)
    {
        Assert.That(_service.ValidateBreakingChangeClassification(Classification(route)), Is.Null);
    }

    [TestCase(null)]
    [TestCase((SdkBreakingChangeMitigationStrategy)999)]
    public void Classification_RejectsMissingOrUndefinedRoute(SdkBreakingChangeMitigationStrategy? route)
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
        var result = Classification(SdkBreakingChangeMitigationStrategy.Manual);
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

    [TestCase(false, "\n")]
    [TestCase(true, "\n")]
    [TestCase(false, "\r\n")]
    [TestCase(true, "\r\n")]
    public void Classification_AcceptsExactCoverageAcrossFrameworksAndGroups(bool merged, string newline)
    {
        var result = Classification(SdkBreakingChangeMitigationStrategy.Manual);
        result.SdkChangeMD = $"### Breaking Changes\n\n- {FirstBreak}\n- {SecondBreak}\n\n### Features Added\n- Widget added"
            .Replace("\n", newline);
        result.BreakingChanges = merged
            ? [ClassifiedChange(SecondBreak, FirstBreak)]
            : [ClassifiedChange(SecondBreak), ClassifiedChange(FirstBreak)];

        Assert.That(_service.ValidateBreakingChangeClassification(result), Is.Null);
    }

    [TestCase("null-list")]
    [TestCase("empty-list")]
    [TestCase("null-entry")]
    [TestCase("blank-entry")]
    [TestCase("missing")]
    [TestCase("duplicate")]
    [TestCase("duplicate-across-groups")]
    [TestCase("fabricated")]
    [TestCase("paraphrased")]
    [TestCase("case-changed")]
    [TestCase("bullet-marker")]
    [TestCase("feature-added")]
    [TestCase("empty-group")]
    public void Classification_RejectsUnfaithfulOriginCoverage(string scenario)
    {
        var result = Classification(SdkBreakingChangeMitigationStrategy.Manual);
        result.SdkChangeMD = $"### Breaking Changes\n- {FirstBreak}\n- {SecondBreak}\n\n### Features Added\n- Widget added";
        var change = ClassifiedChange(FirstBreak, SecondBreak);
        result.BreakingChanges = [change];
        switch (scenario)
        {
            case "null-list": change.OriginBreaks = null; break;
            case "empty-list": change.OriginBreaks = []; break;
            case "null-entry": change.OriginBreaks = [null!, SecondBreak]; break;
            case "blank-entry": change.OriginBreaks = [" ", SecondBreak]; break;
            case "missing": change.OriginBreaks = [FirstBreak]; break;
            case "duplicate": change.OriginBreaks = [FirstBreak, FirstBreak]; break;
            case "duplicate-across-groups": result.BreakingChanges.Add(ClassifiedChange(FirstBreak)); break;
            case "fabricated": change.OriginBreaks = [FirstBreak, "Another member removed"]; break;
            case "paraphrased": change.OriginBreaks = [FirstBreak, SecondBreak.Replace("was removed.", "removed.")]; break;
            case "case-changed": change.OriginBreaks = [FirstBreak, SecondBreak.ToUpperInvariant()]; break;
            case "bullet-marker": change.OriginBreaks = ["- " + FirstBreak, SecondBreak]; break;
            case "feature-added": change.OriginBreaks = [FirstBreak, SecondBreak, "Widget added"]; break;
            case "empty-group": result.BreakingChanges.Add(ClassifiedChange()); break;
        }

        Assert.That(_service.ValidateBreakingChangeClassification(result), Does.Contain("originBreaks"));
    }

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    public void Classification_RepeatedNativeEntriesRequireTheExactInputCount(int outputCount, bool valid)
    {
        var result = Classification(SdkBreakingChangeMitigationStrategy.Manual);
        result.SdkChangeMD = $"### Breaking Changes\n- {FirstBreak}\n- {FirstBreak}";
        result.BreakingChanges = [ClassifiedChange(Enumerable.Repeat(FirstBreak, outputCount).ToArray())];

        Assert.That(_service.ValidateBreakingChangeClassification(result), valid ? Is.Null : Is.Not.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("### Breaking Changes\nNone.")]
    [TestCase("### Breaking Changes\n- ")]
    [TestCase("### Breaking Changes\nWidget removed")]
    [TestCase("### Breaking Changes\n1. Widget removed")]
    [TestCase("### Breaking Changes\n- Widget removed\n  with more details")]
    [TestCase("### Breaking Changes\n```\n- Widget removed\n```")]
    [TestCase("### Features Added\n- Widget removed")]
    [TestCase("### Breaking Changes\n- Widget removed\n### Breaking Changes\n- Other removed")]
    public void Classification_RejectsUnverifiableNativeSection(string? markdown)
    {
        var result = Classification(SdkBreakingChangeMitigationStrategy.Manual);
        result.SdkChangeMD = markdown;

        Assert.That(_service.ValidateBreakingChangeClassification(result), Does.Contain("Cannot validate .NET originBreaks"));
    }

    [Test]
    public void BaseLanguageClassification_DoesNotRequireDotNetRoute()
    {
        var otherLanguage = new Mock<LanguageService> { CallBase = true };

        Assert.That(otherLanguage.Object.ValidateBreakingChangeClassification(Classification(null)), Is.Null);
        Assert.That(otherLanguage.Object.RequiresBreakingChangePatternCatalog, Is.False);
        Assert.That(_service.RequiresBreakingChangePatternCatalog, Is.True);
    }

    private async Task AssertCatalogReadFailureAsync()
    {
        Assert.That(await _service.GetSdkBreakingPattern(_directory.DirectoryPath, CancellationToken.None), Is.Empty);
        Assert.That(_logger.Logs.Any(log => log.ToString()!.Contains("Unable to load SDK breaking change patterns")), Is.True);
    }

    private static SdkBreakingChangeDetectionResult Classification(SdkBreakingChangeMitigationStrategy? route) => new()
    {
        HasBreakingChange = true,
        SdkChangeMD = "### Breaking Changes\n- Widget removed",
        BreakingChanges =
        [
            new SdkBreakingChange
            {
                BreakingChange = "Widget removed",
                Category = SdkBreakingChangeCategory.Unknown,
                MitigationStrategy = route,
                OriginBreaks = ["Widget removed"],
            },
        ],
    };

    private static SdkBreakingChange ClassifiedChange(params string[] origins) => new()
    {
        BreakingChange = "Widget removed",
        Category = SdkBreakingChangeCategory.Unknown,
        MitigationStrategy = SdkBreakingChangeMitigationStrategy.Manual,
        OriginBreaks = origins.ToList(),
    };

    internal static DotNetLanguageService CreateService(ISpecGenSdkConfigHelper configHelper, TestLogger<DotNetLanguageService>? logger = null) => new(
        Mock.Of<IProcessHelper>(), Mock.Of<IPowershellHelper>(), Mock.Of<ICopilotAgentRunner>(),
        Mock.Of<IGitHelper>(), logger ?? new TestLogger<DotNetLanguageService>(),
        Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
        configHelper, Mock.Of<IChangelogHelper>());
}
