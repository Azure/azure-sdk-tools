using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using APIView;
using APIView.Identity;
using APIView.Model;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
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
    private readonly Mock<IDiagnosticCommentService> _mockDiagnosticCommentService;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly Mock<ICodeFileManager> _mockCodeFileManager;
    private readonly Mock<IBlobCodeFileRepository> _mockCodeFileRepository;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IDevopsArtifactRepository> _mockDevopsArtifactRepository;
    private readonly Mock<IHubContext<SignalRHub>> _mockHubContext;
    private readonly Mock<INotificationManager> _mockNotificationManager;
    private readonly Mock<ICosmosCommentsRepository> _mockCommentsRepository;
    private readonly Mock<IBlobOriginalsRepository> _mockOriginalsRepository;
    private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
    private readonly Mock<IProjectsManager> _mockProjectsManager;
    private readonly Mock<IAPIVersionsManager> _mockApiVersionsManager;
    private readonly TelemetryClient _telemetryClient;
    private readonly TestLanguageService _testLanguageService;

    public APIRevisionsManagerTests()
    {
        _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
        _mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();
        _mockAPIRevisionsRepository = new Mock<ICosmosAPIRevisionsRepository>();
        _mockDiagnosticCommentService = new Mock<IDiagnosticCommentService>();
        _mockOriginalsRepository = new Mock<IBlobOriginalsRepository>();
        _mockAuthorizationService = new Mock<IAuthorizationService>();
        _mockHubContext = new Mock<IHubContext<SignalRHub>>();
        _mockCodeFileManager = new Mock<ICodeFileManager>();
        _mockDevopsArtifactRepository = new Mock<IDevopsArtifactRepository>();
        _mockNotificationManager = new Mock<INotificationManager>();
        _mockCommentsRepository = new Mock<ICosmosCommentsRepository>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockProjectsManager = new Mock<IProjectsManager>();
        _mockApiVersionsManager = new Mock<IAPIVersionsManager>();
        _mockApiVersionsManager
            .Setup(m => m.GetOrCreateVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
            .ReturnsAsync(new APIVersionModel { Id = "default-version-id" });

        TelemetryConfiguration telemetryConfiguration = new();
        _telemetryClient = new TelemetryClient(telemetryConfiguration);

        _testLanguageService = new TestLanguageService();

        List<LanguageService> languageServices = new() { _testLanguageService };

        _manager = new APIRevisionsManager(
            _mockAuthorizationService.Object,
            _mockReviewsRepository.Object,
            _mockAPIRevisionsRepository.Object,
            _mockDiagnosticCommentService.Object,
            _mockHubContext.Object,
            languageServices,
            _mockDevopsArtifactRepository.Object,
            _mockCodeFileManager.Object,
            _mockCodeFileRepository.Object,
            _mockOriginalsRepository.Object,
            _mockNotificationManager.Object,
            _mockCommentsRepository.Object,
            _telemetryClient,
            _mockProjectsManager.Object,
            _mockApiVersionsManager.Object,
            _mockConfiguration.Object
        );
    }

    [Fact]
    public async Task ToggleAPIRevisionApprovalAsync_SendsSubscriberEmail_WhenRevisionApprovedAndReviewAlreadyApproved()
    {
        var user = CreateTestUser("approver1");
        const string reviewId = "review-1";
        const string revisionId = "revision-1";

        var review = new ReviewListItemModel
        {
            Id = reviewId,
            IsApproved = true,
            ChangeHistory = new List<ReviewChangeHistoryModel>()
        };

        var revision = new APIRevisionListItemModel
        {
            Id = revisionId,
            ReviewId = reviewId,
            IsApproved = false,
            Approvers = new HashSet<string>(),
            ChangeHistory = new List<APIRevisionChangeHistoryModel>()
        };

        SetupSignalRMocks();
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        _mockAPIRevisionsRepository.Setup(r => r.GetAPIRevisionAsync(revisionId)).ReturnsAsync(revision);
        _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId)).ReturnsAsync(review);
        _mockAPIRevisionsRepository.Setup(r => r.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>())).Returns(Task.CompletedTask);

        var result = await _manager.ToggleAPIRevisionApprovalAsync(user, reviewId, revisionId);

        Assert.False(result.updateReview);
        Assert.True(result.apiRevision.IsApproved);
        _mockNotificationManager.Verify(
            n => n.NotifySubscribersOnApprovalAsync(review, revision, user, false),
            Times.Once);
    }

    [Fact]
    public async Task ToggleAPIRevisionApprovalAsync_DoesNotSendRevisionEmail_WhenReviewWillBeAutoApproved()
    {
        var user = CreateTestUser("approver1");
        const string reviewId = "review-1";
        const string revisionId = "revision-1";

        var review = new ReviewListItemModel
        {
            Id = reviewId,
            IsApproved = false,
            ChangeHistory = new List<ReviewChangeHistoryModel>()
        };

        var revision = new APIRevisionListItemModel
        {
            Id = revisionId,
            ReviewId = reviewId,
            IsApproved = false,
            Approvers = new HashSet<string>(),
            ChangeHistory = new List<APIRevisionChangeHistoryModel>()
        };

        SetupSignalRMocks();
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        _mockAPIRevisionsRepository.Setup(r => r.GetAPIRevisionAsync(revisionId)).ReturnsAsync(revision);
        _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId)).ReturnsAsync(review);
        _mockAPIRevisionsRepository.Setup(r => r.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>())).Returns(Task.CompletedTask);

        var result = await _manager.ToggleAPIRevisionApprovalAsync(user, reviewId, revisionId);

        Assert.True(result.updateReview);
        Assert.True(result.apiRevision.IsApproved);
        _mockNotificationManager.Verify(
            n => n.NotifySubscribersOnApprovalAsync(It.IsAny<ReviewListItemModel>(), It.IsAny<APIRevisionListItemModel>(), It.IsAny<ClaimsPrincipal>(), false),
            Times.Never);
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

    #region APIVersionId Tests

    [Fact]
    public async Task CreateAPIRevisionAsync_WithPackageVersion_SetsAPIVersionId()
    {
        const string reviewId = "review-id";
        const string expectedVersionId = "ver-id";

        var codeFile = new CodeFile
        {
            Name = "test.json",
            Language = "C#",
            PackageName = "TestPackage",
            PackageVersion = "1.0.0"
        };

        _mockApiVersionsManager
            .Setup(m => m.GetOrCreateVersionAsync(reviewId, "1.0.0", It.IsAny<int?>(), It.IsAny<string>()))
            .ReturnsAsync(new APIVersionModel { Id = expectedVersionId });

        _mockCodeFileManager
            .Setup(m => m.CreateReviewCodeFileModel(It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>()))
            .ReturnsAsync(new APICodeFileModel { FileId = "file-id", FileName = "test.json" });

        _mockDiagnosticCommentService
            .Setup(m => m.SyncDiagnosticCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CodeDiagnostic[]>(), It.IsAny<IEnumerable<CommentItemModel>>()))
            .ReturnsAsync(new DiagnosticSyncResult { DiagnosticsHash = "hash" });

        _mockAPIRevisionsRepository
            .Setup(m => m.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        using var memoryStream = new MemoryStream();
        APIRevisionListItemModel revision = await _manager.CreateAPIRevisionAsync(
            userName: "testuser", reviewId: reviewId, apiRevisionType: APIRevisionType.Automatic,
            label: "test-label", memoryStream: memoryStream, codeFile: codeFile, originalName: "test.json");

        Assert.Equal(expectedVersionId, revision.APIVersionId);
    }

    #endregion

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

    private static ClaimsPrincipal CreateTestUser(string login)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimConstants.Login, login),
            new Claim(ClaimConstants.Name, login),
            new Claim(ClaimConstants.Email, $"{login}@contoso.com")
        });
        return new ClaimsPrincipal(identity);
    }

    private void SetupSignalRMocks()
    {
        var clients = new Mock<IHubClients>();
        var proxy = new Mock<IClientProxy>();

        proxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        clients.Setup(c => c.All).Returns(proxy.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(clients.Object);
    }

    #region UpdateAPIRevisionCodeFileAsync Tests

    private const string TestReviewId = "test-review-id";
    private const string TestApiRevisionId = "test-revision-id";
    private const string TestFileId = "test-file-id";
    private const string TestFileName = "test-package_python.json";

    private async Task<CodeFile> SetupUpdateAPIRevisionCodeFileAsyncTest(
        CodeFile pipelineCodeFile,
        CodeFile existingCodeFile = null)
    {
        MemoryStream zipStream = await CreateZipArchiveWithCodeFile(TestReviewId, TestApiRevisionId, TestFileId, pipelineCodeFile);

        _mockDevopsArtifactRepository
            .Setup(x => x.DownloadPackageArtifact(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null,
                It.IsAny<string>(), "zip"))
            .ReturnsAsync(zipStream);

        _mockReviewsRepository
            .Setup(x => x.GetReviewAsync(TestReviewId))
            .ReturnsAsync(new ReviewListItemModel { Id = TestReviewId, Language = "Python" });

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionAsync(TestApiRevisionId))
            .ReturnsAsync(new APIRevisionListItemModel
            {
                Id = TestApiRevisionId,
                Language = "Python",
                Files = new List<APICodeFileModel> { new() { FileId = TestFileId, FileName = TestFileName } }
            });

        _mockCodeFileRepository
            .Setup(x => x.GetCodeFileFromStorageAsync(TestApiRevisionId, TestFileId))
            .ReturnsAsync(existingCodeFile);

        CodeFile capturedCodeFile = null;
        _mockCodeFileRepository
            .Setup(x => x.UpsertCodeFileAsync(TestApiRevisionId, TestFileId, It.IsAny<CodeFile>()))
            .Callback<string, string, CodeFile>((_, _, cf) => capturedCodeFile = cf)
            .Returns(Task.CompletedTask);

        await _manager.UpdateAPIRevisionCodeFileAsync("test-repo", "12345", "apiview", "internal");

        return capturedCodeFile;
    }

    [Fact]
    public async Task UpdateAPIRevisionCodeFileAsync_PreservesCrossLanguageMetadata_WhenNewCodeFileHasNone()
    {
        CrossLanguageMetadata existingMetadata = CreateTestMetadata("ExistingPackage", "existing.module.Class", "ExistingPackage.Class");
        CodeFile existingCodeFile = CreatePipelineCodeFile(existingMetadata);
        existingCodeFile.VersionString = "1.0.0";

        CodeFile result = await SetupUpdateAPIRevisionCodeFileAsyncTest(CreatePipelineCodeFile(null), existingCodeFile);

        Assert.NotNull(result.CrossLanguageMetadata);
        Assert.Equal("ExistingPackage", result.CrossLanguageMetadata.CrossLanguagePackageId);
        Assert.Equal("ExistingPackage.Class", result.CrossLanguageMetadata.CrossLanguageDefinitionId["existing.module.Class"]);
    }

    [Fact]
    public async Task UpdateAPIRevisionCodeFileAsync_KeepsNewCrossLanguageMetadata_WhenNewCodeFileHasIt()
    {
        CrossLanguageMetadata newMetadata = CreateTestMetadata("NewPackage", "new.module.Class", "NewPackage.Class");

        CodeFile result = await SetupUpdateAPIRevisionCodeFileAsyncTest(CreatePipelineCodeFile(newMetadata), null);

        _mockCodeFileRepository.Verify(x => x.GetCodeFileFromStorageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.NotNull(result.CrossLanguageMetadata);
        Assert.Equal("NewPackage", result.CrossLanguageMetadata.CrossLanguagePackageId);
    }

    [Fact]
    public async Task UpdateAPIRevisionCodeFileAsync_HandlesNoExistingCodeFile_Gracefully()
    {
        CodeFile result = await SetupUpdateAPIRevisionCodeFileAsyncTest(CreatePipelineCodeFile(null), null);

        Assert.NotNull(result);
        Assert.Null(result.CrossLanguageMetadata);
    }

    private async Task<MemoryStream> CreateZipArchiveWithCodeFile(string reviewId, string apiRevisionId, string fileId,
        CodeFile codeFile)
    {
        MemoryStream zipStream = new();
        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
        {
            // Path format: apiview/{reviewId}/{apiRevisionId}/{fileId}.json
            string entryPath = $"apiview/{reviewId}/{apiRevisionId}/{fileId}.json";
            ZipArchiveEntry entry = archive.CreateEntry(entryPath);

            using Stream entryStream = entry.Open();
            await codeFile.SerializeAsync(entryStream);
        }

        zipStream.Position = 0;
        return zipStream;
    }

    private static CodeFile CreatePipelineCodeFile(CrossLanguageMetadata metadata = null) => new()
    {
        Name = TestFileName,
        Language = "Python",
        VersionString = "2.0.0",
        PackageName = "test-package",
        PackageVersion = "1.0.0",
        CrossLanguageMetadata = metadata
    };

    private static CrossLanguageMetadata CreateTestMetadata(string packageId, string key, string value) => new()
    {
        CrossLanguagePackageId = packageId,
        CrossLanguageDefinitionId = new Dictionary<string, string> { { key, value } }
    };

    [Fact]
    public async Task UpdateAPIRevisionCodeFileAsync_WithTypeSpecMetadata_CallsUpsertProjectFromMetadata()
    {
        var reviewId = "typespec-review-id";
        var apiRevisionId = "typespec-revision-id";
        var fileId = "typespec-file-id";
        var metadataFileName = "typespec-metadata.json";

        var typeSpecMetadata = new TypeSpecMetadata
        {
            EmitterVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            TypeSpec = new TypeSpecInfo
            {
                Namespace = "Azure.AI.TextAnalytics",
                Documentation = "Test documentation",
                Type = "client"
            },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["python"] = [new LanguageConfig
                {
                    EmitterName = "@azure-tools/typespec-python",
                    PackageName = "azure-ai-textanalytics",
                    Namespace = "azure.ai.textanalytics"
                }]
            },
            SourceConfigPath = "specification/ai/Azure.AI.TextAnalytics/tspconfig.yaml"
        };

        MemoryStream zipStream = await CreateZipArchiveWithCodeFileAndMetadata(
            reviewId, apiRevisionId, fileId, 
            CreatePipelineCodeFile(null), 
            metadataFileName, 
            typeSpecMetadata);

        _mockDevopsArtifactRepository
            .Setup(x => x.DownloadPackageArtifact(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null,
                It.IsAny<string>(), "zip"))
            .ReturnsAsync(zipStream);

        _mockReviewsRepository
            .Setup(x => x.GetReviewAsync(reviewId))
            .ReturnsAsync(new ReviewListItemModel { Id = reviewId, Language = ApiViewConstants.TypeSpecLanguage });

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionAsync(apiRevisionId))
            .ReturnsAsync(new APIRevisionListItemModel
            {
                Id = apiRevisionId,
                Language = ApiViewConstants.TypeSpecLanguage,
                Files = new List<APICodeFileModel> { new() { FileId = fileId, FileName = TestFileName } }
            });

        _mockCodeFileRepository
            .Setup(x => x.GetCodeFileFromStorageAsync(apiRevisionId, fileId))
            .ReturnsAsync((CodeFile)null);

        _mockCodeFileRepository
            .Setup(x => x.UpsertCodeFileAsync(apiRevisionId, fileId, It.IsAny<CodeFile>()))
            .Returns(Task.CompletedTask);

        await _manager.UpdateAPIRevisionCodeFileAsync("test-repo", "12345", "apiview", "internal", metadataFileName);

        _mockProjectsManager.Verify(
            x => x.UpsertProjectFromMetadataAsync(
                It.Is<string>(u => u == ApiViewConstants.AzureSdkBotName),
                It.Is<TypeSpecMetadata>(m => 
                    m.TypeSpec.Namespace == "Azure.AI.TextAnalytics" &&
                    m.EmitterVersion == "1.0.0"),
                It.Is<ReviewListItemModel>(r => r.Id == reviewId)),
            Times.Once,
            "UpsertProjectFromMetadataAsync should be called for TypeSpec reviews with metadata");
    }

    [Fact]
    public async Task UpdateAPIRevisionCodeFileAsync_WithNonTypeSpecReview_DoesNotCallUpsertProjectFromMetadata()
    {
        var metadataFileName = "typespec-metadata.json";

        MemoryStream zipStream = await CreateZipArchiveWithCodeFile(TestReviewId, TestApiRevisionId, TestFileId, CreatePipelineCodeFile(null));

        _mockDevopsArtifactRepository
            .Setup(x => x.DownloadPackageArtifact(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null,
                It.IsAny<string>(), "zip"))
            .ReturnsAsync(zipStream);

        _mockReviewsRepository
            .Setup(x => x.GetReviewAsync(TestReviewId))
            .ReturnsAsync(new ReviewListItemModel { Id = TestReviewId, Language = "Python" }); // Non-TypeSpec

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionAsync(TestApiRevisionId))
            .ReturnsAsync(new APIRevisionListItemModel
            {
                Id = TestApiRevisionId,
                Language = "Python",
                Files = new List<APICodeFileModel> { new() { FileId = TestFileId, FileName = TestFileName } }
            });

        _mockCodeFileRepository
            .Setup(x => x.GetCodeFileFromStorageAsync(TestApiRevisionId, TestFileId))
            .ReturnsAsync((CodeFile)null);

        _mockCodeFileRepository
            .Setup(x => x.UpsertCodeFileAsync(TestApiRevisionId, TestFileId, It.IsAny<CodeFile>()))
            .Returns(Task.CompletedTask);

        await _manager.UpdateAPIRevisionCodeFileAsync("test-repo", "12345", "apiview", "internal", metadataFileName);

        _mockProjectsManager.Verify(
            x => x.UpsertProjectFromMetadataAsync(
                It.IsAny<string>(),
                It.IsAny<TypeSpecMetadata>(),
                It.IsAny<ReviewListItemModel>()),
            Times.Never,
            "UpsertProjectFromMetadataAsync should not be called for non-TypeSpec reviews");
    }

    private async Task<MemoryStream> CreateZipArchiveWithCodeFileAndMetadata(
        string reviewId, 
        string apiRevisionId, 
        string fileId,
        CodeFile codeFile,
        string metadataFileName,
        TypeSpecMetadata metadata)
    {
        MemoryStream zipStream = new();
        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
        {
            // Add code file: apiview/{reviewId}/{apiRevisionId}/{fileId}.json
            string codeFilePath = $"apiview/{reviewId}/{apiRevisionId}/{fileId}.json";
            ZipArchiveEntry codeEntry = archive.CreateEntry(codeFilePath);
            using (Stream codeEntryStream = codeEntry.Open())
            {
                await codeFile.SerializeAsync(codeEntryStream);
            }

            // Add metadata file: apiview/{reviewId}/{apiRevisionId}/{metadataFileName}
            string metadataPath = $"apiview/{reviewId}/{apiRevisionId}/{metadataFileName}";
            ZipArchiveEntry metadataEntry = archive.CreateEntry(metadataPath);
            using (Stream metadataStream = metadataEntry.Open())
            {
                var jsonOptions = new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                };
                await JsonSerializer.SerializeAsync(metadataStream, metadata, jsonOptions);
            }
        }

        zipStream.Position = 0;
        return zipStream;
    }
    #endregion

    #region CarryForwardRevisionDataAsync Tests

    [Fact]
    public async Task CarryForwardRevisionDataAsync_CopiesHasAutoGeneratedComments_WhenSourceHasTrue()
    {
        var sourceRevision = new APIRevisionListItemModel
        {
            Id = "source-revision",
            ReviewId = "test-review",
            HasAutoGeneratedComments = true
        };

        var targetRevision = new APIRevisionListItemModel
        {
            Id = "target-revision",
            ReviewId = "test-review",
            HasAutoGeneratedComments = false
        };

        _mockAPIRevisionsRepository
            .Setup(x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await _manager.CarryForwardRevisionDataAsync(targetRevision, sourceRevision);

        Assert.True(targetRevision.HasAutoGeneratedComments);
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => 
                r.Id == "target-revision" && 
                r.HasAutoGeneratedComments)),
            Times.Once);
    }

    [Fact]
    public async Task CarryForwardRevisionDataAsync_DoesNotCopy_WhenSourceHasFalse()
    {
        var sourceRevision = new APIRevisionListItemModel
        {
            Id = "source-revision",
            ReviewId = "test-review",
            HasAutoGeneratedComments = false
        };

        var targetRevision = new APIRevisionListItemModel
        {
            Id = "target-revision",
            ReviewId = "test-review",
            HasAutoGeneratedComments = false
        };

        await _manager.CarryForwardRevisionDataAsync(targetRevision, sourceRevision);

        Assert.False(targetRevision.HasAutoGeneratedComments);
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()),
            Times.Never);
    }

    [Fact]
    public async Task CarryForwardRevisionDataAsync_CopiesApproval_WhenSourceIsApproved()
    {
        var sourceRevision = new APIRevisionListItemModel
        {
            Id = "source-revision",
            ReviewId = "test-review",
            IsApproved = true,
            Approvers = new HashSet<string> { "test-approver" },
            ChangeHistory = new List<APIRevisionChangeHistoryModel>()
        };

        var targetRevision = new APIRevisionListItemModel
        {
            Id = "target-revision",
            ReviewId = "test-review",
            IsApproved = false,
            Approvers = new HashSet<string>(),
            ChangeHistory = new List<APIRevisionChangeHistoryModel>()
        };

        _mockAPIRevisionsRepository
            .Setup(x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await _manager.CarryForwardRevisionDataAsync(targetRevision, sourceRevision);

        Assert.True(targetRevision.IsApproved);
        Assert.Contains("test-approver", targetRevision.Approvers);
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => 
                r.Id == "target-revision" && 
                r.IsApproved)),
            Times.Once);
    }

    [Fact]
    public async Task CarryForwardRevisionDataAsync_CopiesBothApprovalAndComments_InSingleWrite()
    {
        var sourceRevision = new APIRevisionListItemModel
        {
            Id = "source-revision",
            ReviewId = "test-review",
            IsApproved = true,
            HasAutoGeneratedComments = true,
            Approvers = new HashSet<string> { "test-approver" },
            ChangeHistory = new List<APIRevisionChangeHistoryModel>()
        };

        var targetRevision = new APIRevisionListItemModel
        {
            Id = "target-revision",
            ReviewId = "test-review",
            IsApproved = false,
            HasAutoGeneratedComments = false,
            Approvers = new HashSet<string>(),
            ChangeHistory = new List<APIRevisionChangeHistoryModel>()
        };

        _mockAPIRevisionsRepository
            .Setup(x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await _manager.CarryForwardRevisionDataAsync(targetRevision, sourceRevision);

        Assert.True(targetRevision.IsApproved);
        Assert.True(targetRevision.HasAutoGeneratedComments);
        // Verify only ONE upsert call (not two)
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()),
            Times.Once);
    }

    [Fact]
    public async Task CarryForwardRevisionDataAsync_DoesNotUpdate_WhenNothingToCarryForward()
    {
        var sourceRevision = new APIRevisionListItemModel
        {
            Id = "source-revision",
            ReviewId = "test-review",
            IsApproved = false,
            HasAutoGeneratedComments = false
        };

        var targetRevision = new APIRevisionListItemModel
        {
            Id = "target-revision",
            ReviewId = "test-review",
            IsApproved = false,
            HasAutoGeneratedComments = false
        };

        await _manager.CarryForwardRevisionDataAsync(targetRevision, sourceRevision);

        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()),
            Times.Never);
    }

    #endregion

    #region AutoArchiveAPIRevisions Tests

    [Fact]
    public async Task AutoArchiveAPIRevisions_PreservesLastApprovedStableRelease()
    {
        // Arrange
        var reviewId = "test-review-1";
        var oldDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(160)); // Older than 4 months
        
        // Old approved stable release - should be preserved
        var stableApprovedOld = CreateRevisionForArchiveTest(reviewId, "rev-stable-approved", "1.0.0", true, false, oldDate);
        
        // Old unapproved stable release - should be archived
        var stableUnapprovedOld = CreateRevisionForArchiveTest(reviewId, "rev-stable-unapproved", "2.0.0", false, false, oldDate);
        
        // Setup mocks
        var oldRevisions = new List<APIRevisionListItemModel> { stableApprovedOld, stableUnapprovedOld };
        var allRevisionsForReview = new List<APIRevisionListItemModel> 
        { 
            stableApprovedOld, 
            stableUnapprovedOld,
            // Add a newer revision that's not in the archive list
            CreateRevisionForArchiveTest(reviewId, "rev-new", "1.1.0", false, false, DateTime.UtcNow)
        };

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(oldRevisions);

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(reviewId))
            .ReturnsAsync(allRevisionsForReview);

        // Act
        await _manager.AutoArchiveAPIRevisions(4);

        // Assert - stableApprovedOld should NOT be archived, stableUnapprovedOld should be archived
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == stableApprovedOld.Id && r.IsDeleted)),
            Times.Never,
            "Last approved stable release should be preserved");

        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == stableUnapprovedOld.Id && r.IsDeleted)),
            Times.Once,
            "Unapproved stable release should be archived");
    }

    [Fact]
    public async Task AutoArchiveAPIRevisions_PreservesLastPreviewRelease()
    {
        // Arrange
        var reviewId = "test-review-2";
        var oldDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(150)); // Older than 4 months
        
        // Old preview releases
        var previewOld1 = CreateRevisionForArchiveTest(reviewId, "rev-preview-1", "1.0.0-beta.1", false, false, oldDate.AddDays(-10));
        var previewOld2 = CreateRevisionForArchiveTest(reviewId, "rev-preview-2", "1.0.0-beta.2", false, false, oldDate); // Latest preview
        
        // Setup mocks
        var oldRevisions = new List<APIRevisionListItemModel> { previewOld1, previewOld2 };
        var allRevisionsForReview = new List<APIRevisionListItemModel> { previewOld1, previewOld2 };

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(oldRevisions);

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(reviewId))
            .ReturnsAsync(allRevisionsForReview);

        // Act
        await _manager.AutoArchiveAPIRevisions(4);

        // Assert - previewOld2 (latest) should NOT be archived, previewOld1 should be archived
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == previewOld2.Id && r.IsDeleted)),
            Times.Never,
            "Last preview release should be preserved");

        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == previewOld1.Id && r.IsDeleted)),
            Times.Once,
            "Older preview release should be archived");
    }

    [Fact]
    public async Task AutoArchiveAPIRevisions_PreservesBothStableAndPreview()
    {
        // Arrange
        var reviewId = "test-review-3";
        var oldDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(150));
        
        var stableApproved = CreateRevisionForArchiveTest(reviewId, "rev-stable", "1.0.0", true, false, oldDate.AddDays(-20));
        var preview = CreateRevisionForArchiveTest(reviewId, "rev-preview", "2.0.0-beta.1", false, false, oldDate);
        var oldUnapproved = CreateRevisionForArchiveTest(reviewId, "rev-old", "0.9.0", false, false, oldDate.AddDays(-30));
        
        // Setup mocks
        var oldRevisions = new List<APIRevisionListItemModel> { stableApproved, preview, oldUnapproved };
        var allRevisionsForReview = new List<APIRevisionListItemModel> { stableApproved, preview, oldUnapproved };

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(oldRevisions);

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(reviewId))
            .ReturnsAsync(allRevisionsForReview);

        // Act
        await _manager.AutoArchiveAPIRevisions(4);

        // Assert - both stable and preview should be preserved, oldUnapproved should be archived
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == stableApproved.Id && r.IsDeleted)),
            Times.Never,
            "Last approved stable should be preserved");

        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == preview.Id && r.IsDeleted)),
            Times.Never,
            "Last preview should be preserved");

        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == oldUnapproved.Id && r.IsDeleted)),
            Times.Once,
            "Old unapproved revision should be archived");
    }

    [Fact]
    public async Task AutoArchiveAPIRevisions_HandlesMultipleReviews()
    {
        // Arrange
        var reviewId1 = "test-review-1";
        var reviewId2 = "test-review-2";
        var oldDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(150));
        
        // Review 1 revisions
        var review1Stable = CreateRevisionForArchiveTest(reviewId1, "rev1-stable", "1.0.0", true, false, oldDate);
        var review1Preview = CreateRevisionForArchiveTest(reviewId1, "rev1-preview", "2.0.0-beta.1", false, false, oldDate.AddDays(-5));
        
        // Review 2 revisions
        var review2Stable = CreateRevisionForArchiveTest(reviewId2, "rev2-stable", "1.5.0", true, false, oldDate);
        var review2Old = CreateRevisionForArchiveTest(reviewId2, "rev2-old", "1.0.0", false, false, oldDate.AddDays(-10));
        
        // Setup mocks
        var oldRevisions = new List<APIRevisionListItemModel> { review1Stable, review1Preview, review2Stable, review2Old };

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(oldRevisions);

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(reviewId1))
            .ReturnsAsync(new List<APIRevisionListItemModel> { review1Stable, review1Preview });

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(reviewId2))
            .ReturnsAsync(new List<APIRevisionListItemModel> { review2Stable, review2Old });

        // Act
        await _manager.AutoArchiveAPIRevisions(4);

        // Assert - preserve last approved stable and preview for each review
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == review1Stable.Id && r.IsDeleted)),
            Times.Never);

        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == review1Preview.Id && r.IsDeleted)),
            Times.Never);

        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == review2Stable.Id && r.IsDeleted)),
            Times.Never);

        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == review2Old.Id && r.IsDeleted)),
            Times.Once);
    }

    [Fact]
    public async Task AutoArchiveAPIRevisions_HandlesInvalidVersionGracefully()
    {
        // Arrange
        var reviewId = "test-review-invalid";
        var oldDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(150));
        
        var revisionWithInvalidVersion = CreateRevisionForArchiveTest(reviewId, "rev-invalid", "invalid-version", false, false, oldDate);
        
        // Setup mocks
        var oldRevisions = new List<APIRevisionListItemModel> { revisionWithInvalidVersion };
        var allRevisionsForReview = new List<APIRevisionListItemModel> { revisionWithInvalidVersion };

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(oldRevisions);

        _mockAPIRevisionsRepository
            .Setup(x => x.GetAPIRevisionsAsync(reviewId))
            .ReturnsAsync(allRevisionsForReview);

        // Act & Assert - should not throw
        await _manager.AutoArchiveAPIRevisions(4);

        // Revision with invalid version should be archived (treated as non-prerelease)
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Id == revisionWithInvalidVersion.Id && r.IsDeleted)),
            Times.Once);
    }

    private APIRevisionListItemModel CreateRevisionForArchiveTest(
        string reviewId, 
        string revisionId, 
        string packageVersion, 
        bool isApproved, 
        bool isDeleted, 
        DateTime lastUpdatedOn)
    {
        return new APIRevisionListItemModel
        {
            Id = revisionId,
            ReviewId = reviewId,
            Language = "Python",
            PackageName = "test-package",
            IsApproved = isApproved,
            IsDeleted = isDeleted,
            LastUpdatedOn = lastUpdatedOn,
            CreatedOn = lastUpdatedOn, // Set CreatedOn to same as LastUpdatedOn for tests
            APIRevisionType = APIRevisionType.Manual,
            ChangeHistory = new List<APIRevisionChangeHistoryModel>(), // Initialize ChangeHistory
            Files = new List<APICodeFileModel>
            {
                new APICodeFileModel
                {
                    FileId = $"{revisionId}-file",
                    FileName = "test.whl",
                    PackageName = "test-package",
                    PackageVersion = packageVersion,
                    VersionString = "1.0.0"
                }
            }
        };
    }

    #endregion

    #region AutoPurgeAPIRevisions Tests

    [Fact]
    public async Task AutoPurgeAPIRevisions_DeletesOnlyManualAndPullRequestRevisions()
    {
        // Arrange
        var purgeBeforeDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(180)); // 6 months ago
        
        var manualRevision1 = CreateSoftDeletedRevision("manual-rev-1", "review-1", APIRevisionType.Manual, purgeBeforeDate);
        var manualRevision2 = CreateSoftDeletedRevision("manual-rev-2", "review-2", APIRevisionType.Manual, purgeBeforeDate);
        var prRevision1 = CreateSoftDeletedRevision("pr-rev-1", "review-3", APIRevisionType.PullRequest, purgeBeforeDate);
        
        var manualRevisions = new List<APIRevisionListItemModel> { manualRevision1, manualRevision2 };
        var prRevisions = new List<APIRevisionListItemModel> { prRevision1 };
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(manualRevisions);
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.PullRequest))
            .ReturnsAsync(prRevisions);
        
        // Act
        await _manager.AutoPurgeAPIRevisions(6);
        
        // Assert - verify all revisions were deleted
        _mockAPIRevisionsRepository.Verify(
            x => x.DeleteAPIRevisionAsync(manualRevision1.Id, manualRevision1.ReviewId),
            Times.Once);
        
        _mockAPIRevisionsRepository.Verify(
            x => x.DeleteAPIRevisionAsync(manualRevision2.Id, manualRevision2.ReviewId),
            Times.Once);
        
        _mockAPIRevisionsRepository.Verify(
            x => x.DeleteAPIRevisionAsync(prRevision1.Id, prRevision1.ReviewId),
            Times.Once);
        
        // Verify code files and originals were deleted
        _mockCodeFileRepository.Verify(
            x => x.DeleteCodeFileAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(3)); // One per revision
        
        _mockOriginalsRepository.Verify(
            x => x.DeleteOriginalAsync(It.IsAny<string>()),
            Times.Exactly(3)); // Each revision has HasOriginal = true
    }

    [Fact]
    public async Task AutoPurgeAPIRevisions_DeletesAssociatedBlobs()
    {
        // Arrange
        var purgeBeforeDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(180));
        var revisionId = "test-revision-1";
        var fileId = "test-file-1";
        
        var revision = CreateSoftDeletedRevision(revisionId, "review-1", APIRevisionType.Manual, purgeBeforeDate);
        revision.Files[0].FileId = fileId;
        revision.Files[0].HasOriginal = true;
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(new List<APIRevisionListItemModel> { revision });
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.PullRequest))
            .ReturnsAsync(new List<APIRevisionListItemModel>());
        
        // Act
        await _manager.AutoPurgeAPIRevisions(6);
        
        // Assert - verify code file blob was deleted
        _mockCodeFileRepository.Verify(
            x => x.DeleteCodeFileAsync(revisionId, fileId),
            Times.Once);
        
        // Verify original blob was deleted
        _mockOriginalsRepository.Verify(
            x => x.DeleteOriginalAsync(fileId),
            Times.Once);
        
        // Verify cosmos entry was deleted
        _mockAPIRevisionsRepository.Verify(
            x => x.DeleteAPIRevisionAsync(revisionId, "review-1"),
            Times.Once);
    }

    [Fact]
    public async Task AutoPurgeAPIRevisions_ContinuesOnBlobDeletionError()
    {
        // Arrange
        var purgeBeforeDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(180));
        var revision = CreateSoftDeletedRevision("test-revision-1", "review-1", APIRevisionType.Manual, purgeBeforeDate);
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(new List<APIRevisionListItemModel> { revision });
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.PullRequest))
            .ReturnsAsync(new List<APIRevisionListItemModel>());
        
        // Setup blob deletion to throw exception
        _mockCodeFileRepository
            .Setup(x => x.DeleteCodeFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Blob not found"));
        
        // Act - should not throw
        await _manager.AutoPurgeAPIRevisions(6);
        
        // Assert - cosmos entry should still be deleted even if blob deletion failed
        _mockAPIRevisionsRepository.Verify(
            x => x.DeleteAPIRevisionAsync(revision.Id, revision.ReviewId),
            Times.Once);
    }

    [Fact]
    public async Task AutoPurgeAPIRevisions_HandlesMultipleFiles()
    {
        // Arrange
        var purgeBeforeDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(180));
        var revision = CreateSoftDeletedRevision("test-revision-1", "review-1", APIRevisionType.Manual, purgeBeforeDate);
        
        // Add multiple files
        revision.Files.Add(new APICodeFileModel
        {
            FileId = "file-2",
            FileName = "test2.whl",
            PackageName = "test-package",
            PackageVersion = "1.0.0",
            HasOriginal = true
        });
        revision.Files.Add(new APICodeFileModel
        {
            FileId = "file-3",
            FileName = "test3.whl",
            PackageName = "test-package",
            PackageVersion = "1.0.0",
            HasOriginal = false // No original for this one
        });
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(new List<APIRevisionListItemModel> { revision });
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.PullRequest))
            .ReturnsAsync(new List<APIRevisionListItemModel>());
        
        // Act
        await _manager.AutoPurgeAPIRevisions(6);
        
        // Assert - verify all code files were deleted
        _mockCodeFileRepository.Verify(
            x => x.DeleteCodeFileAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(3)); // Three files
        
        // Verify originals were deleted only for files with HasOriginal = true
        _mockOriginalsRepository.Verify(
            x => x.DeleteOriginalAsync(It.IsAny<string>()),
            Times.Exactly(2)); // Only two files have originals
    }

    [Fact]
    public async Task AutoPurgeAPIRevisions_DoesNotDeleteRecentSoftDeletedRevisions()
    {
        // Arrange - revision soft-deleted recently (1 month ago)
        var recentDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(30));
        var recentRevision = CreateSoftDeletedRevision("recent-rev-1", "review-1", APIRevisionType.Manual, recentDate);
        
        // Setup mock to return empty list (recent revisions won't be returned by the query)
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.Manual))
            .ReturnsAsync(new List<APIRevisionListItemModel>());
        
        _mockAPIRevisionsRepository
            .Setup(x => x.GetSoftDeletedAPIRevisionsAsync(It.IsAny<DateTime>(), APIRevisionType.PullRequest))
            .ReturnsAsync(new List<APIRevisionListItemModel>());
        
        // Act
        await _manager.AutoPurgeAPIRevisions(6);
        
        // Assert - verify nothing was deleted
        _mockAPIRevisionsRepository.Verify(
            x => x.DeleteAPIRevisionAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    private APIRevisionListItemModel CreateSoftDeletedRevision(
        string revisionId, 
        string reviewId, 
        APIRevisionType revisionType,
        DateTime lastUpdatedOn)
    {
        return new APIRevisionListItemModel
        {
            Id = revisionId,
            ReviewId = reviewId,
            Language = "Python",
            PackageName = "test-package",
            IsDeleted = true,
            LastUpdatedOn = lastUpdatedOn,
            CreatedOn = lastUpdatedOn.Subtract(TimeSpan.FromDays(7)),
            APIRevisionType = revisionType,
            ChangeHistory = new List<APIRevisionChangeHistoryModel>(),
            Files = new List<APICodeFileModel>
            {
                new APICodeFileModel
                {
                    FileId = $"{revisionId}-file",
                    FileName = "test.whl",
                    PackageName = "test-package",
                    PackageVersion = "1.0.0",
                    HasOriginal = true
                }
            }
        };
    }

    #endregion

    #region GetReviewQualityScoreAsync Tests

    private APIRevisionListItemModel CreateRevisionForQualityTest(string reviewId = "review-1", string revisionId = "rev-1")
    {
        return new APIRevisionListItemModel
        {
            Id = revisionId,
            ReviewId = reviewId,
            Language = "Python",
            PackageName = "test-package",
            Files = new List<APICodeFileModel>
            {
                new APICodeFileModel
                {
                    FileId = "file-1",
                    Name = "test-package (1.0.0)",
                    FileName = "test.whl",
                    Language = "Python",
                    PackageName = "test-package",
                    PackageVersion = "1.0.0",
                    VersionString = "1.0.0"
                }
            }
        };
    }

    private CommentItemModel CreateComment(
        CommentSeverity? severity,
        bool isResolved = false,
        CommentSource source = CommentSource.UserGenerated,
        float confidenceScore = 1.0f,
        string reviewId = "review-1",
        string apiRevisionId = "rev-1",
        string elementId = null,
        string threadId = null,
        DateTime? createdOn = null)
    {
        return new CommentItemModel
        {
            ReviewId = reviewId,
            APIRevisionId = apiRevisionId,
            CommentType = CommentType.APIRevision,
            Severity = severity,
            IsResolved = isResolved,
            CommentSource = source,
            ConfidenceScore = confidenceScore,
            CommentText = "Test comment",
            ElementId = elementId ?? Guid.NewGuid().ToString(),
            ThreadId = threadId,
            CreatedOn = createdOn ?? DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_NoComments_Returns100()
    {
        var revision = CreateRevisionForQualityTest();
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(new List<CommentItemModel>());

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(100, result.Score);
        Assert.Equal(0, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_OnlyResolvedComments_Returns100()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.MustFix, isResolved: true),
            CreateComment(CommentSeverity.ShouldFix, isResolved: true)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(100, result.Score);
        Assert.Equal(0, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_QuestionComments_DoNotDegradeScore()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.Question),
            CreateComment(CommentSeverity.Question),
            CreateComment(CommentSeverity.Question)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(100, result.Score);
        Assert.Equal(3, result.UnresolvedQuestionCount);
        Assert.Equal(3, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_MustFixDegradesScoreTheMost()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.MustFix)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(80, result.Score); // 100 - 20
        Assert.Equal(1, result.UnresolvedMustFixCount);
        Assert.Equal(0, result.UnresolvedMustFixDiagnostics);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_DiagnosticMustFix_DoesNotIncrementNonDiagnosticMustFixCount()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.MustFix, source: CommentSource.Diagnostic)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(80, result.Score);
        Assert.Equal(1, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.UnresolvedMustFixDiagnostics);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_ShouldFixPenalty()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.ShouldFix)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(90, result.Score); // 100 - 10
        Assert.Equal(1, result.UnresolvedShouldFixCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_SuggestionPenalty()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.Suggestion)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(100, result.Score); // Suggestions have no penalty
        Assert.Equal(1, result.UnresolvedSuggestionCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_MixedSeverities_CumulativePenalty()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.MustFix),
            CreateComment(CommentSeverity.ShouldFix),
            CreateComment(CommentSeverity.Suggestion),
            CreateComment(CommentSeverity.Question)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // 100 - 20 (MustFix) - 10 (ShouldFix) - 0 (Suggestion) - 0 (Question) = 70
        Assert.Equal(70, result.Score);
        Assert.Equal(1, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.UnresolvedShouldFixCount);
        Assert.Equal(1, result.UnresolvedSuggestionCount);
        Assert.Equal(1, result.UnresolvedQuestionCount);
        Assert.Equal(4, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_ScoreNeverBelowZero()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>();
        // 6 MustFix = 120 penalty, score should clamp to 0
        for (int i = 0; i < 6; i++)
        {
            comments.Add(CreateComment(CommentSeverity.MustFix));
        }
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(0, result.Score);
        Assert.Equal(6, result.UnresolvedMustFixCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_AIGeneratedComments_ScaledByConfidence()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            // AI-generated MustFix with 0.5 confidence → penalty = 20 * 0.5 = 10
            CreateComment(CommentSeverity.MustFix, source: CommentSource.AIGenerated, confidenceScore: 0.5f)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(90, result.Score); // 100 - (20 * 0.5) = 90
        Assert.Equal(1, result.UnresolvedMustFixCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_AIGeneratedWithZeroConfidence_NoPenalty()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.MustFix, source: CommentSource.AIGenerated, confidenceScore: 0f)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(100, result.Score);
        Assert.Equal(1, result.UnresolvedMustFixCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_UserAndAICommentsMixed()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            // User MustFix: -20
            CreateComment(CommentSeverity.MustFix),
            // AI ShouldFix with 0.8 confidence: -10 * 0.8 = -8
            CreateComment(CommentSeverity.ShouldFix, source: CommentSource.AIGenerated, confidenceScore: 0.8f),
            // User Suggestion: no penalty
            CreateComment(CommentSeverity.Suggestion)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // 100 - 20 - 8 = 72
        Assert.Equal(72, result.Score, precision: 2);
        Assert.Equal(3, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_NullSeverity_UnknownPenalty()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(severity: null)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // null severity comments receive a ShouldFix-equivalent penalty and are counted as Unknown
        Assert.Equal(90, result.Score); // 100 - 10
        Assert.Equal(1, result.UnresolvedUnknownCount);
        Assert.Equal(1, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_SampleRevisionComments_Ignored()
    {
        // SampleRevision comments are excluded at the DB level because the repository
        // is queried with CommentType.APIRevision. The mock returns an empty list to
        // reflect what the DB would actually return for that filter.
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>();
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        Assert.Equal(100, result.Score);
        Assert.Equal(0, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_DiagnosticComments_FullPenalty()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.ShouldFix, source: CommentSource.Diagnostic)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // Diagnostic comments are not AI-generated, so full penalty applies
        Assert.Equal(90, result.Score);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_InvalidRevisionId_ThrowsArgumentException()
    {
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync("bad-id")).ReturnsAsync((APIRevisionListItemModel)null);

        await Assert.ThrowsAsync<ArgumentException>(() => _manager.GetReviewQualityScoreAsync("bad-id"));
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_RepliesInSameThread_NotCountedSeparately()
    {
        var revision = CreateRevisionForQualityTest();
        var threadElement = "element-thread-1";
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comments = new List<CommentItemModel>
        {
            // Thread starter: ShouldFix
            CreateComment(CommentSeverity.ShouldFix, elementId: threadElement, createdOn: baseTime),
            // Reply 1: no severity (null)
            CreateComment(severity: null, elementId: threadElement, createdOn: baseTime.AddMinutes(5)),
            // Reply 2: no severity (null)
            CreateComment(severity: null, elementId: threadElement, createdOn: baseTime.AddMinutes(10))
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // Only the thread starter (ShouldFix) should be counted; replies are ignored
        Assert.Equal(90, result.Score); // 100 - 10
        Assert.Equal(1, result.UnresolvedShouldFixCount);
        Assert.Equal(0, result.UnresolvedQuestionCount);
        Assert.Equal(1, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_ThreadStarterSeverityUsed_NotReplySeverity()
    {
        var revision = CreateRevisionForQualityTest();
        var threadElement = "element-thread-2";
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comments = new List<CommentItemModel>
        {
            // Thread starter: Question (no penalty)
            CreateComment(CommentSeverity.Question, elementId: threadElement, createdOn: baseTime),
            // Reply with MustFix severity (should be ignored since it's not the thread starter)
            CreateComment(CommentSeverity.MustFix, elementId: threadElement, createdOn: baseTime.AddMinutes(5))
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // Only the thread starter (Question) determines severity — MustFix reply is ignored
        Assert.Equal(100, result.Score);
        Assert.Equal(1, result.UnresolvedQuestionCount);
        Assert.Equal(0, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_MultipleThreadsCountedSeparately()
    {
        var revision = CreateRevisionForQualityTest();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comments = new List<CommentItemModel>
        {
            // Thread 1: MustFix with 2 replies
            CreateComment(CommentSeverity.MustFix, elementId: "elem-1", createdOn: baseTime),
            CreateComment(severity: null, elementId: "elem-1", createdOn: baseTime.AddMinutes(1)),
            CreateComment(severity: null, elementId: "elem-1", createdOn: baseTime.AddMinutes(2)),
            // Thread 2: ShouldFix with 1 reply
            CreateComment(CommentSeverity.ShouldFix, elementId: "elem-2", createdOn: baseTime),
            CreateComment(severity: null, elementId: "elem-2", createdOn: baseTime.AddMinutes(1)),
            // Thread 3: Question alone
            CreateComment(CommentSeverity.Question, elementId: "elem-3", createdOn: baseTime)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // 3 threads: MustFix(-20), ShouldFix(-10), Question(0)
        Assert.Equal(70, result.Score); // 100 - 20 - 10
        Assert.Equal(1, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.UnresolvedShouldFixCount);
        Assert.Equal(1, result.UnresolvedQuestionCount);
        Assert.Equal(3, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_ThreadIdTakesPriorityOverElementId()
    {
        var revision = CreateRevisionForQualityTest();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comments = new List<CommentItemModel>
        {
            // Two comments with different ElementIds but same ThreadId → same thread
            CreateComment(CommentSeverity.MustFix, elementId: "elem-A", threadId: "thread-1", createdOn: baseTime),
            CreateComment(severity: null, elementId: "elem-B", threadId: "thread-1", createdOn: baseTime.AddMinutes(1)),
            // A separate thread via ThreadId
            CreateComment(CommentSeverity.Suggestion, elementId: "elem-C", threadId: "thread-2", createdOn: baseTime)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // 2 threads: MustFix(-20) and Suggestion(0)
        Assert.Equal(80, result.Score); // 100 - 20
        Assert.Equal(1, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.UnresolvedSuggestionCount);
        Assert.Equal(2, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_ElementIdFallbackWhenNoThreadId()
    {
        var revision = CreateRevisionForQualityTest();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comments = new List<CommentItemModel>
        {
            // Legacy comments: no ThreadId, grouped by ElementId
            CreateComment(CommentSeverity.ShouldFix, elementId: "legacy-elem-1", threadId: null, createdOn: baseTime),
            CreateComment(severity: null, elementId: "legacy-elem-1", threadId: null, createdOn: baseTime.AddMinutes(1)),
            CreateComment(CommentSeverity.MustFix, elementId: "legacy-elem-2", threadId: null, createdOn: baseTime)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // 2 threads: ShouldFix(-10) and MustFix(-20)
        Assert.Equal(70, result.Score); // 100 - 10 - 20
        Assert.Equal(1, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.UnresolvedShouldFixCount);
        Assert.Equal(2, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_CrossRevisionComments_IncludedInScore()
    {
        var revision = CreateRevisionForQualityTest(revisionId: "rev-2");
        var comments = new List<CommentItemModel>
        {
            // Comment from the current revision
            CreateComment(CommentSeverity.MustFix, apiRevisionId: "rev-2"),
            // Unresolved comment from a different revision — should still count (visible)
            CreateComment(CommentSeverity.ShouldFix, apiRevisionId: "rev-1"),
            // Resolved comment from a different revision — should NOT count
            CreateComment(CommentSeverity.MustFix, apiRevisionId: "rev-1", isResolved: true)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // Both unresolved comments count regardless of which revision they were created on
        // 100 - 20 (MustFix) - 10 (ShouldFix) = 70
        Assert.Equal(70, result.Score);
        Assert.Equal(1, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.UnresolvedShouldFixCount);
        Assert.Equal(2, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_SyncsDiagnosticsBeforeScoring()
    {
        var revision = CreateRevisionForQualityTest();
        revision.DiagnosticsHash = null;
        var diagnostics = new[] { new CodeDiagnostic { TargetId = "elem-1", Text = "Missing docs", Level = CodeDiagnosticLevel.Warning } };
        var codeFile = new CodeFile
        {
            Name = "test",
            Language = "Python",
            PackageName = "test-package",
            PackageVersion = "1.0.0",
            Diagnostics = diagnostics
        };
        var renderedCodeFile = new RenderedCodeFile(codeFile);

        var syncedComment = CreateComment(CommentSeverity.ShouldFix, source: CommentSource.Diagnostic);
        var syncResult = new DiagnosticSyncResult
        {
            Comments = new List<CommentItemModel> { syncedComment },
            DiagnosticsHash = "new-hash",
            WasSynced = true
        };

        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCodeFileRepository.Setup(x => x.GetCodeFileAsync(revision, false)).ReturnsAsync(renderedCodeFile);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(new List<CommentItemModel>());
        _mockDiagnosticCommentService.Setup(x => x.SyncDiagnosticCommentsAsync(
            revision.ReviewId, revision.Id, revision.DiagnosticsHash, diagnostics, It.IsAny<IEnumerable<CommentItemModel>>()))
            .ReturnsAsync(syncResult);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // Synced diagnostic (ShouldFix) should be scored
        Assert.Equal(90, result.Score); // 100 - 10
        Assert.Equal(1, result.UnresolvedShouldFixCount);
        _mockDiagnosticCommentService.Verify(x => x.SyncDiagnosticCommentsAsync(
            revision.ReviewId, revision.Id, null, diagnostics, It.IsAny<IEnumerable<CommentItemModel>>()), Times.Once);
        _mockAPIRevisionsRepository.Verify(x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.DiagnosticsHash == "new-hash")), Times.Once);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_OldStyleNoThreadId_ResolvedByIndividualComment()
    {
        // Old-style comments have no ThreadId. Each comment is grouped by ElementId,
        // so each unique ElementId is a standalone "thread". Resolution is determined
        // by the individual comment's IsResolved flag.
        var revision = CreateRevisionForQualityTest();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comments = new List<CommentItemModel>
        {
            // Resolved old-style comment — should NOT be counted
            CreateComment(CommentSeverity.MustFix, elementId: "old-elem-1", threadId: null, isResolved: true, createdOn: baseTime),
            // Unresolved old-style comment — should be counted
            CreateComment(CommentSeverity.ShouldFix, elementId: "old-elem-2", threadId: null, isResolved: false, createdOn: baseTime),
            // Another resolved — should NOT be counted
            CreateComment(CommentSeverity.MustFix, elementId: "old-elem-3", threadId: null, isResolved: true, createdOn: baseTime)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // Only old-elem-2 (ShouldFix, unresolved) counts
        Assert.Equal(90, result.Score); // 100 - 10
        Assert.Equal(0, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.UnresolvedShouldFixCount);
        Assert.Equal(1, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_NewStyleWithThreadId_MixedResolution_TreatedAsResolved()
    {
        // New-style comments share a ThreadId. ResolveConversation marks every comment
        // in the thread as IsResolved=true. If a reply is later added (e.g., agent reply),
        // that new comment defaults to IsResolved=false, creating a mixed state.
        // The thread should still be treated as resolved because the thread was explicitly
        // resolved — matching the conversations panel behavior (any resolved → thread resolved).
        var revision = CreateRevisionForQualityTest();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comments = new List<CommentItemModel>
        {
            // Original comment — resolved when thread was resolved
            CreateComment(CommentSeverity.MustFix, elementId: "elem-1", threadId: "thread-1", isResolved: true, createdOn: baseTime),
            // Reply added after resolution — IsResolved defaults to false
            CreateComment(severity: null, elementId: "elem-1", threadId: "thread-1", isResolved: false, createdOn: baseTime.AddMinutes(5)),
            // A genuinely unresolved thread for comparison
            CreateComment(CommentSeverity.ShouldFix, elementId: "elem-2", threadId: "thread-2", createdOn: baseTime)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // thread-1 has a resolved comment → entire thread is resolved → not counted
        // Only thread-2 (ShouldFix) counts
        Assert.Equal(90, result.Score); // 100 - 10
        Assert.Equal(0, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.UnresolvedShouldFixCount);
        Assert.Equal(1, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_NewStyleWithThreadId_AllUnresolved_CountedAsActive()
    {
        // New-style thread where no comment has been resolved — the thread is active.
        var revision = CreateRevisionForQualityTest();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.MustFix, elementId: "elem-1", threadId: "thread-1", isResolved: false, createdOn: baseTime),
            CreateComment(severity: null, elementId: "elem-1", threadId: "thread-1", isResolved: false, createdOn: baseTime.AddMinutes(5)),
            CreateComment(severity: null, elementId: "elem-1", threadId: "thread-1", isResolved: false, createdOn: baseTime.AddMinutes(10))
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // All unresolved in thread-1 → thread is active, severity from first comment (MustFix)
        Assert.Equal(80, result.Score); // 100 - 20
        Assert.Equal(1, result.UnresolvedMustFixCount);
        Assert.Equal(1, result.TotalUnresolvedCount);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_NormalizesTimestamps_BeforeSelectingRepresentative()
    {
        // Simulate the DateTime.Now vs DateTime.UtcNow bug:
        // Architect posts MustFix at 10:00 UTC, user replies (no severity) 5 min later
        // but stored with DateTime.Now (Kind=Local). Without normalization the local
        // face value may sort before the architect's comment, picking the wrong
        // thread representative. After normalization both are UTC and the MustFix
        // comment is correctly selected as the first in the thread.
        var revision = CreateRevisionForQualityTest();
        var architectUtcTime = new DateTime(2026, 3, 25, 10, 0, 0, DateTimeKind.Utc);
        // Construct the local-time equivalent of 10:05 UTC (what DateTime.Now returned)
        var userReplyActualUtc = new DateTime(2026, 3, 25, 10, 5, 0, DateTimeKind.Utc);
        var userLocalTime = userReplyActualUtc.ToLocalTime();

        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.MustFix, elementId: "elem-1", threadId: "thread-1",
                createdOn: architectUtcTime),
            CreateComment(severity: null, elementId: "elem-1", threadId: "thread-1",
                createdOn: userLocalTime)
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        var result = await _manager.GetReviewQualityScoreAsync(revision.Id);

        // After normalization, the MustFix (10:00 UTC) is the first comment in the thread,
        // so the thread gets the MustFix severity penalty.
        Assert.Equal(80, result.Score); // 100 - 20 (MustFix penalty)
        Assert.Equal(1, result.UnresolvedMustFixCount);

        // Verify the non-UTC comment was persisted back to fix it in the DB
        _mockCommentsRepository.Verify(
            r => r.UpsertCommentAsync(It.Is<CommentItemModel>(c => c.CreatedOn.Kind == DateTimeKind.Utc)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetReviewQualityScoreAsync_AllUtcTimestamps_SkipsNormalization()
    {
        var revision = CreateRevisionForQualityTest();
        var comments = new List<CommentItemModel>
        {
            CreateComment(CommentSeverity.ShouldFix, elementId: "elem-1",
                createdOn: new DateTime(2026, 3, 25, 10, 0, 0, DateTimeKind.Utc))
        };
        _mockAPIRevisionsRepository.Setup(x => x.GetAPIRevisionAsync(revision.Id)).ReturnsAsync(revision);
        _mockCommentsRepository.Setup(x => x.GetCommentsAsync(revision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(comments);

        await _manager.GetReviewQualityScoreAsync(revision.Id);

        // All timestamps are UTC — no UpsertCommentAsync calls for normalization
        _mockCommentsRepository.Verify(
            r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Never);
    }

    #endregion

    #region AreAPIRevisionsTheSame Tests

    private static APIRevisionListItemModel CreateRevisionWithContentHash(string contentHash) =>
        new()
        {
            Id = "revision-id",
            ReviewId = "review-id",
            Files = new List<APICodeFileModel>
            {
                new() { FileId = "file-id", FileName = "test_python.json", ContentHash = contentHash }
            }
        };

    private static RenderedCodeFile CreateSimpleRenderedCodeFile() =>
        new(new CodeFile
        {
            Name = "test_python.json",
            Language = "Python",
            PackageName = "test-package",
            PackageVersion = "1.0.0"
        });

    [Fact]
    public async Task AreAPIRevisionsTheSame_FastPath_ReturnsTrue_WhenHashesMatch()
    {
        RenderedCodeFile renderedCodeFile = CreateSimpleRenderedCodeFile();
        APIRevisionListItemModel revision = CreateRevisionWithContentHash("test-hash-abc");

        _mockCodeFileManager
            .Setup(x => x.ComputeAPIContentHashAsync(renderedCodeFile.CodeFile))
            .ReturnsAsync("test-hash-abc");

        bool result = await _manager.AreAPIRevisionsTheSame(revision, renderedCodeFile, incomingContentHash: "test-hash-abc");

        Assert.True(result);
        _mockCodeFileRepository.Verify(
            x => x.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<bool>()),
            Times.Never,
            "Fast path should not download the blob");
    }

    [Fact]
    public async Task AreAPIRevisionsTheSame_HashMismatch_ReturnsFalseWithoutBlobDownload()
    {
     
        APIRevisionListItemModel revision = CreateRevisionWithContentHash("stored-hash");
        RenderedCodeFile renderedCodeFile = CreateSimpleRenderedCodeFile();

        _mockCodeFileManager
            .Setup(x => x.ComputeAPIContentHashAsync(renderedCodeFile.CodeFile))
            .ReturnsAsync("different-hash");

        bool result = await _manager.AreAPIRevisionsTheSame(revision, renderedCodeFile);

        Assert.False(result);
        _mockCodeFileRepository.Verify(x => x.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<bool>()),
            Times.Never,
            "Hash mismatch is a definitive 'different' — no blob download required");
    }

    [Fact]
    public async Task AreAPIRevisionsTheSame_FastPath_AutoComputesHash_WhenNotProvided()
    {
        APIRevisionListItemModel revision = CreateRevisionWithContentHash("test-hash-abc");
        RenderedCodeFile renderedCodeFile = CreateSimpleRenderedCodeFile();

        _mockCodeFileManager
            .Setup(x => x.ComputeAPIContentHashAsync(renderedCodeFile.CodeFile))
            .ReturnsAsync("test-hash-abc");

        // No incomingContentHash supplied — manager should compute it from the renderedCodeFile
        bool result = await _manager.AreAPIRevisionsTheSame(revision, renderedCodeFile);

        Assert.True(result);
        _mockCodeFileRepository.Verify(
            x => x.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<bool>()),
            Times.Never, "Should not download blob when storedHash is set");
    }

    [Fact]
    public async Task AreAPIRevisionsTheSame_SlowPath_BackfillsHash_WhenStoredHashIsNull()
    {
        APIRevisionListItemModel revision = CreateRevisionWithContentHash(null);
        RenderedCodeFile blobCodeFile = CreateSimpleRenderedCodeFile();
        RenderedCodeFile incomingRenderedCodeFile = CreateSimpleRenderedCodeFile();

        _mockCodeFileRepository
            .Setup(x => x.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), false))
            .ReturnsAsync(blobCodeFile);

        _mockCodeFileManager
            .Setup(x => x.AreAPICodeFilesTheSame(blobCodeFile, incomingRenderedCodeFile))
            .Returns(true);

        _mockCodeFileManager
            .Setup(x => x.ComputeAPIContentHashAsync(blobCodeFile.CodeFile))
            .ReturnsAsync("backfilled-hash");

        _mockAPIRevisionsRepository
            .Setup(x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        bool result = await _manager.AreAPIRevisionsTheSame(revision, incomingRenderedCodeFile);

        Assert.True(result);
        Assert.Equal("backfilled-hash", revision.Files[0].ContentHash);
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.Is<APIRevisionListItemModel>(r => r.Files[0].ContentHash == "backfilled-hash")),
            Times.Once,
            "Lazy backfill should persist the hash so future calls use the fast path");
    }

    [Fact]
    public async Task AreAPIRevisionsTheSame_SlowPath_ReturnsFalse_WhenBlobThrowsJsonException()
    {
        APIRevisionListItemModel revision = CreateRevisionWithContentHash(null);
        RenderedCodeFile renderedCodeFile = CreateSimpleRenderedCodeFile();

        _mockCodeFileRepository
            .Setup(x => x.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), false))
            .ThrowsAsync(new JsonException("corrupt blob"));

        bool result = await _manager.AreAPIRevisionsTheSame(revision, renderedCodeFile);

        Assert.False(result);
        _mockAPIRevisionsRepository.Verify(
            x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()),
            Times.Never,
            "Should not upsert when blob read fails");
    }

    [Fact]
    public async Task AreAPIRevisionsTheSame_ReturnsTrue_WhenSameApiSurface_AndPackageVersionNotChecked()
    {
        // Stored revision has PackageVersion "1.0.0"; incoming has "2.0.0".
        var revisionFile = new APICodeFileModel
        {
            FileId = "file-id",
            FileName = "test_python.json",
            ContentHash = "api-surface-hash",
            PackageVersion = "1.0.0"
        };
        var revision = new APIRevisionListItemModel
        {
            Id = "revision-id", ReviewId = "review-id", Files = new List<APICodeFileModel> { revisionFile }
        };
        var incomingFile = new RenderedCodeFile(new CodeFile
        {
            Name = "test_python.json",
            Language = "Python",
            PackageName = "test-package",
            PackageVersion = "2.0.0" // different version, same surface
        });

        _mockCodeFileManager
            .Setup(x => x.ComputeAPIContentHashAsync(incomingFile.CodeFile))
            .ReturnsAsync("api-surface-hash"); // same hash because surface is identical

        bool result = await _manager.AreAPIRevisionsTheSame(revision, incomingFile, false);

        Assert.True(result);
        _mockCodeFileRepository.Verify(
            x => x.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<bool>()),
            Times.Never,
            "Version-only difference should not trigger a blob download");
    }

    [Fact]
    public async Task
        AreAPIRevisionsTheSame_ReturnsFalse_WhenSameApiSurface_AndPackageVersionDiffers_WithConsiderVersion()
    {
        var revisionFile = new APICodeFileModel
        {
            FileId = "file-id",
            FileName = "test_python.json",
            ContentHash = "api-surface-hash",
            PackageVersion = "1.0.0"
        };
        var revision = new APIRevisionListItemModel
        {
            Id = "revision-id", ReviewId = "review-id", Files = new List<APICodeFileModel> { revisionFile }
        };
        var incomingFile = new RenderedCodeFile(new CodeFile
        {
            Name = "test_python.json", Language = "Python", PackageName = "test-package", PackageVersion = "2.0.0"
        });

        _mockCodeFileManager
            .Setup(x => x.ComputeAPIContentHashAsync(incomingFile.CodeFile))
            .ReturnsAsync("api-surface-hash");

        bool result = await _manager.AreAPIRevisionsTheSame(revision, incomingFile, true);

        Assert.False(result);
        _mockCodeFileRepository.Verify(
            x => x.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<bool>()),
            Times.Never);
    }

    #endregion

    #region CreateAPIRevisionAsync_PreParsed Tests

    [Fact]
    public async Task CreateAPIRevisionAsync_WithPreParsedCodeFile_DoesNotParseAgain()
    {
        // Arrange
        var user = CreateTestUser("testuser");
        var review = new ReviewListItemModel
        {
            Id = "review-1",
            PackageName = "test-package",
            Language = "Python",
            ChangeHistory = new List<ReviewChangeHistoryModel>()
        };

        var preParsedCodeFile = new CodeFile
        {
            Name = "test.whl",
            Language = "Python",
            PackageName = "test-package",
            PackageVersion = "1.0.0"
        };

        var preParsedMemoryStream = new MemoryStream();
        var codeFileModel = new APICodeFileModel { FileId = "file-1", Language = "Python" };

        SetupSignalRMocks();

        // Setup: CreateReviewCodeFileModel should be called with the pre-parsed data
        _mockCodeFileManager
            .Setup(m => m.CreateReviewCodeFileModel(It.IsAny<string>(), preParsedMemoryStream, preParsedCodeFile))
            .ReturnsAsync(codeFileModel);

        _mockDiagnosticCommentService
            .Setup(d => d.SyncDiagnosticCommentsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CodeDiagnostic[]>(), It.IsAny<List<CommentItemModel>>()))
            .ReturnsAsync(new DiagnosticSyncResult());

        _mockAPIRevisionsRepository
            .Setup(x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.CreateAPIRevisionAsync(
            user: user, review: review, file: null, filePath: "test.whl",
            language: "Python", label: "test",
            preParsedCodeFile: preParsedCodeFile, preParsedMemoryStream: preParsedMemoryStream);

        // Assert: CreateReviewCodeFileModel was called (reusing pre-parsed data)
        _mockCodeFileManager.Verify(
            m => m.CreateReviewCodeFileModel(It.IsAny<string>(), preParsedMemoryStream, preParsedCodeFile),
            Times.Once);

        // Assert: CreateCodeFileAsync was NOT called (no re-parsing)
        _mockCodeFileManager.Verify(
            m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAPIRevisionAsync_WithoutPreParsedCodeFile_ParsesFile()
    {
        // Arrange
        var user = CreateTestUser("testuser");
        var review = new ReviewListItemModel
        {
            Id = "review-1",
            PackageName = "test-package",
            Language = "Python",
            ChangeHistory = new List<ReviewChangeHistoryModel>()
        };

        var codeFileModel = new APICodeFileModel { FileId = "file-1", Language = "Python" };
        var codeFile = new CodeFile { Name = "test.whl", Language = "Python", PackageName = "test-package" };

        SetupSignalRMocks();

        // Setup: CreateCodeFileAsync should be called (normal parsing path)
        _mockCodeFileManager
            .Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), "test.whl", true, null, "Python"))
            .ReturnsAsync(codeFileModel);

        _mockCodeFileRepository
            .Setup(m => m.GetCodeFileFromStorageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(codeFile);

        _mockDiagnosticCommentService
            .Setup(d => d.SyncDiagnosticCommentsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CodeDiagnostic[]>(), It.IsAny<List<CommentItemModel>>()))
            .ReturnsAsync(new DiagnosticSyncResult());

        _mockAPIRevisionsRepository
            .Setup(x => x.UpsertAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _manager.CreateAPIRevisionAsync(
            user: user, review: review, file: null, filePath: "test.whl",
            language: "Python", label: "test");

        // Assert: CreateCodeFileAsync WAS called (normal path)
        _mockCodeFileManager.Verify(
            m => m.CreateCodeFileAsync(It.IsAny<string>(), "test.whl", true, null, "Python"),
            Times.Once);

        // Assert: CreateReviewCodeFileModel was NOT called directly
        _mockCodeFileManager.Verify(
            m => m.CreateReviewCodeFileModel(It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>()),
            Times.Never);
    }

    #endregion
}
