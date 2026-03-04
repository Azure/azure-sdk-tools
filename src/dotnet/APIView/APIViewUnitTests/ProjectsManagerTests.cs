using System.Collections.Generic;
using System.Linq;
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

    #region Helpers

    private static ReviewListItemModel CreateReview(
        string id, string language, string packageName,
        string crossLanguagePackageId = null, string projectId = null)
    {
        return new ReviewListItemModel
        {
            Id = id,
            Language = language,
            PackageName = packageName,
            CrossLanguagePackageId = crossLanguagePackageId,
            ProjectId = projectId
        };
    }

    private static ReviewListItemModel CreateTypeSpecReview(
        string id, string packageName, string projectId = null)
    {
        return new ReviewListItemModel
        {
            Id = id,
            Language = "TypeSpec",
            PackageName = packageName,
            CrossLanguagePackageId = packageName,
            ProjectId = projectId
        };
    }

    private static Project CreateProject(
        string id,
        string crossLanguagePackageId = null,
        string namespaceName = null,
        string description = null,
        Dictionary<string, PackageInfo> expectedPackages = null,
        HashSet<string> reviewIds = null,
        HashSet<string> historicalReviewIds = null)
    {
        return new Project
        {
            Id = id,
            CrossLanguagePackageId = crossLanguagePackageId,
            Namespace = namespaceName,
            DisplayName = namespaceName,
            Description = description,
            ExpectedPackages = expectedPackages ?? new Dictionary<string, PackageInfo>(),
            ReviewIds = reviewIds ?? new HashSet<string>(),
            HistoricalReviewIds = historicalReviewIds ?? new HashSet<string>(),
            ChangeHistory = new List<ProjectChangeHistory>()
        };
    }

    private static TypeSpecMetadata CreateMetadata(
        string namespaceName,
        string documentation = null,
        params (string language, string packageName)[] languages)
    {
        return new TypeSpecMetadata
        {
            TypeSpec = new TypeSpecInfo { Namespace = namespaceName, Documentation = documentation ?? namespaceName },
            Languages = languages.ToDictionary(
                l => l.language,
                l => new LanguageConfig { Namespace = l.packageName, PackageName = l.packageName })
        };
    }

    private static Dictionary<string, PackageInfo> Packages(params (string language, string packageName)[] packages)
    {
        return packages.ToDictionary(p => p.language,
            p => new PackageInfo { Namespace = p.packageName, PackageName = p.packageName });
    }

    private void SetupGetProject(string projectId, Project project)
    {
        _mockProjectsRepository.Setup(r => r.GetProjectAsync(projectId)).ReturnsAsync(project);
    }

    private void SetupFindProjectByCrossLanguageId(string crossLanguagePackageId, Project project)
    {
        _mockProjectsRepository.Setup(r => r.GetProjectByCrossLanguagePackageIdAsync(crossLanguagePackageId))
            .ReturnsAsync(project);
    }

    private void SetupFindProjectByExpectedPackage(string language, string packageName, Project project)
    {
        _mockProjectsRepository.Setup(r => r.GetProjectByExpectedPackageAsync(language, packageName))
            .ReturnsAsync(project);
    }

    private void SetupGetReviews(IEnumerable<ReviewListItemModel> reviews)
    {
        _mockReviewsRepository.Setup(r => r.GetReviewsAsync(It.IsAny<IEnumerable<string>>(), null))
            .ReturnsAsync(reviews.ToList());
    }

    private void SetupFindReview(string language, string packageName, ReviewListItemModel review)
    {
        _mockReviewsRepository.Setup(r => r.GetReviewAsync(language, packageName, false)).ReturnsAsync(review);
    }

    #endregion

    #region TryLinkReviewToProjectAsync Tests

    [Fact]
    public async Task TryLinkReviewToProjectAsync_FindsProjectByCrossLanguagePackageId_LinksReview()
    {
        ReviewListItemModel review = CreateReview("review-1", "Python", "azure-core", "Azure.Core");
        Project project = CreateProject("project-1", "Azure.Core");
        SetupFindProjectByCrossLanguageId("Azure.Core", project);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        Assert.Equal("project-1", review.ProjectId);
        Assert.Contains("review-1", result.ReviewIds);
        Assert.Single(result.ChangeHistory);
        Assert.Equal(ProjectChangeAction.ReviewLinked, result.ChangeHistory[0].ChangeAction);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(project), Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(review), Times.Once);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_FindsProjectByExpectedPackage_LinksReview()
    {
        ReviewListItemModel review = CreateReview("review-1", "Python", "azure-core");
        Project project = CreateProject("project-1", "Azure.Core",
            expectedPackages: Packages(("Python", "azure-core")));

        SetupFindProjectByExpectedPackage("Python", "azure-core", project);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        Assert.Equal("project-1", review.ProjectId);
        Assert.Contains("review-1", result.ReviewIds);
        _mockProjectsRepository.Verify(r => r.GetProjectByExpectedPackageAsync("Python", "azure-core"), Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(review), Times.Once);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_NoProjectFound_ReturnsNull()
    {
        ReviewListItemModel review = CreateReview("review-1", "Python", "azure-unknown", "Azure.Unknown");
        SetupFindProjectByCrossLanguageId("Azure.Unknown", null);
        SetupFindProjectByExpectedPackage("Python", "azure-unknown", null);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.Null(result);
        Assert.Null(review.ProjectId);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()), Times.Never);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_ReviewAlreadyLinked_DoesNotDuplicateLink()
    {
        ReviewListItemModel review = CreateReview("review-1", "Python", "azure-core", "Azure.Core");
        Project project = CreateProject("project-1", "Azure.Core",
            reviewIds: new HashSet<string> { "review-1" });
        SetupFindProjectByCrossLanguageId("Azure.Core", project);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        Assert.Single(result.ReviewIds);
        Assert.Empty(result.ChangeHistory);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(review), Times.Once);
    }

    #endregion

    #region UpsertProjectFromMetadataAsync Tests

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_NewProject_CreatesProject()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage");
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage TypeSpec",
            ("Python", "azure-storage"), ("JavaScript", "@azure/storage"));

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
        Assert.Equal(2, capturedProject.ExpectedPackages.Count);
        Assert.Equal("azure-storage", capturedProject.ExpectedPackages["Python"].PackageName);
        Assert.Equal("@azure/storage", capturedProject.ExpectedPackages["JavaScript"].PackageName);
        Assert.Single(capturedProject.ChangeHistory);
        Assert.Equal(ProjectChangeAction.Created, capturedProject.ChangeHistory[0].ChangeAction);
        Assert.Equal(capturedProject.Id, typeSpecReview.ProjectId);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(typeSpecReview), Times.Once);
    }

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_ExistingProject_UpdatesProject()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage.Old", "Old",
            Packages(("Python", "azure-storage-old")));
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage.New", "New",
            ("Python", "azure-storage-new"));

        SetupGetProject("project-1", project);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(result);
        Assert.Equal("Azure.Storage.New", result.Namespace);
        Assert.Equal("New", result.Description);
        Assert.Equal("azure-storage-new", result.ExpectedPackages["Python"].PackageName);
        Assert.Single(result.ChangeHistory);
        Assert.Equal(ProjectChangeAction.Edited, result.ChangeHistory[0].ChangeAction);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(project), Times.Once);
    }

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_NoChanges_DoesNotUpdate()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Same",
            Packages(("Python", "azure-storage")));
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Same", ("Python", "azure-storage"));

        SetupGetProject("project-1", project);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(result);
        Assert.Empty(result.ChangeHistory);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_NonTypeSpecReview_ReturnsNull()
    {
        ReviewListItemModel review = CreateReview("py-1", "Python", "azure-core");
        TypeSpecMetadata metadata = CreateMetadata("Azure.Core");

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, review);

        Assert.Null(result);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task TryLinkReviewToProjectAsync_EmptyCrossLanguagePackageId_UsesPackageLookup()
    {
        ReviewListItemModel review = CreateReview("review-1", "JavaScript", "@azure/identity", "");
        Project project = CreateProject("project-1");
        SetupFindProjectByExpectedPackage("JavaScript", "@azure/identity", project);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        _mockProjectsRepository.Verify(r => r.GetProjectByCrossLanguagePackageIdAsync(It.IsAny<string>()), Times.Never);
        _mockProjectsRepository.Verify(r => r.GetProjectByExpectedPackageAsync("JavaScript", "@azure/identity"),
            Times.Once);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_MultipleReviewsLinked_AllAreTracked()
    {
        Project project = CreateProject("project-1", "Azure.Core",
            reviewIds: new HashSet<string> { "review-1", "review-2" });
        ReviewListItemModel review3 = CreateReview("review-3", "Go", "azcore", "Azure.Core");
        SetupFindProjectByCrossLanguageId("Azure.Core", project);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review3);

        Assert.NotNull(result);
        Assert.Equal(3, result.ReviewIds.Count);
        Assert.Contains("review-3", result.ReviewIds);
    }

    #endregion

    #region ReconcileReviewLinks Tests

    [Fact]
    public async Task Reconcile_PackageRenamed_UnlinksOldAndLinksNewReview()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        ReviewListItemModel oldReview = CreateReview("py-old", "Python", "azure-storage-old", projectId: "project-1");
        ReviewListItemModel newReview = CreateReview("py-new", "Python", "azure-storage-new");
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Azure Storage",
            Packages(("Python", "azure-storage-old")),
            new HashSet<string> { "ts-1", "py-old" });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage-new"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { oldReview });
        SetupFindReview("Python", "azure-storage-new", newReview);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Contains("py-old", result.HistoricalReviewIds);
        Assert.DoesNotContain("py-old", result.ReviewIds);
        Assert.Null(oldReview.ProjectId);

        Assert.Contains("py-new", result.ReviewIds);
        Assert.Equal("project-1", newReview.ProjectId);

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
                It.Is<IEnumerable<ReviewListItemModel>>(revs => revs.Count() == 1 && revs.First().Id == "py-old")),
            Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(
            It.Is<ReviewListItemModel>(rv => rv.Id == "py-new")), Times.Once);
    }

    [Fact]
    public async Task Reconcile_MultipleLanguagesChange_UnlinksAndRelinksAll()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Core", "project-1");
        ReviewListItemModel oldPy = CreateReview("py-old", "Python", "azure-core-old", projectId: "project-1");
        ReviewListItemModel oldJs = CreateReview("js-old", "JavaScript", "@azure/core-old", projectId: "project-1");
        ReviewListItemModel newPy = CreateReview("py-new", "Python", "azure-core-new");
        ReviewListItemModel newJs = CreateReview("js-new", "JavaScript", "@azure/core-new");
        Project project = CreateProject("project-1", "Azure.Core",
            "Azure.Core", "Azure Core",
            Packages(("Python", "azure-core-old"), ("JavaScript", "@azure/core-old")),
            new HashSet<string> { "ts-1", "py-old", "js-old" });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Core", "Azure Core",
            ("Python", "azure-core-new"), ("JavaScript", "@azure/core-new"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { oldPy, oldJs });
        SetupFindReview("Python", "azure-core-new", newPy);
        SetupFindReview("JavaScript", "@azure/core-new", newJs);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Contains("py-old", result.HistoricalReviewIds);
        Assert.Contains("js-old", result.HistoricalReviewIds);
        Assert.Contains("py-new", result.ReviewIds);
        Assert.Contains("js-new", result.ReviewIds);

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
            It.Is<IEnumerable<ReviewListItemModel>>(revs => revs.Count() == 2)), Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(It.Is<ReviewListItemModel>(rv => rv.Id == "py-new")),
            Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(It.Is<ReviewListItemModel>(rv => rv.Id == "js-new")),
            Times.Once);
    }

    [Fact]
    public async Task Reconcile_NewLanguageAdded_SearchesAndLinksNewReview()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        ReviewListItemModel existingPy = CreateReview("py-review", "Python", "azure-storage", projectId: "project-1");
        ReviewListItemModel newJs = CreateReview("js-new", "JavaScript", "@azure/storage");
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Azure Storage",
            Packages(("Python", "azure-storage")),
            new HashSet<string> { "ts-1", "py-review" });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage"), ("JavaScript", "@azure/storage"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { existingPy });
        SetupFindReview("JavaScript", "@azure/storage", newJs);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Contains("py-review", result.ReviewIds);
        Assert.DoesNotContain("py-review", result.HistoricalReviewIds);
        Assert.Contains("js-new", result.ReviewIds);
        Assert.Equal("project-1", newJs.ProjectId);

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(It.IsAny<IEnumerable<ReviewListItemModel>>()),
            Times.Never);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(It.Is<ReviewListItemModel>(rv => rv.Id == "js-new")),
            Times.Once);
    }

    [Fact]
    public async Task Reconcile_NoReplacementFound_UnlinksOldAndLeavesLanguageUncovered()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        ReviewListItemModel oldReview = CreateReview("py-old", "Python", "azure-storage-old", projectId: "project-1");
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Azure Storage",
            Packages(("Python", "azure-storage-old")),
            new HashSet<string> { "ts-1", "py-old" });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage-new"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { oldReview });
        // No review exists for the new package name
        _mockReviewsRepository.Setup(r => r.GetReviewAsync("Python", "azure-storage-new", false))
            .ReturnsAsync((ReviewListItemModel)null);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Contains("py-old", result.HistoricalReviewIds);
        Assert.DoesNotContain("py-old", result.ReviewIds);
        Assert.Null(oldReview.ProjectId);

        // No new review was linked — only ts-1 remains
        Assert.Single(result.ReviewIds);
        Assert.Contains("ts-1", result.ReviewIds);

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
                It.Is<IEnumerable<ReviewListItemModel>>(revs => revs.Count() == 1 && revs.First().Id == "py-old")),
            Times.Once);
        // No individual review upsert for linking
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()), Times.Never);
    }

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_NullLanguages_CreatesProjectWithEmptyExpectedPackages()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Empty");
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Empty", Documentation = "No languages" },
            Languages = null
        };

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(result);
        Assert.NotNull(capturedProject);
        Assert.Empty(capturedProject.ExpectedPackages);
    }

    #endregion

    #region Language Key Normalization Tests

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_LowercaseLanguageKeys_UsesCaseInsensitiveLookup()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage");
        // Metadata with lowercase language keys (common in TypeSpec metadata)
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Storage", Documentation = "Azure Storage" },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["python"] = new() { Namespace = "azure.storage", PackageName = "azure-storage" },
                ["javascript"] = new() { Namespace = "@azure/storage", PackageName = "@azure/storage" },
                ["java"] = new() { Namespace = "com.azure.storage", PackageName = "azure-storage" },
                ["csharp"] = new() { Namespace = "Azure.Storage", PackageName = "Azure.Storage" },
                ["go"] = new() { Namespace = "azstorage", PackageName = "azstorage" }
            }
        };

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        Assert.Equal(5, capturedProject.ExpectedPackages.Count);
        // Dictionary is case-insensitive for lookups
        Assert.True(capturedProject.ExpectedPackages.ContainsKey("python"));
        Assert.True(capturedProject.ExpectedPackages.ContainsKey("Python")); // Case-insensitive lookup works
        Assert.True(capturedProject.ExpectedPackages.ContainsKey("PYTHON")); // Case-insensitive lookup works
        // Verify values are preserved
        Assert.Equal("azure-storage", capturedProject.ExpectedPackages["python"].PackageName);
        Assert.Equal("@azure/storage", capturedProject.ExpectedPackages["javascript"].PackageName);
    }

    [Fact]
    public async Task Reconcile_LowercaseMetadataKeys_MatchesReviewWithCanonicalLanguage()
    {
        // Review uses canonical casing "Python"
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        ReviewListItemModel pyReview = CreateReview("py-review", "Python", "azure-storage", projectId: "project-1");
        // Project has canonical-cased keys
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Azure Storage",
            Packages(("Python", "azure-storage")),
            new HashSet<string> { "ts-1", "py-review" });
        // Metadata comes with lowercase keys (common in TypeSpec)
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Storage", Documentation = "Azure Storage" },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["python"] = new() { Namespace = "azure.storage", PackageName = "azure-storage" }
            }
        };

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { pyReview });

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // Review should remain linked because package name matches, even with different key casing
        Assert.Contains("py-review", result.ReviewIds);
        Assert.DoesNotContain("py-review", result.HistoricalReviewIds);
        // No reviews should have been unlinked
        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(It.IsAny<IEnumerable<ReviewListItemModel>>()), Times.Never);
    }

    [Fact]
    public async Task Reconcile_MixedCaseLanguageKeys_HandlesCorrectly()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Core", "project-1");
        ReviewListItemModel pyReview = CreateReview("py-review", "Python", "azure-core-old", projectId: "project-1");
        ReviewListItemModel newPyReview = CreateReview("py-new", "Python", "azure-core-new");
        Project project = CreateProject("project-1", "Azure.Core",
            "Azure.Core", "Azure Core",
            Packages(("Python", "azure-core-old")),
            new HashSet<string> { "ts-1", "py-review" });
        // Metadata with lowercase key
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Core", Documentation = "Azure Core" },
            Languages = new Dictionary<string, LanguageConfig>
            {
                ["python"] = new() { Namespace = "azure.core", PackageName = "azure-core-new" }
            }
        };

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { pyReview });
        SetupFindReview("python", "azure-core-new", newPyReview);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // Old review should be unlinked (package name changed)
        Assert.Contains("py-review", result.HistoricalReviewIds);
        Assert.DoesNotContain("py-review", result.ReviewIds);
        // New review should be linked
        Assert.Contains("py-new", result.ReviewIds);
        Assert.Equal("project-1", newPyReview.ProjectId);
    }

    #endregion
}
