using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class TypeSpecMetadataIntegrationTests
{
    private readonly CodeFileManager _codeFileManager;
    private readonly Mock<IBlobCodeFileRepository> _mockCodeFileRepository;
    private readonly Mock<IDevopsArtifactRepository> _mockDevopsArtifactRepository;
    private readonly Mock<IBlobOriginalsRepository> _mockOriginalsRepository;
    private readonly Mock<ILogger<ProjectsManager>> _mockProjectsLogger;
    private readonly Mock<ICosmosProjectRepository> _mockProjectsRepository;
    private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
    private readonly ProjectsManager _projectsManager;

    public TypeSpecMetadataIntegrationTests()
    {
        _mockProjectsRepository = new Mock<ICosmosProjectRepository>();
        _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
        _mockDevopsArtifactRepository = new Mock<IDevopsArtifactRepository>();
        _mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();
        _mockOriginalsRepository = new Mock<IBlobOriginalsRepository>();
        _mockProjectsLogger = new Mock<ILogger<ProjectsManager>>();

        List<LanguageService> languageServices = new();

        var mockCodeFileManagerLogger = new Mock<ILogger<CodeFileManager>>();
        _codeFileManager = new CodeFileManager(
            languageServices,
            _mockCodeFileRepository.Object,
            _mockOriginalsRepository.Object,
            _mockDevopsArtifactRepository.Object,
            mockCodeFileManagerLogger.Object);

        _projectsManager = new ProjectsManager(
            _mockProjectsRepository.Object,
            _mockReviewsRepository.Object,
            _mockProjectsLogger.Object);
    }

    [Fact]
    public async Task EndToEnd_TypeSpecArtifact_ExtractsMetadataAndCreatesProject()
    {
        TypeSpecMetadata metadata = new()
        {
            EmitterVersion = "0.7.2",
            GeneratedAt = DateTime.UtcNow,
            TypeSpec =
                new TypeSpecInfo
                {
                    Namespace = "Azure.Analytics.Purview",
                    Documentation = "Azure Purview Analytics client library",
                    Type = "client"
                },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["Python"] =
                    new()
                    {
                        EmitterName = "@azure-tools/typespec-python",
                        PackageName = "azure-purview-analytics",
                        Namespace = "azure.purview.analytics"
                    },
                ["JavaScript"] =
                    new()
                    {
                        EmitterName = "@azure-tools/typespec-ts",
                        PackageName = "@azure/purview-analytics",
                        Namespace = "@azure/purview-analytics"
                    },
                ["Java"] = new()
                {
                    EmitterName = "@azure-tools/typespec-java",
                    PackageName = "com.azure.analytics.purview",
                    Namespace = "com.azure.analytics.purview"
                },
                ["DotNet"] = new()
                {
                    EmitterName = "@azure-tools/typespec-csharp",
                    PackageName = "Azure.Analytics.Purview",
                    Namespace = "Azure.Analytics.Purview"
                }
            }
        };

        MemoryStream zipStream = CreateTypeSpecArtifactZip(
            "Azure.Analytics.Purview.New.json",
            "Azure.Analytics.Purview",
            "Azure.Analytics.Purview",
            metadata);

        _mockDevopsArtifactRepository
            .Setup(r => r.DownloadPackageArtifact(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(zipStream);

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        // Extract from artifact
        using MemoryStream originalFileStream = new();
        CodeFileResult codeFileResult = await _codeFileManager.GetCodeFileAsync(
            "Azure/azure-rest-api-specs",
            "12345",
            "typeSpecAPIViewArtifacts",
            "Azure.Analytics.Purview",
            null,
            "Azure.Analytics.Purview.New.json",
            originalFileStream,
            metadataFileName: "typespec-metadata.json");

        Assert.NotNull(codeFileResult);
        Assert.NotNull(codeFileResult.CodeFile);
        Assert.NotNull(codeFileResult.Metadata);
        Assert.Equal("Azure.Analytics.Purview", codeFileResult.Metadata.TypeSpec.Namespace);
        Assert.Equal(4, codeFileResult.Metadata.Languages.Count);

        // Create TypeSpec review
        ReviewListItemModel typeSpecReview = new()
        {
            Id = "typespec-review-1",
            Language = "TypeSpec",
            PackageName = codeFileResult.CodeFile.PackageName,
            CrossLanguagePackageId = codeFileResult.CodeFile.CrossLanguagePackageId,
            ProjectId = null
        };

        // Create project from metadata
        Project project =
            await _projectsManager.UpsertProjectFromMetadataAsync("user", codeFileResult.Metadata, typeSpecReview);

        Assert.NotNull(project);
        Assert.NotNull(capturedProject);
        Assert.Equal("Azure.Analytics.Purview", capturedProject.Namespace);
        Assert.Equal("Azure Purview Analytics client library", capturedProject.Description);
        Assert.Equal(4, capturedProject.ExpectedPackages.Count);

        Assert.Equal("azure-purview-analytics", capturedProject.ExpectedPackages["Python"].PackageName);
        Assert.Equal("@azure/purview-analytics", capturedProject.ExpectedPackages["JavaScript"].PackageName);
        Assert.Equal("com.azure.analytics.purview", capturedProject.ExpectedPackages["Java"].PackageName);
        Assert.Equal("Azure.Analytics.Purview", capturedProject.ExpectedPackages["DotNet"].PackageName);

        Assert.Equal(capturedProject.Id, typeSpecReview.ProjectId);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(typeSpecReview), Times.Once);
    }

    [Fact]
    public async Task EndToEnd_LinkLanguageReviewToProject_ViaExpectedPackage()
    {
        Project existingProject = new()
        {
            Id = "project-azure-core",
            CrossLanguagePackageId = "Azure.Core",
            Namespace = "Azure.Core",
            ExpectedPackages = new Dictionary<string, PackageInfo>
            {
                ["Python"] = new() { PackageName = "azure-core", Namespace = "azure.core" },
                ["JavaScript"] =
                    new() { PackageName = "@azure/core-rest-pipeline", Namespace = "@azure/core-rest-pipeline" }
            },
            ReviewIds = new HashSet<string>(),
            ChangeHistory = new List<ProjectChangeHistory>()
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectByCrossLanguagePackageIdAsync(It.IsAny<string>()))
            .ReturnsAsync((Project)null);

        _mockProjectsRepository
            .Setup(r => r.GetProjectByExpectedPackageAsync("Python", "azure-core"))
            .ReturnsAsync(existingProject);

        ReviewListItemModel pythonReview = new()
        {
            Id = "python-review-azure-core",
            Language = "Python",
            PackageName = "azure-core",
            CrossLanguagePackageId = null
        };

        Project linkedProject = await _projectsManager.TryLinkReviewToProjectAsync("user", pythonReview);

        Assert.NotNull(linkedProject);
        Assert.Equal("project-azure-core", linkedProject.Id);
        Assert.Equal("project-azure-core", pythonReview.ProjectId);
        Assert.Contains("python-review-azure-core", linkedProject.ReviewIds);

        _mockProjectsRepository.Verify(r => r.GetProjectByExpectedPackageAsync("Python", "azure-core"), Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(pythonReview), Times.Once);
    }

    [Fact]
    public async Task EndToEnd_UpdateProjectWhenMetadataChanges()
    {
        Project existingProject = new()
        {
            Id = "existing-project-id",
            CrossLanguagePackageId = "Azure.Storage",
            Namespace = "Azure.Storage.Old",
            Description = "Old description",
            ExpectedPackages = new Dictionary<string, PackageInfo>
            {
                ["Python"] = new() { PackageName = "azure-storage-old", Namespace = "azure.storage" }
            },
            ChangeHistory = new List<ProjectChangeHistory>(),
            ReviewIds = new HashSet<string>()
        };

        ReviewListItemModel typeSpecReview = new()
        {
            Id = "typespec-review-1",
            Language = "TypeSpec",
            PackageName = "Azure.Storage",
            CrossLanguagePackageId = "Azure.Storage",
            ProjectId = "existing-project-id"
        };

        TypeSpecMetadata updatedMetadata = new()
        {
            TypeSpec = new TypeSpecInfo
            {
                Namespace = "Azure.Storage.Blobs", // Changed
                Documentation = "New blob storage documentation" // Changed
            },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["Python"] = new() { PackageName = "azure-storage-blob", Namespace = "azure.storage.blob" } // Changed
            }
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectAsync("existing-project-id"))
            .ReturnsAsync(existingProject);

        Project updatedProject = await _projectsManager.UpsertProjectFromMetadataAsync("user", updatedMetadata, typeSpecReview);

        Assert.NotNull(updatedProject);
        Assert.Equal("Azure.Storage.Blobs", updatedProject.Namespace);
        Assert.Equal("New blob storage documentation", updatedProject.Description);
        Assert.Equal("azure-storage-blob", updatedProject.ExpectedPackages["Python"].PackageName);

        Assert.Single(updatedProject.ChangeHistory);
        Assert.Equal(ProjectChangeAction.Edited, updatedProject.ChangeHistory[0].ChangeAction);
        Assert.Contains("Namespace", updatedProject.ChangeHistory[0].Notes);
        Assert.Contains("Description", updatedProject.ChangeHistory[0].Notes);
        Assert.Contains("ExpectedPackages", updatedProject.ChangeHistory[0].Notes);

        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(existingProject), Times.Once);
    }

    #region Helper Methods

    private static MemoryStream CreateTypeSpecArtifactZip(
        string codeFileName,
        string packageName,
        string crossLanguagePackageId,
        TypeSpecMetadata metadata)
    {
        MemoryStream zipStream = new();

        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
        {
            CodeFile codeFile = new()
            {
                PackageName = packageName,
                Language = "TypeSpec",
                VersionString = "1.0.0",
                CrossLanguagePackageId = crossLanguagePackageId
            };
            AddJsonEntry(archive, codeFileName, codeFile);
            AddJsonEntry(archive, "typespec-metadata.json", metadata);
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
