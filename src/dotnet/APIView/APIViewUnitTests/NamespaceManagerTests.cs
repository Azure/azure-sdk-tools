using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class NamespaceManagerTests
{
    private readonly Mock<ICosmosProjectRepository> _mockProjectsRepository;
    private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
    private readonly Mock<IPermissionsManager> _mockPermissionsManager;
    private readonly Mock<ILogger<NamespaceManager>> _mockLogger;
    private readonly NamespaceManager _namespaceManager;

    public NamespaceManagerTests()
    {
        _mockProjectsRepository = new Mock<ICosmosProjectRepository>();
        _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
        _mockPermissionsManager = new Mock<IPermissionsManager>();
        _mockLogger = new Mock<ILogger<NamespaceManager>>();

        _namespaceManager = new NamespaceManager(
            _mockProjectsRepository.Object,
            _mockReviewsRepository.Object,
            _mockPermissionsManager.Object,
            _mockLogger.Object);
    }

    #region Helpers

    private static ClaimsPrincipal CreateUser(string userName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userName),
            new(ClaimTypes.Name, userName),
            new(ClaimConstants.Login, userName)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static Project CreateProject(
        string id,
        Dictionary<string, List<NamespaceDecisionEntry>> currentStatus = null,
        Dictionary<string, List<NamespaceDecisionEntry>> history = null,
        List<NamespaceDecisionEntry> approved = null,
        Dictionary<string, List<string>> reviews = null)
    {
        return new Project
        {
            Id = id,
            NamespaceInfo = new ProjectNamespaceInfo
            {
                CurrentNamespaceStatus = currentStatus ?? new(StringComparer.OrdinalIgnoreCase),
                NamespaceHistory = history ?? new(StringComparer.OrdinalIgnoreCase),
                ApprovedNamespaces = approved ?? []
            },
            ChangeHistory = [],
            Reviews = reviews ?? new(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static NamespaceDecisionEntry ProposedEntry(string language, string ns, string packageName = null, string proposedBy = "user1")
    {
        return new NamespaceDecisionEntry
        {
            Language = language,
            PackageName = packageName,
            Namespace = ns,
            Status = NamespaceDecisionStatus.Proposed,
            ProposedBy = proposedBy,
            ProposedOn = DateTime.UtcNow.AddDays(-1)
        };
    }

    private static NamespaceDecisionEntry ApprovedEntry(string language, string ns, string packageName = null)
    {
        return new NamespaceDecisionEntry
        {
            Language = language,
            PackageName = packageName,
            Namespace = ns,
            Status = NamespaceDecisionStatus.Approved,
            ProposedBy = "user1",
            ProposedOn = DateTime.UtcNow.AddDays(-2),
            DecidedBy = "approver",
            DecidedOn = DateTime.UtcNow.AddDays(-1)
        };
    }

    private static ProjectNamespaceInfo CreateNamespaceInfo(
        Dictionary<string, List<NamespaceDecisionEntry>> currentStatus = null,
        Dictionary<string, List<NamespaceDecisionEntry>> history = null)
    {
        return new ProjectNamespaceInfo
        {
            CurrentNamespaceStatus = currentStatus ?? new(StringComparer.OrdinalIgnoreCase),
            NamespaceHistory = history ?? new(StringComparer.OrdinalIgnoreCase),
            ApprovedNamespaces = []
        };
    }

    private static Dictionary<string, List<PackageInfo>> Packages(params (string language, string packageName, string ns)[] packages)
    {
        return packages.ToDictionary(
            p => p.language,
            p => new List<PackageInfo> { new() { PackageName = p.packageName, Namespace = p.ns } },
            StringComparer.OrdinalIgnoreCase);
    }

    private void SetupProject(string projectId, Project project)
    {
        _mockProjectsRepository.Setup(r => r.GetProjectAsync(projectId)).ReturnsAsync(project);
    }

    private void SetupPermissions(bool canApprove = true)
    {
        _mockPermissionsManager.Setup(p => p.CanApproveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(canApprove);
    }

    private void SetupReview(string reviewId, ReviewListItemModel review)
    {
        _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId)).ReturnsAsync(review);
    }

    #endregion

    #region BuildInitialNamespaceInfo Tests

    [Fact]
    public void BuildInitialNamespaceInfo_WithTypeSpecAndLanguages_CreatesAllEntries()
    {
        var metadata = new TypeSpecMetadata
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Storage" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["Python"] = [new() { PackageName = "azure-storage", Namespace = "azure.storage" }],
                ["JavaScript"] = [new() { PackageName = "@azure/storage", Namespace = "@azure/storage" }]
            }
        };
        var reviews = new List<ReviewListItemModel>();

        ProjectNamespaceInfo result = _namespaceManager.BuildInitialNamespaceInfo("user1", metadata, reviews);

        Assert.Equal(3, result.CurrentNamespaceStatus.Count);
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("TypeSpec"));
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("Python"));
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("JavaScript"));

        Assert.All(result.CurrentNamespaceStatus.Values.SelectMany(list => list), e =>
        {
            Assert.Equal(NamespaceDecisionStatus.Proposed, e.Status);
            Assert.Equal("user1", e.ProposedBy);
            Assert.NotNull(e.ProposedOn);
        });

        Assert.Equal("Azure.Storage", result.CurrentNamespaceStatus["TypeSpec"][0].Namespace);
        Assert.Equal("azure.storage", result.CurrentNamespaceStatus["Python"][0].Namespace);
        Assert.Equal("azure-storage", result.CurrentNamespaceStatus["Python"][0].PackageName);
        Assert.Empty(result.ApprovedNamespaces);
    }

    [Fact]
    public void BuildInitialNamespaceInfo_WithApprovedReview_AutoApproves()
    {
        var metadata = new TypeSpecMetadata
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Core" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["Python"] = [new() { PackageName = "azure-core", Namespace = "azure.core" }]
            }
        };
        var reviews = new List<ReviewListItemModel>
        {
            new()
            {
                Id = "py-review-1",
                Language = "Python",
                PackageName = "azure-core",
                IsApproved = true,
                ChangeHistory =
                [
                    new ReviewChangeHistoryModel
                    {
                        ChangeAction = ReviewChangeAction.Approved,
                        ChangedBy = "approver1",
                        ChangedOn = new DateTime(2025, 6, 1)
                    }
                ]
            }
        };

        ProjectNamespaceInfo result = _namespaceManager.BuildInitialNamespaceInfo("user1", metadata, reviews);

        NamespaceDecisionEntry pyEntry = result.CurrentNamespaceStatus["Python"][0];
        Assert.Equal(NamespaceDecisionStatus.Approved, pyEntry.Status);
        Assert.Equal("approver1", pyEntry.DecidedBy);
        Assert.Equal(new DateTime(2025, 6, 1), pyEntry.DecidedOn);
        Assert.Contains("Auto-approved", pyEntry.Notes);

        Assert.Single(result.ApprovedNamespaces);
        Assert.Equal("Python", result.ApprovedNamespaces[0].Language);
    }

    [Fact]
    public void BuildInitialNamespaceInfo_WithNullLanguages_ReturnsOnlyTypeSpec()
    {
        var metadata = new TypeSpecMetadata
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Empty" },
            Languages = null
        };

        ProjectNamespaceInfo result = _namespaceManager.BuildInitialNamespaceInfo("user1", metadata, []);

        Assert.Single(result.CurrentNamespaceStatus);
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("TypeSpec"));
    }

    [Fact]
    public void BuildInitialNamespaceInfo_SkipsLanguagesWithEmptyNamespace()
    {
        var metadata = new TypeSpecMetadata
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.Test" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["Python"] = [new() { PackageName = "azure-test", Namespace = "azure.test" }],
                ["Go"] = [new() { PackageName = "aztest", Namespace = "" }],
                ["Java"] = [new() { PackageName = "azure-test", Namespace = null }]
            }
        };

        ProjectNamespaceInfo result = _namespaceManager.BuildInitialNamespaceInfo("user1", metadata, []);

        Assert.Equal(2, result.CurrentNamespaceStatus.Count);
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("TypeSpec"));
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("Python"));
        Assert.False(result.CurrentNamespaceStatus.ContainsKey("Go"));
        Assert.False(result.CurrentNamespaceStatus.ContainsKey("Java"));
    }

    [Fact]
    public void BuildInitialNamespaceInfo_MixedApprovedAndProposed_SetsCorrectly()
    {
        var metadata = new TypeSpecMetadata
        {
            TypeSpec = new TypeSpecInfo { Namespace = "Azure.AI" },
            Languages = new Dictionary<string, List<LanguageConfig>>
            {
                ["Python"] = [new() { PackageName = "azure-ai", Namespace = "azure.ai" }],
                ["JavaScript"] = [new() { PackageName = "@azure/ai", Namespace = "@azure/ai" }]
            }
        };
        var reviews = new List<ReviewListItemModel>
        {
            new()
            {
                Id = "py-1",
                Language = "Python",
                PackageName = "azure-ai",
                IsApproved = true,
                ChangeHistory = [new ReviewChangeHistoryModel { ChangeAction = ReviewChangeAction.Approved, ChangedBy = "approver" }]
            },
            new()
            {
                Id = "js-1",
                Language = "JavaScript",
                PackageName = "@azure/ai",
                IsApproved = false,
                ChangeHistory = []
            }
        };

        ProjectNamespaceInfo result = _namespaceManager.BuildInitialNamespaceInfo("user1", metadata, reviews);

        Assert.Equal(NamespaceDecisionStatus.Approved, result.CurrentNamespaceStatus["Python"][0].Status);
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.CurrentNamespaceStatus["JavaScript"][0].Status);
        Assert.Single(result.ApprovedNamespaces);
    }

    #endregion

    #region ResolvePackageNamespaceChanges Tests

    [Fact]
    public void ResolvePackageNamespaceChanges_RemovedLanguage_WithdrawsAndRemoves()
    {
        var pyEntry = ProposedEntry("Python", "azure.storage", "azure-storage");
        var info = CreateNamespaceInfo(
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = [pyEntry] },
            history: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = new List<NamespaceDecisionEntry>() });

        var oldPkgs = Packages(("Python", "azure-storage", "azure.storage"));
        var newPkgs = Packages(); // Python removed

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        Assert.False(result.CurrentNamespaceStatus.ContainsKey("Python"));
        Assert.Single(result.NamespaceHistory["Python"]);
        Assert.Equal(NamespaceDecisionStatus.Withdrawn, result.NamespaceHistory["Python"][0].Status);
        Assert.Contains(NamespaceManagerConstants.AutoWithdrawalLanguageRemoved, result.NamespaceHistory["Python"][0].Notes);
        Assert.Equal("user1", result.NamespaceHistory["Python"][0].DecidedBy);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_AddedLanguage_ProposesNewEntry()
    {
        var info = CreateNamespaceInfo(
            history: new(StringComparer.OrdinalIgnoreCase) { ["JavaScript"] = new List<NamespaceDecisionEntry>() });

        var oldPkgs = Packages();
        var newPkgs = Packages(("JavaScript", "@azure/storage", "@azure/storage"));

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        Assert.True(result.CurrentNamespaceStatus.ContainsKey("JavaScript"));
        NamespaceDecisionEntry entry = result.CurrentNamespaceStatus["JavaScript"][0];
        Assert.Equal(NamespaceDecisionStatus.Proposed, entry.Status);
        Assert.Equal("@azure/storage", entry.Namespace);
        Assert.Equal("@azure/storage", entry.PackageName);
        Assert.Equal("user1", entry.ProposedBy);

        Assert.Single(result.NamespaceHistory["JavaScript"]);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_AddedLanguage_WithApprovedReview_AutoApproves()
    {
        var info = CreateNamespaceInfo(
            history: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = new List<NamespaceDecisionEntry>() });

        var oldPkgs = Packages();
        var newPkgs = Packages(("Python", "azure-storage", "azure.storage"));
        var reviews = new List<ReviewListItemModel>
        {
            new() { Language = "Python", PackageName = "azure-storage", IsApproved = true }
        };

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, reviews);

        NamespaceDecisionEntry entry = result.CurrentNamespaceStatus["Python"][0];
        Assert.Equal(NamespaceDecisionStatus.Approved, entry.Status);
        Assert.Contains(NamespaceManagerConstants.AutoApprovalNotes, entry.Notes);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_ChangedNamespace_WithdrawsOldAndProposesNew()
    {
        var pyEntry = ProposedEntry("Python", "azure.storage.old", "azure-storage");
        var info = CreateNamespaceInfo(
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = [pyEntry] },
            history: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = new List<NamespaceDecisionEntry>() });

        var oldPkgs = Packages(("Python", "azure-storage", "azure.storage.old"));
        var newPkgs = Packages(("Python", "azure-storage", "azure.storage.new"));

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        // Old entry was withdrawn and added to history
        Assert.Equal(2, result.NamespaceHistory["Python"].Count);
        Assert.Equal(NamespaceDecisionStatus.Withdrawn, result.NamespaceHistory["Python"][0].Status);
        Assert.Contains(NamespaceManagerConstants.AutoWithdrawalNewNameSuggested, result.NamespaceHistory["Python"][0].Notes);

        // New entry is proposed and in current status
        NamespaceDecisionEntry current = result.CurrentNamespaceStatus["Python"][0];
        Assert.Equal(NamespaceDecisionStatus.Proposed, current.Status);
        Assert.Equal("azure.storage.new", current.Namespace);

        // New proposed also added to history
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.NamespaceHistory["Python"][1].Status);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_UnchangedNamespace_NoChanges()
    {
        var pyEntry = ProposedEntry("Python", "azure.storage", "azure-storage");
        var info = CreateNamespaceInfo(
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = [pyEntry] },
            history: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = new List<NamespaceDecisionEntry>() });

        var oldPkgs = Packages(("Python", "azure-storage", "azure.storage"));
        var newPkgs = Packages(("Python", "azure-storage", "azure.storage"));

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        Assert.Equal(NamespaceDecisionStatus.Proposed, result.CurrentNamespaceStatus["Python"][0].Status);
        Assert.Empty(result.NamespaceHistory["Python"]);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_NullInputs_ReturnsCurrentInfo()
    {
        var info = CreateNamespaceInfo();

        Assert.Same(info, _namespaceManager.ResolvePackageNamespaceChanges("user1", info, null, Packages(), []));
        Assert.Same(info, _namespaceManager.ResolvePackageNamespaceChanges("user1", info, Packages(), null, []));
        Assert.Null(_namespaceManager.ResolvePackageNamespaceChanges("user1", null, Packages(), Packages(), []));
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_RemovedLanguage_NoCurrentEntry_NoOp()
    {
        var info = CreateNamespaceInfo(
            history: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = new List<NamespaceDecisionEntry>() });

        var oldPkgs = Packages(("Python", "azure-storage", "azure.storage"));
        var newPkgs = Packages();

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        Assert.Empty(result.NamespaceHistory["Python"]);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_MixedOperations_AllApplied()
    {
        var pyEntry = ProposedEntry("Python", "azure.old", "azure-old");
        var jsEntry = ProposedEntry("JavaScript", "@azure/js", "@azure/js");
        var info = CreateNamespaceInfo(
            currentStatus: new(StringComparer.OrdinalIgnoreCase)
            {
                ["Python"] = [pyEntry],
                ["JavaScript"] = [jsEntry]
            },
            history: new(StringComparer.OrdinalIgnoreCase)
            {
                ["Python"] = new List<NamespaceDecisionEntry>(),
                ["JavaScript"] = new List<NamespaceDecisionEntry>(),
                ["Go"] = new List<NamespaceDecisionEntry>()
            });

        // Python: namespace changed, JavaScript: removed, Go: added
        var oldPkgs = Packages(
            ("Python", "azure-old", "azure.old"),
            ("JavaScript", "@azure/js", "@azure/js"));
        var newPkgs = Packages(
            ("Python", "azure-new", "azure.new"),
            ("Go", "azstorage", "azstorage"));

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        // Python: withdrawn + re-proposed
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.CurrentNamespaceStatus["Python"][0].Status);
        Assert.Equal("azure.new", result.CurrentNamespaceStatus["Python"][0].Namespace);
        Assert.Equal(2, result.NamespaceHistory["Python"].Count);

        // JavaScript: withdrawn and removed from current
        Assert.False(result.CurrentNamespaceStatus.ContainsKey("JavaScript"));
        Assert.Single(result.NamespaceHistory["JavaScript"]);
        Assert.Equal(NamespaceDecisionStatus.Withdrawn, result.NamespaceHistory["JavaScript"][0].Status);

        // Go: added
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.CurrentNamespaceStatus["Go"][0].Status);
        Assert.Equal("azstorage", result.CurrentNamespaceStatus["Go"][0].Namespace);
        Assert.Single(result.NamespaceHistory["Go"]);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_AddedLanguageWithEmptyNamespace_Skipped()
    {
        var info = CreateNamespaceInfo(
            history: new(StringComparer.OrdinalIgnoreCase) { ["Go"] = new List<NamespaceDecisionEntry>() });

        var oldPkgs = Packages();
        var newPkgs = new Dictionary<string, List<PackageInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Go"] = [new() { PackageName = "azstorage", Namespace = "" }]
        };

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        Assert.False(result.CurrentNamespaceStatus.ContainsKey("Go"));
        Assert.Empty(result.NamespaceHistory["Go"]);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_CaseInsensitiveLanguageKeys()
    {
        var pyEntry = ProposedEntry("python", "azure.old", "azure-old");
        var info = CreateNamespaceInfo(
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["python"] = [pyEntry] },
            history: new(StringComparer.OrdinalIgnoreCase) { ["python"] = new List<NamespaceDecisionEntry>() });

        var oldPkgs = Packages(("Python", "azure-old", "azure.old"));
        var newPkgs = Packages(("PYTHON", "azure-new", "azure.new"));

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        // Should treat "python", "Python", "PYTHON" as the same key — changed namespace
        Assert.Equal("azure.new", result.CurrentNamespaceStatus["python"][0].Namespace);
        Assert.Equal(2, result.NamespaceHistory["python"].Count);
    }

    #endregion

    #region ResolveTypeSpecNamespaceChange Tests

    [Fact]
    public void ResolveTypeSpecNamespaceChange_SameNamespace_ReturnsUnchanged()
    {
        var tsEntry = ProposedEntry("TypeSpec", "Azure.Storage");
        var info = CreateNamespaceInfo(
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["TypeSpec"] = [tsEntry] },
            history: new(StringComparer.OrdinalIgnoreCase) { ["TypeSpec"] = new List<NamespaceDecisionEntry>() });

        ProjectNamespaceInfo result = _namespaceManager.ResolveTypeSpecNamespaceChange("user1", info, "Azure.Storage", "Azure.Storage");

        Assert.Same(info, result);
        Assert.Empty(result.NamespaceHistory["TypeSpec"]);
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.CurrentNamespaceStatus["TypeSpec"][0].Status);
    }

    [Fact]
    public void ResolveTypeSpecNamespaceChange_DifferentNamespace_WithdrawsAndReproposes()
    {
        var tsEntry = ProposedEntry("TypeSpec", "Azure.Storage.Old");
        var info = CreateNamespaceInfo(
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["TypeSpec"] = [tsEntry] },
            history: new(StringComparer.OrdinalIgnoreCase) { ["TypeSpec"] = new List<NamespaceDecisionEntry>() });

        ProjectNamespaceInfo result = _namespaceManager.ResolveTypeSpecNamespaceChange("user1", info, "Azure.Storage.Old", "Azure.Storage.New");

        Assert.Equal(2, result.NamespaceHistory["TypeSpec"].Count);
        Assert.Equal(NamespaceDecisionStatus.Withdrawn, result.NamespaceHistory["TypeSpec"][0].Status);
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.NamespaceHistory["TypeSpec"][1].Status);
        Assert.Equal("Azure.Storage.New", result.CurrentNamespaceStatus["TypeSpec"][0].Namespace);
    }

    #endregion

    #region UpdateNamespaceStatusAsync Tests

    [Theory]
    [InlineData(NamespaceDecisionStatus.Proposed, NamespaceDecisionStatus.Approved)]
    [InlineData(NamespaceDecisionStatus.Proposed, NamespaceDecisionStatus.Rejected)]
    [InlineData(NamespaceDecisionStatus.Rejected, NamespaceDecisionStatus.Approved)]
    [InlineData(NamespaceDecisionStatus.Approved, NamespaceDecisionStatus.Rejected)]
    public async Task UpdateNamespaceStatusAsync_ValidTransition_Succeeds(NamespaceDecisionStatus from, NamespaceDecisionStatus to)
    {
        SetupPermissions(true);
        var entry = ProposedEntry("Python", "azure.storage", "azure-storage");
        entry.Status = from;
        if (from != NamespaceDecisionStatus.Proposed) { entry.DecidedBy = "someone"; entry.DecidedOn = DateTime.UtcNow.AddDays(-1); }
        var project = CreateProject("project-1",
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = [entry] },
            reviews: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = ["py-review-1"] });
        SetupProject("project-1", project);
        SetupReview("py-review-1", new ReviewListItemModel { Id = "py-review-1" });

        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "project-1", "Python", null, to, "test notes", CreateUser("approver"));

        Assert.True(result.IsSuccess);
        Assert.Equal(to, entry.Status);

        if (to != NamespaceDecisionStatus.Proposed)
        {
            Assert.Equal("approver", entry.DecidedBy);
            Assert.NotNull(entry.DecidedOn);
        }

        Assert.Single(result.Project.ChangeHistory);
        Assert.Equal(ProjectChangeAction.NamespaceStatusChanged, result.Project.ChangeHistory[0].ChangeAction);
        Assert.Single(result.Project.NamespaceInfo.NamespaceHistory["Python"]);
        Assert.Equal(from, result.Project.NamespaceInfo.NamespaceHistory["Python"][0].Status);

        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(project), Times.Once);
    }

    // Invalid transitions ---

    [Theory]
    [InlineData(NamespaceDecisionStatus.Proposed, NamespaceDecisionStatus.Proposed)]
    [InlineData(NamespaceDecisionStatus.Approved, NamespaceDecisionStatus.Approved)]
    [InlineData(NamespaceDecisionStatus.Approved, NamespaceDecisionStatus.Withdrawn)]
    [InlineData(NamespaceDecisionStatus.Approved, NamespaceDecisionStatus.Proposed)]
    [InlineData(NamespaceDecisionStatus.Rejected, NamespaceDecisionStatus.Rejected)]
    [InlineData(NamespaceDecisionStatus.Rejected, NamespaceDecisionStatus.Proposed)]
    [InlineData(NamespaceDecisionStatus.Withdrawn, NamespaceDecisionStatus.Withdrawn)]
    [InlineData(NamespaceDecisionStatus.Withdrawn, NamespaceDecisionStatus.Approved)]
    [InlineData(NamespaceDecisionStatus.Withdrawn, NamespaceDecisionStatus.Rejected)]
    public async Task UpdateNamespaceStatusAsync_InvalidTransition_ReturnsError(NamespaceDecisionStatus from, NamespaceDecisionStatus to)
    {
        SetupPermissions(true);
        var entry = ProposedEntry("Python", "azure.storage", "azure-storage");
        entry.Status = from;
        var project = CreateProject("project-1",
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = [entry] });
        SetupProject("project-1", project);

        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "project-1", "Python", null, to, null, CreateUser("approver"));

        Assert.False(result.IsSuccess);
        Assert.Equal(NamespaceOperationError.InvalidStateTransition, result.Error);
        _mockProjectsRepository.Verify(r => r.UpsertProjectAsync(It.IsAny<Project>()), Times.Never);
    }

    // --- Approved list tracking ---

    [Fact]
    public async Task UpdateNamespaceStatusAsync_Approve_AddsToApprovedList()
    {
        SetupPermissions(true);
        var entry = ProposedEntry("Python", "azure.storage", "azure-storage");
        var project = CreateProject("project-1",
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = [entry] });
        SetupProject("project-1", project);

        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "project-1", "Python", null, NamespaceDecisionStatus.Approved, null, CreateUser("approver"));

        Assert.Single(result.Project.NamespaceInfo.ApprovedNamespaces);
    }

    [Fact]
    public async Task UpdateNamespaceStatusAsync_RejectApproved_RemovesFromApprovedList()
    {
        SetupPermissions(true);
        var entry = ApprovedEntry("Python", "azure.storage", "azure-storage");
        var project = CreateProject("project-1",
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = [entry] },
            approved: [entry]);
        SetupProject("project-1", project);

        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "project-1", "Python", null, NamespaceDecisionStatus.Rejected, null, CreateUser("approver"));

        Assert.Empty(result.Project.NamespaceInfo.ApprovedNamespaces);
    }

    // --- Error cases ---

    [Fact]
    public async Task UpdateNamespaceStatusAsync_NoPermission_ReturnsUnauthorized()
    {
        SetupPermissions(false);

        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "project-1", "Python", null, NamespaceDecisionStatus.Approved, null, CreateUser("noperm"));

        Assert.False(result.IsSuccess);
        Assert.Equal(NamespaceOperationError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task UpdateNamespaceStatusAsync_ProjectNotFound_ReturnsProjectNotFound()
    {
        SetupPermissions(true);
        SetupProject("project-1", null);

        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "project-1", "Python", null, NamespaceDecisionStatus.Approved, null, CreateUser("approver"));

        Assert.False(result.IsSuccess);
        Assert.Equal(NamespaceOperationError.ProjectNotFound, result.Error);
    }

    [Fact]
    public async Task UpdateNamespaceStatusAsync_LanguageNotFound_ReturnsLanguageNotFound()
    {
        SetupPermissions(true);
        var project = CreateProject("project-1");
        SetupProject("project-1", project);

        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "project-1", "Python", null, NamespaceDecisionStatus.Approved, null, CreateUser("approver"));

        Assert.False(result.IsSuccess);
        Assert.Equal(NamespaceOperationError.LanguageNotFound, result.Error);
    }

    #endregion

    #region GetNamespaceInfoAsync Tests

    [Fact]
    public async Task GetNamespaceInfoAsync_ProjectExists_ReturnsNamespaceInfo()
    {
        var project = CreateProject("project-1",
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["Python"] = [ProposedEntry("Python", "azure.storage")] });
        SetupProject("project-1", project);

        ProjectNamespaceInfo result = await _namespaceManager.GetNamespaceInfoAsync("project-1");

        Assert.NotNull(result);
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("Python"));
    }

    [Fact]
    public async Task GetNamespaceInfoAsync_ProjectNotFound_ReturnsNull()
    {
        SetupProject("project-1", null);

        ProjectNamespaceInfo result = await _namespaceManager.GetNamespaceInfoAsync("project-1");

        Assert.Null(result);
    }

    #endregion

    #region AzureAIProjects Real-World Scenario

    // Mirrors the real tspconfig.yaml for azure-ai-projects:
    //   TypeSpec namespace : Azure.AI.Projects
    //   unknown            : no namespace → skipped
    //   python             : azure.ai.projects  (1 package)
    //   csharp             : Azure.AI.Projects + Azure.AI.Agents.Contracts.V2  (2 packages)
    //   typescript         : @azure/ai-projects  (1 package)
    //   java               : com.azure.ai.projects  (1 package)
    private static TypeSpecMetadata AzureAIProjectsMetadata() => new()
    {
        TypeSpec = new TypeSpecInfo { Namespace = "Azure.AI.Projects" },
        Languages = new Dictionary<string, List<LanguageConfig>>
        {
            // "unknown" has no Namespace on any config → all entries skipped
            ["unknown"] = [
                new() { EmitterName = "@typespec/openapi3", ServiceDir = "sdk/ai" },
                new() { EmitterName = "@typespec/json-schema", ServiceDir = "sdk/ai" },
                new() { EmitterName = "@azure-tools/typespec-metadata", ServiceDir = "sdk/ai" }
            ],
            ["python"] = [
                new() { PackageName = "azure-ai-projects", Namespace = "azure.ai.projects", ServiceDir = "sdk/ai" }
            ],
            ["csharp"] = [
                new() { PackageName = "Azure.AI.Projects",              Namespace = "Azure.AI.Projects",              ServiceDir = "sdk/ai" },
                new() { PackageName = "Azure.AI.Agents.Contracts.V2",   Namespace = "Azure.AI.Agents.Contracts.V2",   ServiceDir = "sdk/ai" }
            ],
            ["typescript"] = [
                new() { PackageName = "@azure/ai-projects", Namespace = "@azure/ai-projects", ServiceDir = "sdk/ai" }
            ],
            ["java"] = [
                new() { PackageName = "com.azure:azure-ai-projects", Namespace = "com.azure.ai.projects", ServiceDir = "sdk/ai" }
            ]
        }
    };

    [Fact]
    public void BuildInitialNamespaceInfo_AzureAIProjects_CreatesCorrectEntries()
    {
        ProjectNamespaceInfo result = _namespaceManager.BuildInitialNamespaceInfo("user1", AzureAIProjectsMetadata(), []);

        // TypeSpec + python(1) + csharp(2) + typescript(1) + java(1) = 5 languages; unknown has no namespaces → skipped
        Assert.Equal(5, result.CurrentNamespaceStatus.Count);
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("TypeSpec"));
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("Python"));
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("C#"));          // csharp aliased
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("JavaScript"));  // typescript aliased
        Assert.True(result.CurrentNamespaceStatus.ContainsKey("Java"));
        Assert.False(result.CurrentNamespaceStatus.ContainsKey("unknown"));

        // TypeSpec: one entry
        Assert.Single(result.CurrentNamespaceStatus["TypeSpec"]);
        Assert.Equal("Azure.AI.Projects", result.CurrentNamespaceStatus["TypeSpec"][0].Namespace);

        // C# has two packages → two entries
        Assert.Equal(2, result.CurrentNamespaceStatus["C#"].Count);
        Assert.Contains(result.CurrentNamespaceStatus["C#"], e => e.Namespace == "Azure.AI.Projects" && e.PackageName == "Azure.AI.Projects");
        Assert.Contains(result.CurrentNamespaceStatus["C#"], e => e.Namespace == "Azure.AI.Agents.Contracts.V2" && e.PackageName == "Azure.AI.Agents.Contracts.V2");

        // All proposed, no approvals
        Assert.All(result.CurrentNamespaceStatus.Values.SelectMany(list => list), e =>
            Assert.Equal(NamespaceDecisionStatus.Proposed, e.Status));
        Assert.Empty(result.ApprovedNamespaces);
    }

    [Fact]
    public void BuildInitialNamespaceInfo_AzureAIProjects_WithApprovedPythonReview_AutoApproves()
    {
        var reviews = new List<ReviewListItemModel>
        {
            new()
            {
                Id = "py-ai-1",
                Language = "Python",
                PackageName = "azure-ai-projects",
                IsApproved = true,
                ChangeHistory = [new ReviewChangeHistoryModel { ChangeAction = ReviewChangeAction.Approved, ChangedBy = "sdk-bot", ChangedOn = new DateTime(2026, 1, 15) }]
            }
        };

        ProjectNamespaceInfo result = _namespaceManager.BuildInitialNamespaceInfo("user1", AzureAIProjectsMetadata(), reviews);

        // Python auto-approved; everything else stays Proposed
        Assert.Equal(NamespaceDecisionStatus.Approved, result.CurrentNamespaceStatus["Python"][0].Status);
        Assert.Equal("sdk-bot", result.CurrentNamespaceStatus["Python"][0].DecidedBy);
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.CurrentNamespaceStatus["C#"][0].Status);
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.CurrentNamespaceStatus["JavaScript"][0].Status);
        Assert.Single(result.ApprovedNamespaces);
    }

    [Fact]
    public async Task UpdateNamespaceStatusAsync_AzureAIProjects_ApprovesSpecificCSharpNamespaceByNamespace()
    {
        // C# has two namespace entries. Approving by namespace selects the right one.
        SetupPermissions(true);
        var csEntries = new List<NamespaceDecisionEntry>
        {
            ProposedEntry("C#", "Azure.AI.Projects",            "Azure.AI.Projects"),
            ProposedEntry("C#", "Azure.AI.Agents.Contracts.V2", "Azure.AI.Agents.Contracts.V2")
        };
        var project = CreateProject("ai-project-1",
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["C#"] = csEntries });
        SetupProject("ai-project-1", project);

        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "ai-project-1", "csharp", "Azure.AI.Agents.Contracts.V2", NamespaceDecisionStatus.Approved, "LGTM", CreateUser("approver"));

        Assert.True(result.IsSuccess);
        // Only the targeted namespace is approved
        Assert.Equal(NamespaceDecisionStatus.Approved, csEntries.First(e => e.Namespace == "Azure.AI.Agents.Contracts.V2").Status);
        Assert.Equal(NamespaceDecisionStatus.Proposed,  csEntries.First(e => e.Namespace == "Azure.AI.Projects").Status);
        Assert.Single(result.Project.NamespaceInfo.ApprovedNamespaces);
        Assert.Equal("Azure.AI.Agents.Contracts.V2", result.Project.NamespaceInfo.ApprovedNamespaces[0].Namespace);
    }

    [Fact]
    public async Task UpdateNamespaceStatusAsync_AzureAIProjects_UnknownNamespace_ReturnsNamespaceEntryNotFound()
    {
        SetupPermissions(true);
        var csEntries = new List<NamespaceDecisionEntry>
        {
            ProposedEntry("C#", "Azure.AI.Projects", "Azure.AI.Projects")
        };
        var project = CreateProject("ai-project-1",
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["C#"] = csEntries });
        SetupProject("ai-project-1", project);

        // Passing a namespace that doesn't exist in the entries
        NamespaceOperationResult result = await _namespaceManager.UpdateNamespaceStatusAsync(
            "ai-project-1", "csharp", "Azure.AI.DoesNotExist", NamespaceDecisionStatus.Approved, null, CreateUser("approver"));

        Assert.False(result.IsSuccess);
        Assert.Equal(NamespaceOperationError.NamespaceEntryNotFound, result.Error);
    }

    [Fact]
    public void ResolvePackageNamespaceChanges_AzureAIProjects_AddSecondCSharpPackage()
    {
        // Start with C# having one package; metadata update adds a second package with a new namespace
        var existing = ProposedEntry("C#", "Azure.AI.Projects", "Azure.AI.Projects");
        var info = CreateNamespaceInfo(
            currentStatus: new(StringComparer.OrdinalIgnoreCase) { ["C#"] = [existing] });

        var oldPkgs = new Dictionary<string, List<PackageInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["C#"] = [new() { PackageName = "Azure.AI.Projects", Namespace = "Azure.AI.Projects" }]
        };
        var newPkgs = new Dictionary<string, List<PackageInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["C#"] = [
                new() { PackageName = "Azure.AI.Projects",            Namespace = "Azure.AI.Projects" },
                new() { PackageName = "Azure.AI.Agents.Contracts.V2", Namespace = "Azure.AI.Agents.Contracts.V2" }
            ]
        };

        ProjectNamespaceInfo result = _namespaceManager.ResolvePackageNamespaceChanges("user1", info, oldPkgs, newPkgs, []);

        Assert.Equal(2, result.CurrentNamespaceStatus["C#"].Count);
        // Original entry untouched
        Assert.Equal(NamespaceDecisionStatus.Proposed, result.CurrentNamespaceStatus["C#"].First(e => e.Namespace == "Azure.AI.Projects").Status);
        // New entry proposed
        var newEntry = result.CurrentNamespaceStatus["C#"].First(e => e.Namespace == "Azure.AI.Agents.Contracts.V2");
        Assert.Equal(NamespaceDecisionStatus.Proposed, newEntry.Status);
        Assert.Equal("user1", newEntry.ProposedBy);
    }

    #endregion
}
