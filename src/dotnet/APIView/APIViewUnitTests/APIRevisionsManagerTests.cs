using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
    private readonly Mock<IDiagnosticCommentService> _mockDiagnosticCommentService;
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
        _mockDiagnosticCommentService = new Mock<IDiagnosticCommentService>();
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
            _mockDiagnosticCommentService.Object,
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
}
