using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ApiView;
using APIView.Model;
using APIViewWeb;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace APIViewUnitTests;

/// <summary>
///     Test implementation of LanguageService for unit testing
/// </summary>
public class TestLanguageService : LanguageService
{
    public TestLanguageService()
    {
        Name = "Python";
    }

    public string CapturedCrossLanguageMetadataJson { get; private set; }
    public CodeFile CodeFileToReturn { get; set; }

    public override string Name { get; }
    public override string[] Extensions { get; }
    public override string VersionString => "2.0.0";

    public override bool CanUpdate(string versionString)
    {
        return true;
    }

    public override Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis,
        string crossLanguagePackageMetadata = null)
    {
        CapturedCrossLanguageMetadataJson = crossLanguagePackageMetadata;
        CodeFile codeFile = CodeFileToReturn ?? new CodeFile
        {
            Name = originalName,
            Language = "Python",
            VersionString = "2.0.0",
            PackageName = "test-package",
            PackageVersion = "1.0.0"
        };

        return Task.FromResult(codeFile);
    }

    public override bool IsSupportedFile(string name)
    {
        return name.EndsWith(".whl") || name.EndsWith("_python.json");
    }
}

public class APIRevisionsManagerTests
{
    private readonly APIRevisionsManager _manager;
    private readonly Mock<ICosmosAPIRevisionsRepository> _mockAPIRevisionsRepository;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly Mock<ICodeFileManager> _mockCodeFileManager;
    private readonly Mock<IBlobCodeFileRepository> _mockCodeFileRepository;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IDevopsArtifactRepository> _mockDevopsArtifactRepository;
    private readonly Mock<IHubContext<SignalRHub>> _mockHubContext;
    private readonly Mock<INotificationManager> _mockNotificationManager;
    private readonly Mock<IBlobOriginalsRepository> _mockOriginalsRepository;
    private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
    private readonly TelemetryClient _telemetryClient;
    private readonly TestLanguageService _testLanguageService;

    public APIRevisionsManagerTests()
    {
        _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
        _mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();
        _mockAPIRevisionsRepository = new Mock<ICosmosAPIRevisionsRepository>();
        _mockOriginalsRepository = new Mock<IBlobOriginalsRepository>();
        _mockAuthorizationService = new Mock<IAuthorizationService>();
        _mockHubContext = new Mock<IHubContext<SignalRHub>>();
        _mockCodeFileManager = new Mock<ICodeFileManager>();
        _mockDevopsArtifactRepository = new Mock<IDevopsArtifactRepository>();
        _mockNotificationManager = new Mock<INotificationManager>();
        _mockConfiguration = new Mock<IConfiguration>();

        TelemetryConfiguration telemetryConfiguration = new();
        _telemetryClient = new TelemetryClient(telemetryConfiguration);

        _testLanguageService = new TestLanguageService();

        List<LanguageService> languageServices = new() { _testLanguageService };

        _manager = new APIRevisionsManager(
            _mockAuthorizationService.Object,
            _mockReviewsRepository.Object,
            _mockAPIRevisionsRepository.Object,
            _mockHubContext.Object,
            languageServices,
            _mockDevopsArtifactRepository.Object,
            _mockCodeFileManager.Object,
            _mockCodeFileRepository.Object,
            _mockOriginalsRepository.Object,
            _mockNotificationManager.Object,
            _telemetryClient,
            _mockConfiguration.Object
        );
    }

    [Fact]
    public async Task UpdateAPIRevisionAsync_WithCrossLanguageMetadata_SerializesAndPassesToLanguageService()
    {
        CrossLanguageMetadata crossLanguageMetadata = new()
        {
            CrossLanguagePackageId = "TestPackage",
            CrossLanguageDefinitionId = new Dictionary<string, string>
            {
                { "test.module.Class", "TestPackage.Class" }, { "test.module.Method", "TestPackage.Method" }
            }
        };

        APIRevisionListItemModel revision = CreateTestRevision();
        CodeFile existingCodeFile = CreateCodeFile(revision.Files[0].FileName, crossLanguageMetadata);
        CodeFile newCodeFile = CreateCodeFile(revision.Files[0].FileName);

        SetupMocksForUpdate(revision.Id, revision.Files[0].FileId, existingCodeFile, newCodeFile);

        await _manager.UpdateAPIRevisionAsync(revision, _testLanguageService, false);

        Assert.NotNull(_testLanguageService.CapturedCrossLanguageMetadataJson);

        CrossLanguageMetadata deserializedMetadata = JsonSerializer.Deserialize<CrossLanguageMetadata>(_testLanguageService.CapturedCrossLanguageMetadataJson);
        Assert.NotNull(deserializedMetadata);
        Assert.Equal("TestPackage", deserializedMetadata.CrossLanguagePackageId);
        Assert.Equal(2, deserializedMetadata.CrossLanguageDefinitionId.Count);
        Assert.Equal("TestPackage.Class", deserializedMetadata.CrossLanguageDefinitionId["test.module.Class"]);
        Assert.Equal("TestPackage.Method", deserializedMetadata.CrossLanguageDefinitionId["test.module.Method"]);
    }

    [Fact]
    public async Task UpdateAPIRevisionAsync_WithNullCrossLanguageMetadata_PassesNullToLanguageService()
    {
        APIRevisionListItemModel revision = CreateTestRevision();
        CodeFile existingCodeFile = CreateCodeFile(revision.Files[0].FileName);
        CodeFile newCodeFile = CreateCodeFile(revision.Files[0].FileName);

        SetupMocksForUpdate(revision.Id, revision.Files[0].FileId, existingCodeFile, newCodeFile);

        await _manager.UpdateAPIRevisionAsync(revision, _testLanguageService, false);
        Assert.Null(_testLanguageService.CapturedCrossLanguageMetadataJson);
    }


    [Fact]
    public async Task UpdateAPIRevisionAsync_WithEmptyCrossLanguageDefinitionId_SerializesCorrectly()
    {
        CrossLanguageMetadata crossLanguageMetadata = new()
        {
            CrossLanguagePackageId = "TestPackage",
            CrossLanguageDefinitionId = new Dictionary<string, string>() // Empty dictionary
        };

        APIRevisionListItemModel revision = CreateTestRevision();
        CodeFile existingCodeFile = CreateCodeFile(revision.Files[0].FileName, crossLanguageMetadata);
        CodeFile newCodeFile = CreateCodeFile(revision.Files[0].FileName);

        SetupMocksForUpdate(revision.Id, revision.Files[0].FileId, existingCodeFile, newCodeFile);

        await _manager.UpdateAPIRevisionAsync(revision, _testLanguageService, false);

        Assert.NotNull(_testLanguageService.CapturedCrossLanguageMetadataJson);

        CrossLanguageMetadata deserializedMetadata = JsonSerializer.Deserialize<CrossLanguageMetadata>(_testLanguageService.CapturedCrossLanguageMetadataJson);
        Assert.NotNull(deserializedMetadata);
        Assert.Equal("TestPackage", deserializedMetadata.CrossLanguagePackageId);
        Assert.NotNull(deserializedMetadata.CrossLanguageDefinitionId);
        Assert.Empty(deserializedMetadata.CrossLanguageDefinitionId);
    }


    [Fact]
    public async Task UpdateAPIRevisionAsync_VerifyUpgradabilityOnly_DoesNotCallUpsert()
    {
        CrossLanguageMetadata crossLanguageMetadata = new()
        {
            CrossLanguagePackageId = "TestPackage",
            CrossLanguageDefinitionId = new Dictionary<string, string>
            {
                { "test.module.Class", "TestPackage.Class" }
            }
        };

        APIRevisionListItemModel revision = CreateTestRevision();
        CodeFile existingCodeFile = CreateCodeFile(revision.Files[0].FileName, crossLanguageMetadata);
        CodeFile newCodeFile = CreateCodeFile(revision.Files[0].FileName);

        SetupMocksForUpdate(revision.Id, revision.Files[0].FileId, existingCodeFile, newCodeFile);

        await _manager.UpdateAPIRevisionAsync(revision, _testLanguageService, true);

        _mockCodeFileRepository.Verify(
            x => x.UpsertCodeFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CodeFile>()),
            Times.Never);
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()),
            Times.Never);
    }

    private APIRevisionListItemModel CreateTestRevision(string revisionId = "test-revision-id",
        string fileId = "test-file-id",
        string fileName = "test-package_python.json")
    {
        return new APIRevisionListItemModel
        {
            Id = revisionId,
            Language = "Python",
            Files = new List<APICodeFileModel>
            {
                new() { FileId = fileId, FileName = fileName, HasOriginal = true, VersionString = "1.0.0" }
            }
        };
    }

    private void SetupMocksForUpdate(string revisionId, string fileId, CodeFile existingCodeFile, CodeFile newCodeFile)
    {
        MemoryStream originalStream = new();

        _mockOriginalsRepository
            .Setup(x => x.GetOriginalAsync(fileId))
            .ReturnsAsync(originalStream);

        _mockCodeFileRepository
            .Setup(x => x.GetCodeFileFromStorageAsync(revisionId, fileId))
            .ReturnsAsync(existingCodeFile);

        _testLanguageService.CodeFileToReturn = newCodeFile;
    }

    private CodeFile CreateCodeFile(string fileName, CrossLanguageMetadata crossLanguageMetadata = null)
    {
        return new CodeFile
        {
            Name = fileName,
            Language = "Python",
            VersionString = "2.0.0",
            CrossLanguageMetadata = crossLanguageMetadata
        };
    }
}
