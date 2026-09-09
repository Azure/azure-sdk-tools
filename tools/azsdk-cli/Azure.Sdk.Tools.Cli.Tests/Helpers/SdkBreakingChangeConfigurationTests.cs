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

        Assert.That(await _helper.GetConfigurationAsync(_directory.DirectoryPath, SpecGenSdkConfigType.GetSdkChanges, CancellationToken.None),
            Is.EqualTo((SpecGenSdkConfigContentType.Unknown, string.Empty)));
        Assert.That(await _helper.GetSdkBreakingChangePatternFileConfigurationAsync(_directory.DirectoryPath, CancellationToken.None),
            Is.Empty);
    }

    [TestCase("{\"command\":\"detect\"}", SpecGenSdkConfigContentType.Command, "detect")]
    [TestCase("{\"path\":\"detect.ps1\"}", SpecGenSdkConfigContentType.ScriptPath, "detect.ps1")]
    [TestCase("{\"command\":\"detect\",\"path\":\"detect.ps1\"}", SpecGenSdkConfigContentType.Command, "detect")]
    [TestCase("{\"command\":\"generator sdkchange {packagePath} {outputJsonFile}\",\"path\":\"\"}",
        SpecGenSdkConfigContentType.Command, "generator sdkchange {packagePath} {outputJsonFile}")]
    [TestCase("{\"command\":\"detect\",\"path\":\"\"}", SpecGenSdkConfigContentType.Command, "detect")]
    [TestCase("{\"command\":\"detect\",\"path\":\" \\t\\n\"}", SpecGenSdkConfigContentType.Command, "detect")]
    [TestCase("{\"command\":\"detect\",\"path\":null}", SpecGenSdkConfigContentType.Command, "detect")]
    [TestCase("{\"command\":\"detect\",\"path\":42}", SpecGenSdkConfigContentType.Command, "detect")]
    [TestCase("{\"command\":\"detect\",\"path\":[]}", SpecGenSdkConfigContentType.Command, "detect")]
    [TestCase("{\"command\":\"\",\"path\":\"detect.ps1\"}", SpecGenSdkConfigContentType.ScriptPath, "detect.ps1")]
    [TestCase("{\"command\":\" \\t\\n\",\"path\":\"detect.ps1\"}", SpecGenSdkConfigContentType.ScriptPath, "detect.ps1")]
    public async Task ValidDetectorConfiguration_UsesSharedCommandOrPath(string script, SpecGenSdkConfigContentType type, string value)
    {
        await WriteScriptAsync(script);

        Assert.That(await _helper.GetConfigurationAsync(_directory.DirectoryPath, SpecGenSdkConfigType.GetSdkChanges, CancellationToken.None),
            Is.EqualTo((type, value)));
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("\"detect\"")]
    [TestCase("{\"command\":null}")]
    [TestCase("{\"command\":42}")]
    [TestCase("{\"path\":null}")]
    [TestCase("{\"path\":false}")]
    [TestCase("{\"command\":null,\"path\":\"valid.ps1\"}")]
    [TestCase("{\"command\":42,\"path\":\"valid.ps1\"}")]
    [TestCase("{\"command\":{},\"path\":\"valid.ps1\"}")]
    [TestCase("{\"command\":\"\",\"path\":null}")]
    [TestCase("{\"command\":\" \\t\",\"path\":42}")]
    public async Task InvalidConfiguredValue_NeverFallsBack(string script)
    {
        await WriteScriptAsync(script);

        Assert.ThrowsAsync<JsonException>(() => ReadDetectorAsync());
    }

    [TestCase("{}")]
    [TestCase("{\"command\":\"\"}")]
    [TestCase("{\"command\":\" \\t\"}")]
    [TestCase("{\"path\":\"\"}")]
    [TestCase("{\"path\":\" \\t\"}")]
    [TestCase("{\"command\":\"\",\"path\":\"\"}")]
    [TestCase("{\"command\":\" \\t\",\"path\":\" \\n\"}")]
    public async Task PresentScriptWithoutUsableCommandOrPath_FailsInsteadOfReturningUnsupported(string script)
    {
        await WriteScriptAsync(script);

        var exception = Assert.ThrowsAsync<JsonException>(() => ReadDetectorAsync());

        Assert.That(exception!.Message, Is.EqualTo("getSdkChangesScript must contain a nonempty command or path."));
    }

    [TestCase("not json")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{\"packageOptions\":null}")]
    [TestCase("{\"packageOptions\":false}")]
    public void MalformedConfiguration_NeverLooksAbsent(string config)
    {
        File.WriteAllText(_configPath, config);

        Assert.CatchAsync<JsonException>(() => ReadDetectorAsync());
        Assert.CatchAsync<JsonException>(() =>
            _helper.GetSdkBreakingChangePatternFileConfigurationAsync(_directory.DirectoryPath, CancellationToken.None));
    }

    [Test]
    public void MissingFile_PropagatesReadFailure()
    {
        Assert.ThrowsAsync<FileNotFoundException>(() => ReadDetectorAsync());
    }

    [Test]
    public void UnreadableFile_PropagatesAccessFailure()
    {
        Directory.CreateDirectory(_configPath);

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => ReadDetectorAsync());
    }

    [Test]
    public void LockedFile_PropagatesIoFailure()
    {
        File.WriteAllText(_configPath, "{}");
        using var stream = File.Open(_configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.ThrowsAsync<IOException>(() => ReadDetectorAsync());
        Assert.ThrowsAsync<IOException>(() =>
            _helper.GetSdkBreakingChangePatternFileConfigurationAsync(_directory.DirectoryPath, CancellationToken.None));
    }

    [Test]
    public void Cancellation_IsNeverAbsence()
    {
        File.WriteAllText(_configPath, "{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _helper.GetConfigurationAsync(_directory.DirectoryPath, SpecGenSdkConfigType.GetSdkChanges, cts.Token));
    }

    private Task WriteScriptAsync(string script) =>
        File.WriteAllTextAsync(_configPath, """{"packageOptions":{"getSdkChangesScript":""" + script + "}}");

    private Task<(SpecGenSdkConfigContentType, string)> ReadDetectorAsync() =>
        _helper.GetConfigurationAsync(_directory.DirectoryPath, SpecGenSdkConfigType.GetSdkChanges, CancellationToken.None);
}
