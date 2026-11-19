// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class GeneratedSdkMetadataHelperTests
{
    private TempDirectory _directory = null!;
    private Mock<ILogger> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = TempDirectory.Create("GeneratedSdkMetadataHelperTests");
        _logger = new Mock<ILogger>();
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [TestCase(SdkLanguage.Java, "src/main/resources/META-INF/example_metadata.json")]
    [TestCase(SdkLanguage.JavaScript, "metadata.json")]
    [TestCase(SdkLanguage.Python, "_metadata.json")]
    [TestCase(SdkLanguage.Go, "testdata/_metadata.json")]
    [TestCase(SdkLanguage.DotNet, "metadata.json")]
    public async Task ReadsOnlyLanguageSpecificMetadata(SdkLanguage language, string relativePath)
    {
        WriteMetadata(relativePath, """
            {"apiVersions":{"Second":" 2026-02-01-preview ","First":"2025-01-01","Duplicate":"2025-01-01"},
             "emitterVersion":"1.0.0","crossLanguageDefinitions":{"apiVersions":{"Ignored":"1999-01-01"}}}
            """);
        WriteMetadata("nested/metadata.json", """{"apiVersions":{"Decoy":"1999-01-01"}}""");
        var info = await Populate(language);

        Assert.That(info.ApiVersions, Is.EqualTo(new[]
        {
            new PackageApiVersion("2025-01-01", "Duplicate"),
            new PackageApiVersion("2025-01-01", "First"),
            new PackageApiVersion("2026-02-01-preview", "Second")
        }));
        _logger.VerifyNoOtherCalls();
        Assert.That(info.ApiVersion, Is.EqualTo("typespec-version"));
    }

    [TestCase(SdkLanguage.Java, "src/main/resources/META-INF/example_metadata.json")]
    [TestCase(SdkLanguage.JavaScript, "metadata.json")]
    [TestCase(SdkLanguage.Python, "_metadata.json")]
    [TestCase(SdkLanguage.Go, "testdata/_metadata.json")]
    [TestCase(SdkLanguage.DotNet, "metadata.json")]
    public async Task EveryLanguageSupportsMapAndSingularFallback(SdkLanguage language, string relativePath)
    {
        foreach (var mapPrefix in new[] { "", "\"apiVersions\":{}," })
        {
            WriteMetadata(relativePath, $"{{{mapPrefix}\"apiVersion\":\" 2025-01-01 \"}}");
            var info = await Populate(language);
            Assert.That(info.ApiVersions, Is.EqualTo(new[] { new PackageApiVersion("2025-01-01") }));
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(info));
            Assert.That(json.RootElement.GetProperty("ApiVersions")[0].TryGetProperty("Namespace", out _), Is.False);
        }

        WriteMetadata(relativePath, """{"apiVersion":"2024-01-01","apiVersions":{"Example":"2025-01-01"}}""");
        var mappedInfo = await Populate(language);
        Assert.That(mappedInfo.ApiVersions, Is.EqualTo(new[] { new PackageApiVersion("2025-01-01", "Example") }));
        _logger.VerifyNoOtherCalls();
    }

    [Test]
    public async Task JavaCombinesMatchingFilesWithoutRecursing()
    {
        WriteMetadata("src/main/resources/META-INF/z_metadata.json",
            """{"apiVersions":{"First":"2025-01-01","Second":"2026-01-01"}}""");
        WriteMetadata("src/main/resources/META-INF/a_metadata.json", """{"apiVersions":{"First":"2025-01-01"}}""");
        WriteMetadata("src/main/resources/META-INF/metadata.json", "not a matching file");
        WriteMetadata("src/main/resources/META-INF/nested/other_metadata.json", "not in scope");

        var info = await Populate(SdkLanguage.Java);

        Assert.That(info.ApiVersions, Is.EqualTo(new[]
        {
            new PackageApiVersion("2025-01-01", "First"),
            new PackageApiVersion("2026-01-01", "Second")
        }));
        _logger.VerifyNoOtherCalls();
    }

    [TestCase(SdkLanguage.Java)]
    [TestCase(SdkLanguage.JavaScript)]
    [TestCase(SdkLanguage.Python)]
    [TestCase(SdkLanguage.Go)]
    [TestCase(SdkLanguage.DotNet)]
    [TestCase(SdkLanguage.Rust)]
    public async Task MissingMetadataLeavesVersionsEmpty(SdkLanguage language)
    {
        WriteMetadata("nested/metadata.json", """{"apiVersions":{"Example":"1999-01-01"}}""");
        var info = await Populate(language);

        Assert.That(info.ApiVersions, Is.Empty);
        _logger.VerifyNoOtherCalls();
    }

    [TestCase("{}")]
    [TestCase("""{"apiVersions":{}}""")]
    [TestCase("""{"ApiVersion":"2025-01-01","ApiVersions":{"Example":"2025-01-01"}}""")]
    public async Task AbsentVersionsAreNotAnError(string json)
    {
        WriteMetadata("metadata.json", json);
        var info = await Populate(SdkLanguage.DotNet);

        Assert.That(info.ApiVersions, Is.Empty);
        _logger.VerifyNoOtherCalls();
    }

    [TestCase("{")]
    [TestCase("[]")]
    [TestCase("null")]
    [TestCase("""{"apiVersions":null}""")]
    [TestCase("""{"apiVersions":"2025-01-01"}""")]
    [TestCase("""{"apiVersions":["2025-01-01"]}""")]
    [TestCase("""{"apiVersions":{"Example":null}}""")]
    [TestCase("""{"apiVersions":{"Example":42}}""")]
    [TestCase("""{"apiVersions":{"Example":[]}}""")]
    [TestCase("""{"apiVersions":{"Example":""}}""")]
    [TestCase("""{"apiVersions":{"Example":" "}}""")]
    [TestCase("""{"apiVersions":{"Valid":"2025-01-01","Invalid":false}}""")]
    public async Task InvalidMetadataLogsWarningWithoutPartialVersions(string json)
    {
        var path = WriteMetadata("metadata.json", json);
        var info = await Populate(SdkLanguage.DotNet);

        Assert.That(info.ApiVersions, Is.Empty);
        AssertWarning(path);
        Assert.That(info.ApiVersion, Is.EqualTo("typespec-version"));
    }

    [TestCase("""{"apiVersion":null}""")]
    [TestCase("""{"apiVersion":42}""")]
    [TestCase("""{"apiVersion":" "}""")]
    [TestCase("""{"apiVersions":{},"apiVersion":[]}""")]
    public async Task RejectsInvalidSingularFallback(string json)
    {
        WriteMetadata("_metadata.json", json);
        var info = await Populate(SdkLanguage.Python);

        Assert.That(info.ApiVersions, Is.Empty);
        AssertWarning("'apiVersion' must be a non-empty API version string");
    }

    [Test]
    public async Task JavaInvalidFileDoesNotReturnPartialVersions()
    {
        WriteMetadata("src/main/resources/META-INF/a_metadata.json", """{"apiVersions":{"Example":"2025-01-01"}}""");
        var invalidFile = WriteMetadata("src/main/resources/META-INF/z_metadata.json", "{");
        var info = await Populate(SdkLanguage.Java);

        Assert.That(info.ApiVersions, Is.Empty);
        AssertWarning(invalidFile);
    }

    [Test]
    public async Task UnreadableMetadataLogsWarning()
    {
        var path = WriteMetadata("metadata.json", "{}");
        using var lockedFile = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var info = await Populate(SdkLanguage.DotNet);

        Assert.That(info.ApiVersions, Is.Empty);
        AssertWarning(path);
    }

    [Test]
    public async Task SerializationKeepsTypeSpecVersionSeparate()
    {
        WriteMetadata("metadata.json", """{"apiVersions":{"Example":"2025-01-01"}}""");
        var info = await Populate(SdkLanguage.DotNet);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(info));

        Assert.That(json.RootElement.GetProperty("ApiVersions")[0].GetProperty("Version").GetString(), Is.EqualTo("2025-01-01"));
        Assert.That(json.RootElement.GetProperty("ApiVersions")[0].GetProperty("Namespace").GetString(), Is.EqualTo("Example"));
        Assert.That(json.RootElement.TryGetProperty("ApiVersion", out _), Is.False);
    }

    [Test]
    public async Task SameNamespaceWithDifferentVersionsIsPreservedAcrossFiles()
    {
        WriteMetadata("src/main/resources/META-INF/a_metadata.json", """{"apiVersions":{"Example":"2026-01-01"}}""");
        WriteMetadata("src/main/resources/META-INF/z_metadata.json", """{"apiVersions":{"Example":"2025-01-01"}}""");

        var info = await Populate(SdkLanguage.Java);

        Assert.That(info.ApiVersions, Is.EqualTo(new[]
        {
            new PackageApiVersion("2025-01-01", "Example"),
            new PackageApiVersion("2026-01-01", "Example")
        }));
    }

    [Test]
    public async Task MalformedMapDoesNotSilentlyFallBackToSingularVersion()
    {
        WriteMetadata("metadata.json", """{"apiVersions":[],"apiVersion":"2025-01-01"}""");

        var info = await Populate(SdkLanguage.JavaScript);

        Assert.That(info.ApiVersions, Is.Empty);
        AssertWarning("'apiVersions' must be a namespace-to-version object");
    }

    [Test]
    public async Task PopulatedMapDoesNotReadUnusedSingularField()
    {
        WriteMetadata("metadata.json", """{"apiVersions":{"Example":"2025-01-01"},"apiVersion":[]}""");

        var info = await Populate(SdkLanguage.JavaScript);

        Assert.That(info.ApiVersions, Is.EqualTo(new[] { new PackageApiVersion("2025-01-01", "Example") }));
        _logger.VerifyNoOtherCalls();
    }

    [Test]
    public void CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var info = new PackageInfo { PackagePath = _directory.DirectoryPath, Language = SdkLanguage.DotNet };

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            GeneratedSdkMetadataHelper.PopulateApiVersionsAsync(info, new TestLogger<GeneratedSdkMetadataHelperTests>(), cts.Token));
    }

    private async Task<PackageInfo> Populate(SdkLanguage language)
    {
        var info = new PackageInfo
        {
            PackagePath = _directory.DirectoryPath,
            Language = language,
            ApiVersion = "typespec-version"
        };
        await GeneratedSdkMetadataHelper.PopulateApiVersionsAsync(info, _logger.Object);
        return info;
    }

    private void AssertWarning(string detail)
    {
        _logger.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, type) => state.ToString()!.Contains("Unable to discover API versions")),
            It.Is<Exception>(exception => exception.ToString().Contains(detail)),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private string WriteMetadata(string relativePath, string content)
    {
        var path = Path.Combine(_directory.DirectoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
