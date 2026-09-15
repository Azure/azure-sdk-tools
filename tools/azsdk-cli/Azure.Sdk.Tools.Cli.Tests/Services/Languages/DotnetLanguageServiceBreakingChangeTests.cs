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
public class DotnetLanguageServiceBreakingChangeTests
{
    private TempDirectory _directory = null!;
    private DotnetLanguageService _service = null!;
    private string _configPath = null!;
    private string _defaultCatalogPath = null!;
    private TestLogger<DotnetLanguageService> _logger = null!;

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
        _logger = new TestLogger<DotnetLanguageService>();
        _service = CreateService(new SpecGenSdkConfigHelper(
            new TestLogger<SpecGenSdkConfigHelper>(), Mock.Of<IProcessHelper>()), _logger);
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [TestCase("{}")]
    [TestCase("{\"packageOptions\":{}}")]
    [TestCase("{\"packageOptions\":{\"buildScript\":{\"command\":\"build\"}}}")]
    public async Task PatternCatalog_AbsentPropertyDoesNotSelectDotnetDefault(string config)
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

    [Test]
    public void BaseLanguageClassification_DoesNotRequireDotnetRoute()
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
        BreakingChanges =
        [
            new SdkBreakingChange { BreakingChange = "Widget removed", Category = SdkBreakingChangeCategory.Unknown, MitigationStrategy = route },
        ],
    };

    internal static DotnetLanguageService CreateService(ISpecGenSdkConfigHelper configHelper, TestLogger<DotnetLanguageService>? logger = null) => new(
        Mock.Of<IProcessHelper>(), Mock.Of<IPowershellHelper>(), Mock.Of<ICopilotAgentRunner>(),
        Mock.Of<IGitHelper>(), logger ?? new TestLogger<DotnetLanguageService>(),
        Mock.Of<ICommonValidationHelpers>(), Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(),
        configHelper, Mock.Of<IChangelogHelper>());
}
