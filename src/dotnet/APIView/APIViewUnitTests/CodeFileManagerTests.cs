using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class CodeFileManagerTests
{
    private readonly CodeFileManager _codeFileManager;
    private readonly Mock<IBlobCodeFileRepository> _mockCodeFileRepository;
    private readonly Mock<IDevopsArtifactRepository> _mockDevopsArtifactRepository;
    private readonly Mock<IEnumerable<LanguageService>> _mockLanguageServices;
    private readonly Mock<IBlobOriginalsRepository> _mockOriginalsRepository;

    public CodeFileManagerTests()
    {
        _mockLanguageServices = new Mock<IEnumerable<LanguageService>>();
        _mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();
        _mockOriginalsRepository = new Mock<IBlobOriginalsRepository>();
        _mockDevopsArtifactRepository = new Mock<IDevopsArtifactRepository>();

        // Setup empty language services list
        _mockLanguageServices.Setup(x => x.GetEnumerator())
            .Returns(new List<LanguageService>().GetEnumerator());

        _codeFileManager = new CodeFileManager(
            _mockLanguageServices.Object,
            _mockCodeFileRepository.Object,
            _mockOriginalsRepository.Object,
            _mockDevopsArtifactRepository.Object);
    }

    #region Metadata Extraction Tests

    [Fact]
    public async Task GetCodeFileAsync_WithMetadataFile_ExtractsMetadata()
    {
        MemoryStream zipStream = CreateTestZipWithMetadata(
            "TestPackage.New.json",
            "typespec-metadata.json",
            "Azure.Storage",
            "Azure.Storage",
            "Azure Storage TypeSpec");

        _mockDevopsArtifactRepository
            .Setup(r => r.DownloadPackageArtifact(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(zipStream);

        using MemoryStream originalFileStream = new();

        CodeFileResult result = await _codeFileManager.GetCodeFileAsync(
            "Azure/azure-rest-api-specs",
            "12345",
            "typeSpecAPIViewArtifacts",
            "Azure.Storage",
            null,
            "TestPackage.New.json",
            originalFileStream,
            metadataFileName: "typespec-metadata.json");

        Assert.NotNull(result);
        Assert.NotNull(result.CodeFile);
        Assert.NotNull(result.Metadata);
        Assert.Equal("Azure.Storage", result.Metadata.TypeSpec.Namespace);
        Assert.Equal("Azure Storage TypeSpec", result.Metadata.TypeSpec.Documentation);
    }

    [Fact]
    public async Task GetCodeFileAsync_WithMetadataContainingLanguages_ExtractsAllLanguageConfigs()
    {
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Core", Documentation = "Azure Core TypeSpec" },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["Python"] = new() { PackageName = "azure-core", Namespace = "azure.core" },
                ["JavaScript"] = new() { PackageName = "@azure/core", Namespace = "@azure/core" },
                ["Java"] = new() { PackageName = "com.azure.core", Namespace = "com.azure.core" },
                ["DotNet"] = new() { PackageName = "Azure.Core", Namespace = "Azure.Core" }
            }
        };

        MemoryStream zipStream = CreateTestZipWithCustomMetadata(
            "Azure.Core.New.json",
            "typespec-metadata.json",
            "Azure.Core",
            metadata);

        _mockDevopsArtifactRepository
            .Setup(r => r.DownloadPackageArtifact(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(zipStream);

        using MemoryStream originalFileStream = new();

        CodeFileResult result = await _codeFileManager.GetCodeFileAsync(
            "Azure/azure-rest-api-specs",
            "12345",
            "typeSpecAPIViewArtifacts",
            "Azure.Core",
            null,
            "Azure.Core.New.json",
            originalFileStream,
            metadataFileName: "typespec-metadata.json");

        Assert.NotNull(result.Metadata);
        Assert.NotNull(result.Metadata.Languages);
        Assert.Equal(4, result.Metadata.Languages.Count);
        Assert.Equal("azure-core", result.Metadata.Languages["Python"].PackageName);
        Assert.Equal("@azure/core", result.Metadata.Languages["JavaScript"].PackageName);
        Assert.Equal("com.azure.core", result.Metadata.Languages["Java"].PackageName);
        Assert.Equal("Azure.Core", result.Metadata.Languages["DotNet"].PackageName);
    }

    [Fact]
    public async Task GetCodeFileAsync_WithoutMetadataFileName_ReturnsNullMetadata()
    {
        MemoryStream zipStream = CreateTestZipWithMetadata(
            "TestPackage.New.json",
            "typespec-metadata.json",
            "Azure.Storage",
            "Azure.Storage",
            "Test");

        _mockDevopsArtifactRepository
            .Setup(r => r.DownloadPackageArtifact(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(zipStream);

        using MemoryStream originalFileStream = new();

        CodeFileResult result = await _codeFileManager.GetCodeFileAsync(
            "Azure/azure-rest-api-specs",
            "12345",
            "typeSpecAPIViewArtifacts",
            "Azure.Storage",
            null,
            "TestPackage.New.json",
            originalFileStream,
            metadataFileName: null);

        Assert.NotNull(result);
        Assert.NotNull(result.CodeFile);
        Assert.Null(result.Metadata);
    }

    [Fact]
    public async Task GetCodeFileAsync_WithMissingMetadataFile_ReturnsNullMetadata()
    {
        MemoryStream zipStream = CreateTestZipCodeFileOnly(
            "TestPackage.New.json",
            "Azure.Storage");

        _mockDevopsArtifactRepository
            .Setup(r => r.DownloadPackageArtifact(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(zipStream);

        using MemoryStream originalFileStream = new();

        CodeFileResult result = await _codeFileManager.GetCodeFileAsync(
            "Azure/azure-rest-api-specs",
            "12345",
            "typeSpecAPIViewArtifacts",
            "Azure.Storage",
            null,
            "TestPackage.New.json",
            originalFileStream,
            metadataFileName: "typespec-metadata.json");

        Assert.NotNull(result.CodeFile);
        Assert.Null(result.Metadata); 
    }

    [Fact]
    public async Task GetCodeFileAsync_WithBaselineAndMetadata_ExtractsBoth()
    {
        MemoryStream zipStream = CreateTestZipWithBaselineAndMetadata(
            "Azure.Storage.New.json",
            "Azure.Storage.Baseline.json",
            "typespec-metadata.json",
            "Azure.Storage");

        _mockDevopsArtifactRepository
            .Setup(r => r.DownloadPackageArtifact(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(zipStream);

        using MemoryStream originalFileStream = new();
        using MemoryStream baselineStream = new();

        CodeFileResult result = await _codeFileManager.GetCodeFileAsync(
            "Azure/azure-rest-api-specs",
            "12345",
            "typeSpecAPIViewArtifacts",
            "Azure.Storage",
            null,
            "Azure.Storage.New.json",
            originalFileStream,
            "Azure.Storage.Baseline.json",
            baselineStream,
            metadataFileName: "typespec-metadata.json");

        Assert.NotNull(result.CodeFile);
        Assert.NotNull(result.Metadata);
        Assert.True(baselineStream.Length > 0, "Baseline stream should have content");
    }

    #endregion

    #region Helper Methods

    private static MemoryStream CreateTestZipWithMetadata(
        string codeFileName,
        string metadataFileName,
        string packageName,
        string typeSpecNamespace,
        string documentation)
    {
        TypeSpecMetadata metadata = new()
        {
            EmitterVersion = "0.7.2",
            TypeSpec =
                new TypeSpecInfo { Namespace = typeSpecNamespace, Documentation = documentation, Type = "client" },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["Python"] = new()
                {
                    PackageName = packageName.ToLower().Replace(".", "-"),
                    Namespace = packageName.ToLower().Replace(".", ".")
                }
            }
        };

        return CreateTestZipWithCustomMetadata(codeFileName, metadataFileName, packageName, metadata);
    }

    private static MemoryStream CreateTestZipWithCustomMetadata(
        string codeFileName,
        string metadataFileName,
        string packageName,
        TypeSpecMetadata metadata)
    {
        MemoryStream zipStream = new();

        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
        {
            CodeFile codeFile = new() { PackageName = packageName, Language = "TypeSpec", VersionString = "1.0.0" };
            AddJsonEntry(archive, codeFileName, codeFile);
            AddJsonEntry(archive, metadataFileName, metadata);
        }

        zipStream.Position = 0;
        return zipStream;
    }

    private static MemoryStream CreateTestZipCodeFileOnly(string codeFileName, string packageName)
    {
        MemoryStream zipStream = new();

        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
        {
            CodeFile codeFile = new() { PackageName = packageName, Language = "TypeSpec", VersionString = "1.0.0" };
            AddJsonEntry(archive, codeFileName, codeFile);
        }

        zipStream.Position = 0;
        return zipStream;
    }

    private static MemoryStream CreateTestZipWithBaselineAndMetadata(
        string newCodeFileName,
        string baselineCodeFileName,
        string metadataFileName,
        string packageName)
    {
        MemoryStream zipStream = new();

        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
        {
            CodeFile newCodeFile = new() { PackageName = packageName, Language = "TypeSpec", VersionString = "2.0.0" };
            AddJsonEntry(archive, newCodeFileName, newCodeFile);

            CodeFile baselineCodeFile = new()
            {
                PackageName = packageName, Language = "TypeSpec", VersionString = "1.0.0"
            };
            AddJsonEntry(archive, baselineCodeFileName, baselineCodeFile);

            TypeSpecMetadata metadata = new()
            {
                TypeSpec = new TypeSpecInfo { Namespace = packageName, Documentation = "Test documentation" }
            };
            AddJsonEntry(archive, metadataFileName, metadata);
        }

        zipStream.Position = 0;
        return zipStream;
    }

    private static void AddJsonEntry<T>(ZipArchive archive, string entryName, T content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName);
        using Stream entryStream = entry.Open();
        string json = JsonSerializer.Serialize(content,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        entryStream.Write(bytes, 0, bytes.Length);
    }

    #endregion
}
