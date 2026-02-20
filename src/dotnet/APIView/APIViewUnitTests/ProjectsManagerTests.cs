using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class ProjectsManagerTests
{
    private readonly Mock<ILogger<ProjectsManager>> _mockLogger;
    private readonly Mock<ICosmosProjectRepository> _mockProjectsRepository;
    private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
    private readonly ProjectsManager _projectsManager;

    public ProjectsManagerTests()
    {
        _mockProjectsRepository = new Mock<ICosmosProjectRepository>();
        _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
        _mockLogger = new Mock<ILogger<ProjectsManager>>();

        _projectsManager = new ProjectsManager(
            _mockProjectsRepository.Object,
            _mockReviewsRepository.Object,
            _mockLogger.Object);
    }

    #region TryLinkReviewToProjectAsync Tests

    [Fact]
    public async Task TryLinkReviewToProjectAsync_FindsProjectByCrossLanguagePackageId_LinksReview()
    {
        ReviewListItemModel review = new()
        {
            Id = "review-1", Language = "Python", PackageName = "azure-core", CrossLanguagePackageId = "Azure.Core"
        };

        Project existingProject = new()
        {
            Id = "project-1",
            CrossLanguagePackageId = "Azure.Core",
            ReviewIds = new HashSet<string>(),
            ChangeHistory = new List<ProjectChangeHistory>()
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectByCrossLanguagePackageIdAsync("Azure.Core"))
            .ReturnsAsync(existingProject);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        Assert.Equal("project-1", review.ProjectId);
        Assert.Equal("project-1", result.Id);
        Assert.Contains("review-1", result.ReviewIds);
        Assert.Single(result.ChangeHistory);
        Assert.Equal(ProjectChangeAction.ReviewLinked, result.ChangeHistory[0].ChangeAction);

        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(existingProject), Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(review), Times.Once);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_FindsProjectByExpectedPackage_LinksReview()
    {
        ReviewListItemModel review = new()
        {
            Id = "review-1",
            Language = "Python",
            PackageName = "azure-core",
            CrossLanguagePackageId = null // No CrossLanguagePackageId
        };

        Project existingProject = new()
        {
            Id = "project-1",
            CrossLanguagePackageId = "Azure.Core",
            ExpectedPackages =
                new Dictionary<string, PackageInfo> { ["Python"] = new() { PackageName = "azure-core" } },
            ReviewIds = new HashSet<string>(),
            ChangeHistory = new List<ProjectChangeHistory>()
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectByCrossLanguagePackageIdAsync(It.IsAny<string>()))
            .ReturnsAsync((Project)null);

        _mockProjectsRepository
            .Setup(r => r.GetProjectByExpectedPackageAsync("Python", "azure-core"))
            .ReturnsAsync(existingProject);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        Assert.Equal("project-1", review.ProjectId);
        Assert.Equal("project-1", result.Id);
        Assert.Contains("review-1", result.ReviewIds);

        _mockProjectsRepository.Verify(r => r.GetProjectByExpectedPackageAsync("Python", "azure-core"), Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(review), Times.Once);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_NoProjectFound_ReturnsNull()
    {
        ReviewListItemModel review = new()
        {
            Id = "review-1",
            Language = "Python",
            PackageName = "azure-unknown-package",
            CrossLanguagePackageId = "Azure.Unknown"
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectByCrossLanguagePackageIdAsync("Azure.Unknown"))
            .ReturnsAsync((Project)null);

        _mockProjectsRepository
            .Setup(r => r.GetProjectByExpectedPackageAsync("Python", "azure-unknown-package"))
            .ReturnsAsync((Project)null);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.Null(result);
        Assert.Null(review.ProjectId);

        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()), Times.Never);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_ReviewAlreadyLinked_DoesNotDuplicateLink()
    {
        ReviewListItemModel review = new()
        {
            Id = "review-1", Language = "Python", PackageName = "azure-core", CrossLanguagePackageId = "Azure.Core"
        };

        Project existingProject = new()
        {
            Id = "project-1",
            CrossLanguagePackageId = "Azure.Core",
            ReviewIds = new HashSet<string> { "review-1" }, // Already linked
            ChangeHistory = new List<ProjectChangeHistory>()
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectByCrossLanguagePackageIdAsync("Azure.Core"))
            .ReturnsAsync(existingProject);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        Assert.Single(result.ReviewIds); // Still only one
        Assert.Empty(result.ChangeHistory); // No new change history

        // Project should not be updated since nothing changed
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
        // But review should still be updated with ProjectId
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(review), Times.Once);
    }

    #endregion

    #region UpsertProjectFromMetadataAsync Tests

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_NewProject_CreatesProject()
    {
        ReviewListItemModel typeSpecReview = new()
        {
            Id = "typespec-review-1",
            Language = "TypeSpec",
            PackageName = "Azure.Storage",
            CrossLanguagePackageId = "Azure.Storage",
            ProjectId = null // No existing project
        };

        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Storage", Documentation = "Azure Storage TypeSpec" },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["Python"] = new() { Namespace = "azure.storage", PackageName = "azure-storage" },
                ["JavaScript"] = new() { Namespace = "@azure/storage", PackageName = "@azure/storage" }
            }
        };

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(result);
        Assert.NotNull(capturedProject);
        Assert.Equal("Azure.Storage", capturedProject.CrossLanguagePackageId);
        Assert.Equal("Azure.Storage", capturedProject.Namespace);
        Assert.Equal("Azure Storage TypeSpec", capturedProject.Description);
        Assert.Equal(2, capturedProject.ExpectedPackages.Count);
        Assert.Equal("azure-storage", capturedProject.ExpectedPackages["Python"].PackageName);
        Assert.Equal("@azure/storage", capturedProject.ExpectedPackages["JavaScript"].PackageName);
        Assert.Contains("testUser", capturedProject.Owners);
        Assert.Single(capturedProject.ChangeHistory);
        Assert.Equal(ProjectChangeAction.Created, capturedProject.ChangeHistory[0].ChangeAction);
        Assert.Equal(capturedProject.Id, typeSpecReview.ProjectId);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(typeSpecReview), Times.Once);
    }

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_ExistingProject_UpdatesProject()
    {
        ReviewListItemModel typeSpecReview = new()
        {
            Id = "typespec-review-1",
            Language = "TypeSpec",
            PackageName = "Azure.Storage",
            CrossLanguagePackageId = "Azure.Storage",
            ProjectId = "existing-project-id"
        };

        Project existingProject = new()
        {
            Id = "existing-project-id",
            CrossLanguagePackageId = "Azure.Storage",
            Namespace = "Azure.Storage.Old",
            Description = "Old description",
            ExpectedPackages =
                new Dictionary<string, PackageInfo> { ["Python"] = new() { PackageName = "azure-storage-old" } },
            ChangeHistory = new List<ProjectChangeHistory>(),
            ReviewIds = new HashSet<string>()
        };

        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo
            {
                Namespace = "Azure.Storage.New", // Changed
                Documentation = "New description" // Changed
            },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["Python"] = new() { Namespace = "azure.storage", PackageName = "azure-storage-new" } // Changed
            }
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectAsync("existing-project-id"))
            .ReturnsAsync(existingProject);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);
        Assert.NotNull(result);
        Assert.Equal("Azure.Storage.New", result.Namespace);
        Assert.Equal("New description", result.Description);
        Assert.Equal("azure-storage-new", result.ExpectedPackages["Python"].PackageName);
        Assert.Single(result.ChangeHistory);
        Assert.Equal(ProjectChangeAction.Edited, result.ChangeHistory[0].ChangeAction);
        Assert.Contains("Namespace", result.ChangeHistory[0].Notes);
        Assert.Contains("Description", result.ChangeHistory[0].Notes);
        Assert.Contains("ExpectedPackages", result.ChangeHistory[0].Notes);

        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(existingProject), Times.Once);
    }

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_NoChanges_DoesNotUpdate()
    {
        ReviewListItemModel typeSpecReview = new()
        {
            Id = "typespec-review-1",
            Language = "TypeSpec",
            PackageName = "Azure.Storage",
            CrossLanguagePackageId = "Azure.Storage",
            ProjectId = "existing-project-id"
        };

        Project existingProject = new()
        {
            Id = "existing-project-id",
            CrossLanguagePackageId = "Azure.Storage",
            Namespace = "Azure.Storage",
            Description = "Same description",
            ExpectedPackages = new Dictionary<string, PackageInfo>
            {
                ["Python"] = new() { Namespace = "azure.storage", PackageName = "azure-storage" }
            },
            ChangeHistory = new List<ProjectChangeHistory>(),
            ReviewIds = new HashSet<string>()
        };

        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo
            {
                Namespace = "Azure.Storage", // Same
                Documentation = "Same description" // Same
            },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["Python"] = new() { Namespace = "azure.storage", PackageName = "azure-storage" } // Same
            }
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectAsync("existing-project-id"))
            .ReturnsAsync(existingProject);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(result);
        Assert.Empty(result.ChangeHistory);

        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_NonTypeSpecReview_ReturnsNull()
    {
        ReviewListItemModel review = new()
        {
            Id = "python-review-1",
            Language = "Python", // Not TypeSpec
            PackageName = "azure-core",
            ProjectId = null
        };
        TypeSpecMetadata metadata = new() { TypeSpec = new TypeSpecInfo { Namespace = "Azure.Core" } };

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, review);
        Assert.Null(result);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task TryLinkReviewToProjectAsync_EmptyCrossLanguagePackageId_UsesPackageLookup()
    {
        ReviewListItemModel review = new()
        {
            Id = "review-1",
            Language = "JavaScript",
            PackageName = "@azure/identity",
            CrossLanguagePackageId = "" // Empty string
        };

        Project existingProject = new()
        {
            Id = "project-1", ReviewIds = new HashSet<string>(), ChangeHistory = new List<ProjectChangeHistory>()
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectByExpectedPackageAsync("JavaScript", "@azure/identity"))
            .ReturnsAsync(existingProject);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);
        Assert.NotNull(result);
        _mockProjectsRepository.Verify(r => r.GetProjectByCrossLanguagePackageIdAsync(It.IsAny<string>()), Times.Never);
        _mockProjectsRepository.Verify(r => r.GetProjectByExpectedPackageAsync("JavaScript", "@azure/identity"),
            Times.Once);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_MultipleReviewsLinked_AllAreTracked()
    {
        Project existingProject = new()
        {
            Id = "project-1",
            CrossLanguagePackageId = "Azure.Core",
            ReviewIds = new HashSet<string> { "review-1", "review-2" },
            ChangeHistory = new List<ProjectChangeHistory>()
        };

        ReviewListItemModel review3 = new()
        {
            Id = "review-3", Language = "Go", PackageName = "azcore", CrossLanguagePackageId = "Azure.Core"
        };

        _mockProjectsRepository
            .Setup(r => r.GetProjectByCrossLanguagePackageIdAsync("Azure.Core"))
            .ReturnsAsync(existingProject);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review3);

        Assert.NotNull(result);
        Assert.Equal(3, result.ReviewIds.Count);
        Assert.Contains("review-1", result.ReviewIds);
        Assert.Contains("review-2", result.ReviewIds);
        Assert.Contains("review-3", result.ReviewIds);
    }

    #endregion
}
