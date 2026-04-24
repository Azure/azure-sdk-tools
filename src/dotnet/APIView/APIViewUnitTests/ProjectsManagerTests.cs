using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
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
    private readonly Mock<INamespaceManager> _mockNamespaceManager;
    private readonly ProjectsManager _projectsManager;

    public ProjectsManagerTests()
    {
        _mockProjectsRepository = new Mock<ICosmosProjectRepository>();
        _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
        _mockNamespaceManager = new Mock<INamespaceManager>();
        _mockLogger = new Mock<ILogger<ProjectsManager>>();

        _projectsManager = new ProjectsManager(
            _mockProjectsRepository.Object,
            _mockReviewsRepository.Object,
            _mockNamespaceManager.Object,
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
        List<string> expectedPackages = null,
        List<string> expectedNamespaces = null,
        Dictionary<string, List<string>> reviewIds = null,
        HashSet<string> historicalReviewIds = null)
    {
        return new Project
        {
            Id = id,
            CrossLanguagePackageId = crossLanguagePackageId,
            Namespace = namespaceName,
            DisplayName = namespaceName,
            Description = description,
            ExpectedPackages = expectedPackages ?? [],
            ExpectedNamespaces = expectedNamespaces ?? [],
            Reviews = reviewIds ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            HistoricalReviewIds = historicalReviewIds ?? [],
            ChangeHistory = []
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
                l => new List<LanguageConfig> { new() { Namespace = l.packageName, PackageName = l.packageName } })
        };
    }

    private static (List<string> packages, List<string> namespaces) Packages(
        params (string language, string packageName)[] entries)
    {
        // CreateMetadata sets Namespace = PackageName, so both flat lists use the same token value.
        var tokens = entries
            .Select(e => $"{e.language.ToLowerInvariant()}::{e.packageName.ToLowerInvariant()}")
            .ToList();
        return (tokens, tokens);
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
        Assert.Contains("review-1", result.Reviews.Values.SelectMany(v => v));
        Assert.Single(result.ChangeHistory);
        Assert.Equal(ProjectChangeAction.ReviewLinked, result.ChangeHistory[0].ChangeAction);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(project), Times.Once);
        _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(review), Times.Once);
    }

    [Fact]
    public async Task TryLinkReviewToProjectAsync_FindsProjectByExpectedPackage_LinksReview()
    {
        ReviewListItemModel review = CreateReview("review-1", "Python", "azure-core");
        Project project = CreateProject("project-1", "Azure.Core");

        SetupFindProjectByExpectedPackage("Python", "azure-core", project);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        Assert.Equal("project-1", review.ProjectId);
        Assert.Contains("review-1", result.Reviews.Values.SelectMany(v => v));
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
            reviewIds: new Dictionary<string, List<string>> { ["Python"] = ["review-1"] });
        SetupFindProjectByCrossLanguageId("Azure.Core", project);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review);

        Assert.NotNull(result);
        Assert.Single(result.Reviews);
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
        Assert.Contains("python::azure-storage", capturedProject.ExpectedPackages);
        Assert.Contains("javascript::@azure/storage", capturedProject.ExpectedPackages);
        Assert.Contains("python::azure-storage", capturedProject.ExpectedNamespaces);
        Assert.Contains("javascript::@azure/storage", capturedProject.ExpectedNamespaces);
        Assert.Single(capturedProject.ChangeHistory);
        Assert.Equal(ProjectChangeAction.Created, capturedProject.ChangeHistory[0].ChangeAction);
        Assert.Equal(capturedProject.Id, typeSpecReview.ProjectId);
        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
            It.Is<IEnumerable<ReviewListItemModel>>(revs =>
                revs.Any(rv => rv.Id == "ts-1"))),
            Times.Once);
    }

    [Fact]
    public async Task NewProject_DiscoversSingleExistingReview_LinksIt()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage");
        ReviewListItemModel existingPy = CreateReview("py-1", "Python", "azure-storage");
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage"));

        SetupFindReview("Python", "azure-storage", existingPy);

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        Assert.Contains("ts-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.Contains("py-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.Equal(capturedProject.Id, existingPy.ProjectId);
        Assert.Equal(2, capturedProject.ChangeHistory.Count);
        Assert.Equal(ProjectChangeAction.Created, capturedProject.ChangeHistory[0].ChangeAction);
        Assert.Equal(ProjectChangeAction.ReviewLinked, capturedProject.ChangeHistory[1].ChangeAction);

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
            It.Is<IEnumerable<ReviewListItemModel>>(revs =>
                revs.Count() == 2 &&
                revs.Any(rv => rv.Id == "py-1") &&
                revs.Any(rv => rv.Id == "ts-1"))),
            Times.Once);
    }

    [Fact]
    public async Task NewProject_DiscoversMultipleExistingReviews_LinksAll()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage");
        ReviewListItemModel existingPy = CreateReview("py-1", "Python", "azure-storage");
        ReviewListItemModel existingJs = CreateReview("js-1", "JavaScript", "@azure/storage");
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage"), ("JavaScript", "@azure/storage"));

        SetupFindReview("Python", "azure-storage", existingPy);
        SetupFindReview("JavaScript", "@azure/storage", existingJs);

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        Assert.Equal(3, capturedProject.Reviews.Count);
        Assert.Contains("ts-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.Contains("py-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.Contains("js-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.Equal(capturedProject.Id, existingPy.ProjectId);
        Assert.Equal(capturedProject.Id, existingJs.ProjectId);

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
            It.Is<IEnumerable<ReviewListItemModel>>(revs => revs.Count() == 3)),
            Times.Once);
    }

    [Fact]
    public async Task NewProject_MixedDiscovery_LinksFoundAndSkipsMissing()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage");
        ReviewListItemModel existingPy = CreateReview("py-1", "Python", "azure-storage");
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage"), ("JavaScript", "@azure/storage"));

        SetupFindReview("Python", "azure-storage", existingPy);
        // JavaScript review does not exist
        _mockReviewsRepository.Setup(r => r.GetReviewAsync("JavaScript", "@azure/storage", false))
            .ReturnsAsync((ReviewListItemModel)null);

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        Assert.Equal(2, capturedProject.Reviews.Count);
        Assert.Contains("ts-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.Contains("py-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.DoesNotContain("js-1", capturedProject.Reviews.Values.SelectMany(v => v));
    }

    [Fact]
    public async Task NewProject_NoExistingReviews_LinksOnlyTypeSpecReview()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage");
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage"));

        // No review exists for the expected package
        _mockReviewsRepository.Setup(r => r.GetReviewAsync("Python", "azure-storage", false))
            .ReturnsAsync((ReviewListItemModel)null);

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        Assert.Single(capturedProject.Reviews);
        Assert.Contains("ts-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.Single(capturedProject.ChangeHistory);
        Assert.Equal(ProjectChangeAction.Created, capturedProject.ChangeHistory[0].ChangeAction);
    }

    [Fact]
    public async Task NewProject_ReviewAlreadyLinkedToAnotherProject_ReassignsToNewProject()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage");
        // This review is already linked to a different project
        ReviewListItemModel existingPy = CreateReview("py-1", "Python", "azure-storage", projectId: "other-project");
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage"));

        SetupFindReview("Python", "azure-storage", existingPy);

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        Assert.Contains("py-1", capturedProject.Reviews.Values.SelectMany(v => v));
        Assert.Equal(capturedProject.Id, existingPy.ProjectId);

        // Change history should note the re-assignment
        var linkEntry = capturedProject.ChangeHistory
            .First(h => h.ChangeAction == ProjectChangeAction.ReviewLinked);
        Assert.Contains("re-linked from project other-project", linkEntry.Notes);
    }

    [Fact]
    public async Task UpsertProjectFromMetadataAsync_ExistingProject_UpdatesProject()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage.Old", "Old",
            expectedPackages: ["python::azure-storage-old"],
            expectedNamespaces: ["python::azure-storage-old"]);
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage.New", "New",
            ("Python", "azure-storage-new"));

        SetupGetProject("project-1", project);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(result);
        Assert.Equal("Azure.Storage.New", result.Namespace);
        Assert.Equal("New", result.Description);
        Assert.Contains("python::azure-storage-new", result.ExpectedPackages);
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
            expectedPackages: ["python::azure-storage"],
            expectedNamespaces: ["python::azure-storage"]);
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
            reviewIds: new Dictionary<string, List<string>> { ["Python"] = ["review-1"], ["JavaScript"] = ["review-2"] });
        ReviewListItemModel review3 = CreateReview("review-3", "Go", "azcore", "Azure.Core");
        SetupFindProjectByCrossLanguageId("Azure.Core", project);

        Project result = await _projectsManager.TryLinkReviewToProjectAsync("testUser", review3);

        Assert.NotNull(result);
        Assert.Equal(3, result.Reviews.Count);
        Assert.Contains("review-3", result.Reviews.Values.SelectMany(v => v));
    }

    #endregion

    #region ReconcileReviewLinks Tests

    [Fact]
    public async Task Reconcile_PackageRenamed_UnlinksOldAndLinksNewReview()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        ReviewListItemModel oldReview = CreateReview("py-old", "Python", "azure-storage-old", projectId: "project-1");
        ReviewListItemModel newReview = CreateReview("py-new", "Python", "azure-storage-new");
        var (pkgs, ns) = Packages(("Python", "azure-storage-old"));
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Azure Storage",
            pkgs, ns,
            new Dictionary<string, List<string>> { ["TypeSpec"] = ["ts-1"], ["Python"] = ["py-old"] });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage-new"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { oldReview });
        SetupFindReview("Python", "azure-storage-new", newReview);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Contains("py-old", result.HistoricalReviewIds);
        Assert.DoesNotContain("py-old", result.Reviews.Values.SelectMany(v => v));
        Assert.Null(oldReview.ProjectId);

        Assert.Contains("py-new", result.Reviews.Values.SelectMany(v => v));
        Assert.Equal("project-1", newReview.ProjectId);

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
            It.Is<IEnumerable<ReviewListItemModel>>(revs =>
                revs.Count() == 2 &&
                revs.Any(rv => rv.Id == "py-old" && rv.ProjectId == null) &&
                revs.Any(rv => rv.Id == "py-new" && rv.ProjectId == "project-1"))),
            Times.Once);
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
            expectedPackages: ["python::azure-core-old", "javascript::@azure/core-old"],
            expectedNamespaces: ["python::azure-core-old", "javascript::@azure/core-old"],
            reviewIds: new Dictionary<string, List<string>> { ["TypeSpec"] = ["ts-1"], ["Python"] = ["py-old"], ["JavaScript"] = ["js-old"] });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Core", "Azure Core",
            ("Python", "azure-core-new"), ("JavaScript", "@azure/core-new"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { oldPy, oldJs });
        SetupFindReview("Python", "azure-core-new", newPy);
        SetupFindReview("JavaScript", "@azure/core-new", newJs);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Contains("py-old", result.HistoricalReviewIds);
        Assert.Contains("js-old", result.HistoricalReviewIds);
        Assert.Contains("py-new", result.Reviews.Values.SelectMany(v => v));
        Assert.Contains("js-new", result.Reviews.Values.SelectMany(v => v));

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
            It.Is<IEnumerable<ReviewListItemModel>>(revs =>
                revs.Count() == 4 &&
                revs.Any(rv => rv.Id == "py-old" && rv.ProjectId == null) &&
                revs.Any(rv => rv.Id == "js-old" && rv.ProjectId == null) &&
                revs.Any(rv => rv.Id == "py-new" && rv.ProjectId == "project-1") &&
                revs.Any(rv => rv.Id == "js-new" && rv.ProjectId == "project-1"))),
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
            expectedPackages: ["python::azure-storage"],
            expectedNamespaces: ["python::azure-storage"],
            reviewIds: new Dictionary<string, List<string>> { ["TypeSpec"] = ["ts-1"], ["Python"] = ["py-review"] });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage"), ("JavaScript", "@azure/storage"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { existingPy });
        SetupFindReview("JavaScript", "@azure/storage", newJs);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Contains("py-review", result.Reviews.Values.SelectMany(v => v));
        Assert.DoesNotContain("py-review", result.HistoricalReviewIds);
        Assert.Contains("js-new", result.Reviews.Values.SelectMany(v => v));
        Assert.Equal("project-1", newJs.ProjectId);

        _mockReviewsRepository.Verify(r => r.UpsertReviewsAsync(
            It.Is<IEnumerable<ReviewListItemModel>>(revs =>
                revs.Count() == 1 && revs.First().Id == "js-new" && revs.First().ProjectId == "project-1")),
            Times.Once);
    }

    [Fact]
    public async Task Reconcile_DiscoveredReviewAlreadyLinkedToAnotherProject_ReassignsToCurrentProject()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        ReviewListItemModel oldReview = CreateReview("py-old", "Python", "azure-storage-old", projectId: "project-1");
        // The replacement review is already linked to a different project
        ReviewListItemModel newReview = CreateReview("py-new", "Python", "azure-storage-new", projectId: "other-project");
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Azure Storage",
            expectedPackages: ["python::azure-storage-old"],
            expectedNamespaces: ["python::azure-storage-old"],
            reviewIds: new Dictionary<string, List<string>> { ["TypeSpec"] = ["ts-1"], ["Python"] = ["py-old"] });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage-new"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { oldReview });
        SetupFindReview("Python", "azure-storage-new", newReview);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // Review should be re-assigned to the current project
        Assert.Contains("py-new", result.Reviews.Values.SelectMany(v => v));
        Assert.Equal("project-1", newReview.ProjectId);

        // Change history should note the re-assignment from other-project
        var linkEntry = result.ChangeHistory
            .First(h => h.ChangeAction == ProjectChangeAction.ReviewLinked);
        Assert.Contains("re-linked from project other-project", linkEntry.Notes);
    }

    [Fact]
    public async Task Reconcile_NoReplacementFound_UnlinksOldAndLeavesLanguageUncovered()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        ReviewListItemModel oldReview = CreateReview("py-old", "Python", "azure-storage-old", projectId: "project-1");
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Azure Storage",
            expectedPackages: ["python::azure-storage-old"],
            expectedNamespaces: ["python::azure-storage-old"],
            reviewIds: new Dictionary<string, List<string>> { ["TypeSpec"] = ["ts-1"], ["Python"] = ["py-old"] });
        TypeSpecMetadata metadata = CreateMetadata("Azure.Storage", "Azure Storage",
            ("Python", "azure-storage-new"));

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { oldReview });
        // No review exists for the new package name
        _mockReviewsRepository.Setup(r => r.GetReviewAsync("Python", "azure-storage-new", false))
            .ReturnsAsync((ReviewListItemModel)null);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Contains("py-old", result.HistoricalReviewIds);
        Assert.DoesNotContain("py-old", result.Reviews.Values.SelectMany(v => v));
        Assert.Null(oldReview.ProjectId);

        // No new review was linked — Python slot cleared, TypeSpec remains
        Assert.Contains("ts-1", result.Reviews.Values.SelectMany(v => v));
        Assert.DoesNotContain("py-old", result.Reviews.Values.SelectMany(v => v));

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
        Assert.Empty(capturedProject.ExpectedNamespaces);
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
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["python"] = [new() { Namespace = "azure.storage", PackageName = "azure-storage" }],
                ["javascript"] = [new() { Namespace = "@azure/storage", PackageName = "@azure/storage" }],
                ["java"] = [new() { Namespace = "com.azure.storage", PackageName = "azure-storage" }],
                ["csharp"] = [new() { Namespace = "Azure.Storage", PackageName = "Azure.Storage" }],
                ["go"] = [new() { Namespace = "azstorage", PackageName = "azstorage" }]
            }
        };

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        // One flat token per language (5 languages, each with a unique package name)
        Assert.Equal(5, capturedProject.ExpectedPackages.Count);
        Assert.Contains("python::azure-storage", capturedProject.ExpectedPackages);
        Assert.Contains("javascript::@azure/storage", capturedProject.ExpectedPackages);
    }

    [Fact]
    public async Task Reconcile_LowercaseMetadataKeys_MatchesReviewWithCanonicalLanguage()
    {
        // Review uses canonical casing "Python"
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        ReviewListItemModel pyReview = CreateReview("py-review", "Python", "azure-storage", projectId: "project-1");
        // Project has canonical-cased keys; namespace here is "azure.storage" (dot-notation) to match the metadata below.
        Project project = CreateProject("project-1", "Azure.Storage",
            "Azure.Storage", "Azure Storage",
            expectedPackages: ["python::azure-storage"],
            expectedNamespaces: ["python::azure.storage"],
            reviewIds: new Dictionary<string, List<string>> { ["TypeSpec"] = ["ts-1"], ["Python"] = ["py-review"] });
        // Metadata comes with lowercase keys (common in TypeSpec)
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Storage", Documentation = "Azure Storage" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["python"] = [new() { Namespace = "azure.storage", PackageName = "azure-storage" }]
            }
        };

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { pyReview });

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // Review should remain linked because package name matches, even with different key casing
        Assert.True(result.Reviews.Values.Any(v => v.Contains("py-review")));
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
            expectedPackages: ["python::azure-core-old"],
            expectedNamespaces: ["python::azure-core-old"],
            reviewIds: new Dictionary<string, List<string>> { ["TypeSpec"] = ["ts-1"], ["Python"] = ["py-review"] });
        // Metadata with lowercase key
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Core", Documentation = "Azure Core" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["python"] = [new() { Namespace = "azure.core", PackageName = "azure-core-new" }]
            }
        };

        SetupGetProject("project-1", project);
        SetupGetReviews(new[] { pyReview });
        SetupFindReview("Python", "azure-core-new", newPyReview);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // Old review should be unlinked (package name changed)
        Assert.Contains("py-review", result.HistoricalReviewIds);
        Assert.False(result.Reviews.Values.Any(v => v.Contains("py-review")));
        // New review should be linked
        Assert.True(result.Reviews.Values.Any(v => v.Contains("py-new")));
        Assert.Equal("project-1", newPyReview.ProjectId);
    }

    #endregion

    #region BuildExpectedPackages Deduplication Tests

    [Fact]
    public async Task BuildExpectedPackages_JavaKeyVaultFlavorScenario_TwoDifferentPackages_KeepsBoth()
    {
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Security.KeyVault.Administration");
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Security.KeyVault.Administration", Documentation = "KeyVault" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["java"] =
                [
                    // azurev2 flavor
                    new LanguageConfig { EmitterName = "@azure-tools/typespec-java",
                            PackageName = "com.azure.v2:azure-security-keyvault-administration",
                            Namespace = "com.azure.v2.security.keyvault.administration",
                            ServiceDir = "sdk/keyvault",
                            Flavor = "azurev2"
                    },
                    // azure flavor 
                    new LanguageConfig { EmitterName = "@azure-tools/typespec-java-v2",
                            PackageName = "com.azure:azure-security-keyvault-administration",
                            Namespace = "com.azure.security.keyvault.administration",
                            ServiceDir = "sdk/keyvault",
                            Flavor = "azure"

                    }
                ]
            }
        };

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        // Two distinct PackageName tokens → both kept, each needs its own approval
        Assert.Equal(2, capturedProject.ExpectedPackages.Count(t => t.StartsWith("java::")));
        Assert.Contains("java::com.azure.v2:azure-security-keyvault-administration", capturedProject.ExpectedPackages);
        Assert.Contains("java::com.azure:azure-security-keyvault-administration", capturedProject.ExpectedPackages);
        // Two ExpectedNamespaces tokens for Java
        Assert.Equal(2, capturedProject.ExpectedNamespaces.Count(t => t.StartsWith("java::")));
        Assert.Contains("java::com.azure.v2.security.keyvault.administration", capturedProject.ExpectedNamespaces);
        Assert.Contains("java::com.azure.security.keyvault.administration", capturedProject.ExpectedNamespaces);
    }

    [Fact]
    public async Task BuildExpectedPackages_CSharpTwoEmittersSamePackage_DeduplicatesToOne()
    {
        // C# scenario: two tspconfig entries both resolve to the SAME PackageName + Namespace
        // so only one namespace approval should be required.
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Core");
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Core", Documentation = "Azure Core" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["csharp"] =
                [
                    new() { EmitterName = "@azure-tools/typespec-csharp",
                            PackageName = "Azure.Core", Namespace = "Azure.Core" },
                    // Different emitter, but same PackageName + Namespace → deduplicates to one entry
                    new() { EmitterName = "@azure-tools/typespec-csharp-v2",
                            PackageName = "Azure.Core", Namespace = "Azure.Core" }
                ]
            }
        };

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        // Two configs, same PackageName + Namespace → deduplicated to one token each
        Assert.Single(capturedProject.ExpectedPackages.Where(t => t.StartsWith("c#::")));
        Assert.Contains("c#::azure.core", capturedProject.ExpectedPackages);
        Assert.Single(capturedProject.ExpectedNamespaces.Where(t => t.StartsWith("c#::")));
        Assert.Contains("c#::azure.core", capturedProject.ExpectedNamespaces);
    }

    [Fact]
    public async Task BuildExpectedPackages_CSharpTwoEmittersDifferentPackages_KeepsBoth()
    {
        // C# scenario: two tspconfig entries pointing to DIFFERENT packages/namespaces.
        // Both should be preserved as separate PackageInfo entries, each requiring its own approval.
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.AI.Projects");
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.AI.Projects", Documentation = "AI Projects" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["csharp"] =
                [
                    new LanguageConfig { PackageName = "Azure.AI.Projects",            Namespace = "Azure.AI.Projects", Flavor = "azure"},
                    new LanguageConfig { PackageName = "Azure.AI.Agents.Contracts.V2", Namespace = "Azure.AI.Agents.Contracts.V2", Flavor = "azure" }
                ]
            }
        };

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        // Two distinct packages → both kept
        Assert.Equal(2, capturedProject.ExpectedPackages.Count(t => t.StartsWith("c#::")));
        Assert.Contains("c#::azure.ai.projects", capturedProject.ExpectedPackages);
        Assert.Contains("c#::azure.ai.agents.contracts.v2", capturedProject.ExpectedPackages);
    }

    [Fact]
    public async Task BuildExpectedPackages_TwoEmittersSamePackageNameDifferentNamespace_KeepsBoth()
    {
        // Edge case: same PackageName but different Namespace → different "PackageName::Namespace" keys
        // → treated as two distinct entries (not deduplicated).
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Widget");
        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Widget", Documentation = "Widget" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["python"] =
                [
                    new() { PackageName = "azure-widget", Namespace = "azure.widget.v1" },
                    new() { PackageName = "azure-widget", Namespace = "azure.widget.v2" }
                ]
            }
        };

        Project capturedProject = null;
        _mockProjectsRepository
            .Setup(r => r.UpsertProjectAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .Returns(Task.CompletedTask);

        await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.NotNull(capturedProject);
        // Same PackageName, different Namespace → one ExpectedPackages token, two ExpectedNamespaces tokens
        Assert.Single(capturedProject.ExpectedPackages.Where(t => t.StartsWith("python::")));
        Assert.Contains("python::azure-widget", capturedProject.ExpectedPackages);
        Assert.Equal(2, capturedProject.ExpectedNamespaces.Count(t => t.StartsWith("python::")));
        Assert.Contains("python::azure.widget.v1", capturedProject.ExpectedNamespaces);
        Assert.Contains("python::azure.widget.v2", capturedProject.ExpectedNamespaces);
    }

    #endregion

    #region AreExpectedPackagesEqual / PackageListsAreEqual Tests
    // When packages are considered equal → no reconciliation → UpsertProjectAsync is NOT called.
    // When packages are considered unequal → reconciliation runs → UpsertProjectAsync IS called.

    [Fact]
    public async Task ExpectedPackagesEqual_SamePackages_SkipsReconciliation()
    {
        // Identical packages: equal → no update.
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Core", "project-1");
        var (pkgs, ns) = Packages(("Python", "azure-core"), ("JavaScript", "@azure/core"));
        Project project = CreateProject("project-1", "Azure.Core", "Azure.Core", "Azure Core",
            expectedPackages: pkgs, expectedNamespaces: ns);
        TypeSpecMetadata metadata = CreateMetadata("Azure.Core", "Azure Core",
            ("Python", "azure-core"), ("JavaScript", "@azure/core"));

        SetupGetProject("project-1", project);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Empty(result.ChangeHistory);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task ExpectedPackagesEqual_DifferentCasing_TreatedAsEqual()
    {
        // Packages that differ only by casing should be treated as equal.
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Core", "project-1");
        var (pkgs, ns) = Packages(("Python", "azure-core"));
        Project project = CreateProject("project-1", "Azure.Core", "Azure.Core", "Azure Core",
            expectedPackages: pkgs, expectedNamespaces: ns);

        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Core", Documentation = "Azure Core" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["Python"] = [new() { PackageName = "Azure-Core", Namespace = "azure-core" }]
            },
        };

        SetupGetProject("project-1", project);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // azure-core (pkg, lowercased) == azure-core; azure-core (ns, lowercased) == azure-core → equal → no update.
        Assert.Empty(result.ChangeHistory);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task ExpectedPackagesEqual_MultiplePackagesSameLanguage_SameSet_TreatedAsEqual()
    {
        // Two packages for C# — same set in both old and new → equal → no reconciliation.
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.AI.Projects", "project-1");
        Project project = CreateProject("project-1", "Azure.AI.Projects", "Azure.AI.Projects", "AI Projects",
            expectedPackages: ["c#::azure.ai.projects", "c#::azure.ai.agents.contracts.v2"],
            expectedNamespaces: ["c#::azure.ai.projects", "c#::azure.ai.agents.contracts.v2"]);

        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.AI.Projects", Documentation = "AI Projects" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["csharp"] =
                [
                    new() { PackageName = "Azure.AI.Projects",            Namespace = "Azure.AI.Projects" },
                    new() { PackageName = "Azure.AI.Agents.Contracts.V2", Namespace = "Azure.AI.Agents.Contracts.V2" }
                ]
            }
        };

        SetupGetProject("project-1", project);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        Assert.Empty(result.ChangeHistory);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task ExpectedPackagesEqual_MultiplePackagesSameLanguage_DifferentCount_TreatedAsUnequal()
    {
        // Old has one C# package, new adds a second → unequal → reconciliation runs.
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.AI.Projects", "project-1");
        Project project = CreateProject("project-1", "Azure.AI.Projects", "Azure.AI.Projects", "AI Projects",
            expectedPackages: ["c#::azure.ai.projects"],
            expectedNamespaces: ["c#::azure.ai.projects"]);

        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.AI.Projects", Documentation = "AI Projects" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["csharp"] =
                [
                    new() { PackageName = "Azure.AI.Projects",            Namespace = "Azure.AI.Projects" },
                    new() { PackageName = "Azure.AI.Agents.Contracts.V2", Namespace = "Azure.AI.Agents.Contracts.V2" }
                ]
            }
        };

        SetupGetProject("project-1", project);
        // No existing reviews to reconcile against.
        SetupGetReviews([]);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // Packages changed → at least one change entry recorded and project was saved.
        Assert.Contains(result.ChangeHistory, h => h.ChangeAction == ProjectChangeAction.Edited);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Once);
    }

    [Fact]
    public async Task ExpectedPackagesEqual_SamePackageNameDifferentNamespace_TreatedAsUnequal()
    {
        // Package name is identical but the namespace changed.
        // ExpectedPackages tokens are equal (same pkg name), but ExpectedNamespaces tokens differ → unequal.
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        Project project = CreateProject("project-1", "Azure.Storage", "Azure.Storage", "Azure Storage",
            expectedPackages: ["python::azure-storage"],
            expectedNamespaces: ["python::azure.storage.old"]);

        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Storage", Documentation = "Azure Storage" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                // Same PackageName, different Namespace
                ["python"] = [new() { PackageName = "azure-storage", Namespace = "azure.storage.new" }]
            }
        };

        SetupGetProject("project-1", project);
        SetupGetReviews([]);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // ExpectedNamespaces differs → unequal → update.
        Assert.Contains(result.ChangeHistory, h => h.ChangeAction == ProjectChangeAction.Edited);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Once);
    }

    [Fact]
    public async Task ExpectedPackagesEqual_SameNamespaceDifferentPackageName_TreatedAsUnequal()
    {
        // Namespace is identical but the package name changed (e.g. a rename).
        ReviewListItemModel typeSpecReview = CreateTypeSpecReview("ts-1", "Azure.Storage", "project-1");
        Project project = CreateProject("project-1", "Azure.Storage", "Azure.Storage", "Azure Storage",
            expectedPackages: ["python::azure-storage-old"],
            expectedNamespaces: ["python::azure.storage"]);

        TypeSpecMetadata metadata = new()
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Storage", Documentation = "Azure Storage" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                // Same Namespace, different PackageName
                ["python"] = [new() { PackageName = "azure-storage-new", Namespace = "azure.storage" }]
            }
        };

        SetupGetProject("project-1", project);
        SetupGetReviews([]);

        Project result = await _projectsManager.UpsertProjectFromMetadataAsync("testUser", metadata, typeSpecReview);

        // ExpectedPackages differs → unequal → update.
        Assert.Contains(result.ChangeHistory, h => h.ChangeAction == ProjectChangeAction.Edited);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Once);
    }

    #endregion
}
