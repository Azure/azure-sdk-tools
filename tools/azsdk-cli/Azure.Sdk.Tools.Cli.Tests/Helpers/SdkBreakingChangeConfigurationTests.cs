// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class SdkBreakingChangeConfigurationTests
{
    private TempDirectory _directory = null!;
    private SpecGenSdkConfigHelper _helper = null!;
    private string _configPath = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = TempDirectory.Create("breaking-change-config");
        _configPath = Path.Combine(_directory.DirectoryPath, "eng", "swagger_to_sdk_config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        _helper = new SpecGenSdkConfigHelper(new TestLogger<SpecGenSdkConfigHelper>(), Mock.Of<IProcessHelper>());
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [TestCase("{}")]
    [TestCase("{\"packageOptions\":{}}")]
    public async Task AbsentSettings_AreDistinguishedFromErrors(string config)
    {
        await File.WriteAllTextAsync(_configPath, config);

        Assert.That(await _helper.GetSdkBreakingChangePatternFileConfigurationAsync(_directory.DirectoryPath, CancellationToken.None),
            Is.Empty);
    }

    [TestCase("eng/sdk-breaking-change-patterns.json")]
    [TestCase("eng/custom patterns.json")]
    public async Task ConfiguredPatternFile_UsesSharedPropertyLookup(string path)
    {
        await WritePatternFileAsync(JsonSerializer.Serialize(path));

        Assert.That(await ReadPatternFileAsync(), Is.EqualTo(path));
        Assert.That(await _helper.GetConfigValueFromRepoAsync<string>(
            _directory.DirectoryPath, "packageOptions/sdkBreakingChangePatternFile", CancellationToken.None), Is.EqualTo(path));
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("42")]
    [TestCase("false")]
    [TestCase("\"\"")]
    [TestCase("\" \\t\\n\"")]
    public async Task InvalidConfiguredPatternFile_NeverLooksAbsent(string value)
    {
        await WritePatternFileAsync(value);

        var exception = Assert.ThrowsAsync<JsonException>(() => ReadPatternFileAsync());

        Assert.That(exception!.Message, Does.Contain("packageOptions/sdkBreakingChangePatternFile"));
    }

    [TestCase("""{"packageOptions":{"getSdkChangesScript":null}}""", "")]
    [TestCase("""{"packageOptions":{"getSdkChangesScript":{},"sdkBreakingChangePatternFile":"patterns.json"}}""", "patterns.json")]
    public async Task UnusedMalformedScript_DoesNotAffectPatternFile(string config, string expected)
    {
        await File.WriteAllTextAsync(_configPath, config);

        Assert.That(await ReadPatternFileAsync(), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("not json")]
    [TestCase("{")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("\"config\"")]
    [TestCase("42")]
    [TestCase("false")]
    [TestCase("{\"packageOptions\":null}")]
    [TestCase("{\"packageOptions\":false}")]
    [TestCase("{\"packageOptions\":42}")]
    [TestCase("{\"packageOptions\":\"options\"}")]
    [TestCase("{\"packageOptions\":[]}")]
    public void MalformedConfiguration_NeverLooksAbsent(string config)
    {
        File.WriteAllText(_configPath, config);

        Assert.CatchAsync<JsonException>(() => ReadPatternFileAsync());
    }

    [Test]
    public void MissingFile_PropagatesReadFailure()
    {
        Assert.ThrowsAsync<FileNotFoundException>(() => ReadPatternFileAsync());
    }

    [Test]
    public void UnreadableFile_PropagatesAccessFailure()
    {
        Directory.CreateDirectory(_configPath);

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => ReadPatternFileAsync());
    }

    [Test]
    public void LockedFile_PropagatesIoFailure()
    {
        File.WriteAllText(_configPath, "{}");
        using var stream = File.Open(_configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.ThrowsAsync<IOException>(() => ReadPatternFileAsync());
    }

    [Test]
    public void Cancellation_IsNeverAbsence()
    {
        File.WriteAllText(_configPath, "{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _helper.GetSdkBreakingChangePatternFileConfigurationAsync(_directory.DirectoryPath, cts.Token));
    }

    private Task WritePatternFileAsync(string value) =>
        File.WriteAllTextAsync(_configPath, """{"packageOptions":{"sdkBreakingChangePatternFile":""" + value + "}}");

    private Task<string> ReadPatternFileAsync() =>
        _helper.GetSdkBreakingChangePatternFileConfigurationAsync(_directory.DirectoryPath, CancellationToken.None);
}
