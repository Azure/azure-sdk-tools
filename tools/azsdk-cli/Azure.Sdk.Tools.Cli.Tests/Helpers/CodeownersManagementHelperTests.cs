// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class CodeownersManagementHelperTests
{
    private Mock<IDevOpsService> _mockDevOps;
    private Mock<ITeamUserCache> _mockTeamUserCache;
    private Mock<IGitHubService> _mockGitHub;
    private CodeownersManagementHelper _helper;

    [SetUp]
    public void Setup()
    {
        _mockDevOps = new Mock<IDevOpsService>();
        _mockTeamUserCache = new Mock<ITeamUserCache>();
        _mockTeamUserCache.Setup(c => c.GetUsersForTeam(It.IsAny<string>())).Returns(new List<string>());
        _mockTeamUserCache.Setup(c => c.TeamUserDict).Returns(new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase));
        _mockGitHub = new Mock<IGitHubService>();
        _helper = new CodeownersManagementHelper(
            new TestLogger<CodeownersManagementHelper>(),
            _mockDevOps.Object,
            _mockTeamUserCache.Object,
            _mockGitHub.Object
        );
    }

    // ========================
    // WorkItem factory helpers
    // ========================

    /// <summary>Creates a raw WorkItem with the given type and fields, plus optional relations.</summary>
    private static WorkItem MakeWorkItem(int id, string workItemType, Dictionary<string, object> extraFields, params int[] relatedIds)
    {
        var fields = new Dictionary<string, object>
        {
            { "System.WorkItemType", workItemType }
        };
        foreach (var kvp in extraFields)
        {
            fields[kvp.Key] = kvp.Value;
        }

        return new WorkItem
        {
            Id = id,
            Fields = fields,
            Relations = relatedIds.Select(relId => new WorkItemRelation
            {
                Rel = "System.LinkTypes.Related",
                Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{relId}"
            }).ToList()
        };
    }

    private static WorkItem MakeOwnerWorkItem(int id, string alias, params int[] relatedIds)
        => MakeWorkItem(id, "Owner", new Dictionary<string, object> { { "Custom.GitHubAlias", alias } }, relatedIds);

    private static WorkItem MakeLabelWorkItem(int id, string labelName, params int[] relatedIds)
        => MakeWorkItem(id, "Label", new Dictionary<string, object> { { "Custom.Label", labelName } }, relatedIds);

    private static WorkItem MakePackageWorkItem(int id, string name, string language = ".NET", string pkgType = "client", string version = "1.0", params int[] relatedIds)
        => MakeWorkItem(id, "Package", new Dictionary<string, object>
        {
            { "Custom.Package", name },
            { "Custom.Language", language },
            { "Custom.PackageType", pkgType },
            { "Custom.PackageVersionMajorMinor", version },
            { "Custom.ServiceName", "" },
            { "Custom.PackageDisplayName", name },
            { "Custom.GroupId", "" },
            { "Custom.PackageRepoPath", "" }
        }, relatedIds);

    private static WorkItem MakeLabelOwnerWorkItem(int id, string labelType, string repository, string repoPath = "", params int[] relatedIds)
        => MakeWorkItem(id, "Label Owner", new Dictionary<string, object>
        {
            { "Custom.LabelType", labelType },
            { "Custom.Repository", repository },
            { "Custom.RepoPath", repoPath },
            { "Custom.Section", "" }
        }, relatedIds);

    // ========================
    // Assertion helpers
    // ========================

    private static void AssertPackage(PackageResponse pkg, string expectedName, string[]? expectedOwners = null, string[]? expectedLabels = null)
    {
        Assert.That(pkg.PackageName, Is.EqualTo(expectedName));
        if (expectedOwners != null)
        {
            Assert.That(pkg.Owners!.Select(o => o.GitHubAlias), Is.EquivalentTo(expectedOwners));
        }
        if (expectedLabels != null)
        {
            Assert.That(pkg.Labels, Is.EquivalentTo(expectedLabels));
        }
    }

    private static void AssertLabelOwner(LabelOwnerResponse lo, string expectedRepo, string expectedPath, string[]? expectedOwners = null, string[]? expectedLabels = null, string? expectedSection = null)
    {
        Assert.That(lo.Repo, Is.EqualTo(expectedRepo));
        Assert.That(lo.Path, Is.EqualTo(expectedPath));
        if (expectedOwners != null)
        {
            Assert.That(lo.Owners!.Select(o => o.GitHubAlias), Is.EquivalentTo(expectedOwners));
        }
        if (expectedLabels != null)
        {
            Assert.That(lo.Labels, Is.EquivalentTo(expectedLabels));
        }
        if (expectedSection != null)
        {
            Assert.That(lo.Section, Is.EqualTo(expectedSection));
        }
    }

    // ========================
    // Static helper tests
    // ========================

    [TestCase("@test-owner", "test-owner")]
    [TestCase("test-owner", "test-owner")]
    [TestCase(" @test-owner ", "test-owner")]
    [TestCase("", "")]
    [TestCase("  ", "")]
    [TestCase("@", "")]
    [TestCase("@azure/test-team", "azure/test-team")]
    [TestCase("azure/test-team", "azure/test-team")]
    [TestCase(" @azure/test-team ", "azure/test-team")]
    public void NormalizeGitHubAlias_ReturnsExpected(string input, string expected)
    {
        Assert.That(CodeownersManagementHelper.NormalizeGitHubAlias(input), Is.EqualTo(expected));
    }

    // ========================
    // GetViewByUser tests
    // ========================

    [Test]
    public async Task GetViewByUser_OwnerNotFound_ReturnsError()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Owner'") && q.Contains("Custom.GitHubAlias") && q.Contains("nonexistent-owner")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByUser("nonexistent-owner", null, CancellationToken.None);

        Assert.That(result.ResponseError, Does.Contain("No Owner work item found"));
    }

    [Test]
    public async Task GetViewByUser_ReturnsPackagesAndLabelOwners()
    {
        const int ownerId = 1;
        const int pkgId = 10;
        const int loId = 20;
        const int labelId = 30;

        // Owner has related package and label owner
        var ownerWi = MakeOwnerWorkItem(ownerId, "owner1", pkgId, loId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Owner'") && q.Contains("Custom.GitHubAlias") && q.Contains("owner1")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        // GetWorkItemsByIdsAsync for related IDs of the owner — returns package + label owner
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", language: ".NET", relatedIds: [ownerId, labelId]);
        var loWi = MakeLabelOwnerWorkItem(loId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", ownerId, labelId);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(pkgId) && ids.Contains(loId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi, loWi });

        // Hydration: fetch owners and labels for the package's and label owner's related IDs
        var ownerForHydration = MakeOwnerWorkItem(ownerId, "owner1");
        var labelForHydration = MakeLabelWorkItem(labelId, "Storage");

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { ownerForHydration, labelForHydration });

        var result = await _helper.GetViewByUser("owner1", null, CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));
        AssertPackage(result.Packages![0], "Azure.Storage.Blobs", ["owner1"], ["Storage"]);
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        AssertLabelOwner(result.PathBasedLabelOwners![0], "Azure/azure-sdk-for-net", "/sdk/storage");
    }

    [Test]
    public async Task GetViewByUser_WithRepoFilter_FiltersPackagesByLanguage()
    {
        var ownerId = 1;
        var netPkgId = 10;
        var pyPkgId = 11;

        var ownerWi = MakeOwnerWorkItem(ownerId, "owner1", netPkgId, pyPkgId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Owner'") && q.Contains("Custom.GitHubAlias") && q.Contains("owner1")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        var netPkg = MakePackageWorkItem(netPkgId, "Azure.Storage.Blobs", language: ".NET", relatedIds: [ownerId]);
        var pyPkg = MakePackageWorkItem(pyPkgId, "azure-storage-blob", language: "Python", relatedIds: [ownerId]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(netPkgId) && ids.Contains(pyPkgId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { netPkg, pyPkg });

        // Hydration for the one .NET package
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { MakeOwnerWorkItem(ownerId, "owner1") });

        var result = await _helper.GetViewByUser("owner1", "azure-sdk-for-python", CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));
        AssertPackage(result.Packages![0], "azure-storage-blob");
    }

    [Test]
    public async Task GetViewByUser_NoRelatedItems_ReturnsEmptyResult()
    {
        var ownerId = 1;
        var ownerWi = MakeOwnerWorkItem(ownerId, "owner1"); // no related IDs

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Owner'") && q.Contains("Custom.GitHubAlias") && q.Contains("owner1")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        // Empty response for no IDs
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByUser("owner1", null, CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Is.Null);
        Assert.That(result.PathlessLabelOwners, Is.Null);
    }

    // ========================
    // GetViewByLabel tests
    // ========================

    [Test]
    public async Task GetViewByLabel_LabelNotFound_ReturnsError()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label'") && q.Contains("Custom.Label") && q.Contains("NonExistent")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByLabel(["NonExistent"], null, CancellationToken.None);

        Assert.That(result.ResponseError, Does.Contain("No Label work item found for 'NonExistent'"));
    }

    [Test]
    public async Task GetViewByLabel_SingleLabel_ReturnsRelatedItems()
    {
        var labelId = 1;
        var pkgId = 10;
        var ownerId = 20;

        var labelWi = MakeLabelWorkItem(labelId, "Storage", pkgId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label'") && q.Contains("Custom.Label") && q.Contains("Storage")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelWi });

        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [ownerId, labelId]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(pkgId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner2"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        var result = await _helper.GetViewByLabel(["Storage"], null, CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));
        AssertPackage(result.Packages![0], "Azure.Storage.Blobs", ["owner2"]);
    }

    [Test]
    public async Task GetViewByLabel_MultipleLabels_IntersectsRelatedIds()
    {
        // Label A relates to items {10, 20, 30}
        // Label B relates to items {20, 30, 40}
        // Intersection should be {20, 30}
        var labelAId = 1;
        var labelBId = 2;
        var sharedPkgId = 20;
        var sharedLoId = 30;
        var ownerId = 50;

        var labelAWi = MakeLabelWorkItem(labelAId, "Storage", 10, sharedPkgId, sharedLoId);
        var labelBWi = MakeLabelWorkItem(labelBId, "Blobs", sharedPkgId, sharedLoId, 40);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label'") && q.Contains("Custom.Label") && q.Contains("Storage")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelAWi });
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label'") && q.Contains("Custom.Label") && q.Contains("Blobs")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelBWi });

        var pkgWi = MakePackageWorkItem(sharedPkgId, "Azure.Storage.Blobs", relatedIds: [ownerId, labelAId]);
        var loWi = MakeLabelOwnerWorkItem(sharedLoId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", ownerId, labelAId);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids =>
                    ids.Contains(sharedPkgId)
                    && ids.Contains(sharedLoId)
                    && !ids.Contains(10)
                    && !ids.Contains(40)
                ),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi, loWi });

        // Hydration: fetch owners and labels for the package's and label owner's related IDs
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelAId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner1"),
                MakeLabelWorkItem(labelAId, "Storage")
            });

        var result = await _helper.GetViewByLabel(["Storage", "Blobs"], null, CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));
        AssertPackage(result.Packages![0], "Azure.Storage.Blobs", ["owner1"], ["Storage"]);
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        AssertLabelOwner(result.PathBasedLabelOwners![0], "Azure/azure-sdk-for-net", "/sdk/storage", ["owner1"], ["Storage"]);
    }

    [Test]
    public async Task GetViewByLabel_SecondLabelNotFound_ReturnsErrorForSecond()
    {
        var labelWi = MakeLabelWorkItem(1, "Storage", 10);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label'") && q.Contains("Custom.Label") && q.Contains("Storage")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelWi });
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label'") && q.Contains("Custom.Label") && q.Contains("Missing")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByLabel(["Storage", "Missing"], null, CancellationToken.None);

        Assert.That(result.ResponseError, Does.Contain("No Label work item found for 'Missing'"));
    }

    // ========================
    // GetViewByPath tests
    // ========================

    [Test]
    public async Task GetViewByPath_ReturnsMatchingLabelOwners()
    {
        var loId = 1;
        var ownerId = 10;
        var labelId = 11;

        var loWi = MakeLabelOwnerWorkItem(loId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", ownerId, labelId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner2"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        var result = await _helper.GetViewByPath("/sdk/storage", null, CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Is.Null); // GetViewByPath returns no packages
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        AssertLabelOwner(result.PathBasedLabelOwners![0], "Azure/azure-sdk-for-net", "/sdk/storage", ["owner2"], ["Storage"]);
    }

    [Test]
    public async Task GetViewByPath_ReturnsSectionInResponse()
    {
        var loId = 1;
        var ownerId = 10;
        var labelId = 11;

        var loWi = MakeWorkItem(loId, "Label Owner", new Dictionary<string, object>
        {
            { "Custom.LabelType", "Service Owner" },
            { "Custom.Repository", "Azure/azure-sdk-for-net" },
            { "Custom.RepoPath", "/sdk/storage" },
            { "Custom.Section", "Test Section" }
        }, ownerId, labelId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner2"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        var result = await _helper.GetViewByPath("/sdk/storage", null, CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        AssertLabelOwner(result.PathBasedLabelOwners![0], "Azure/azure-sdk-for-net", "/sdk/storage",
            expectedOwners: ["owner2"], expectedLabels: ["Storage"], expectedSection: "Test Section");
    }

    [Test]
    public async Task GetViewByPath_WithRepoFilter_IncludesRepoInQuery()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && q.Contains("Azure/azure-sdk-for-net")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPath("/sdk/storage", "Azure/azure-sdk-for-net", CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        _mockDevOps.Verify(d => d.FetchWorkItemsPagedAsync(
            It.Is<string>(q => q.Contains("Custom.Repository") && q.Contains("Azure/azure-sdk-for-net")),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetViewByPath_NoMatches_ReturnsEmptyResult()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPath("/nonexistent/path", null, CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Is.Null);
        Assert.That(result.PathlessLabelOwners, Is.Null);
    }

    // ========================
    // GetViewByPackage tests
    // ========================

    [Test]
    public async Task GetViewByPackage_PackageNotFound_ReturnsError()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains("Custom.Package") && q.Contains("NoSuch.Package")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPackage("NoSuch.Package", default);

        Assert.That(result.ResponseError, Does.Contain("No Package work item found for 'NoSuch.Package'"));
    }

    [Test]
    public async Task GetViewByPackage_ReturnsHydratedPackageWithOwnersAndLabels()
    {
        var pkgId = 1;
        var ownerId = 10;
        var labelId = 11;

        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [ownerId, labelId]);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains("Custom.Package") && q.Contains("Azure.Storage.Blobs")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        // Hydration for package's related IDs
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner2"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        // FetchRelatedLabelOwners from the package's related IDs
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>()); // No label owners in this set

        var result = await _helper.GetViewByPackage("Azure.Storage.Blobs", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));
        AssertPackage(result.Packages![0], "Azure.Storage.Blobs", ["owner2"], ["Storage"]);
    }

    [Test]
    public async Task GetViewByPackage_MultipleVersions_ReturnsLatest()
    {
        var pkgV1Id = 1;
        var pkgV2Id = 2;

        var pkgV1Wi = MakePackageWorkItem(pkgV1Id, "Azure.Storage.Blobs", version: "1.0");
        var pkgV2Wi = MakePackageWorkItem(pkgV2Id, "Azure.Storage.Blobs", version: "2.0");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains("Custom.Package") && q.Contains("Azure.Storage.Blobs")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgV1Wi, pkgV2Wi });

        // Hydration
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPackage("Azure.Storage.Blobs", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));
        Assert.That(result.Packages![0].WorkItemId, Is.EqualTo(pkgV2Id));
    }

    [Test]
    public async Task GetViewByPackage_WithRepoFilter_FiltersByRepo()
    {
        var netPkgId = 1;
        var pyPkgId = 2;
        var owner1Id = 10;
        var owner2Id = 11;

        // This name collision between .NET and Python package identities is unlikely in practice,
        // but is useful here to validate repo/language filtering behavior.
        var netPkgWi = MakePackageWorkItem(netPkgId, "Azure.Storage.Blobs", language: ".NET", relatedIds: [owner1Id, owner2Id]);
        var pyPkgWi = MakePackageWorkItem(pyPkgId, "Azure.Storage.Blobs", language: "Python", relatedIds: [owner1Id]);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")
                    && q.Contains("Custom.Package")
                    && q.Contains("Azure.Storage.Blobs")
                    && q.Contains("Custom.Language")
                    && q.Contains(".NET")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { netPkgWi });

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")
                    && q.Contains("Custom.Package")
                    && q.Contains("Azure.Storage.Blobs")
                    && !q.Contains("Custom.Language")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unfiltered package query should not be called when repo filter is provided."));

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(owner1Id) && ids.Contains(owner2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(owner1Id, "owner1"),
                MakeOwnerWorkItem(owner2Id, "owner2")
            });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(owner1Id) && ids.Contains(owner2Id)),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPackage("Azure.Storage.Blobs", "Azure/azure-sdk-for-net", CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));
        Assert.That(result.Packages![0].WorkItemId, Is.EqualTo(netPkgId));
        AssertPackage(result.Packages![0], "Azure.Storage.Blobs", ["owner1", "owner2"]);

        _mockDevOps.Verify(d => d.FetchWorkItemsPagedAsync(
            It.Is<string>(q => q.Contains("'Package'")
                && q.Contains("Custom.Package")
                && q.Contains("Azure.Storage.Blobs")
                && q.Contains("Custom.Language")
                && q.Contains(".NET")),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================
    // Repo filter tests for label owners
    // ========================

    [Test]
    public async Task GetViewByUser_WithRepoFilter_FiltersLabelOwnersByRepo()
    {
        var ownerId = 1;
        var netLoId = 10;
        var pyLoId = 11;

        var ownerWi = MakeOwnerWorkItem(ownerId, "owner1", netLoId, pyLoId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Owner'") && q.Contains("Custom.GitHubAlias") && q.Contains("owner1")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        var netLo = MakeLabelOwnerWorkItem(netLoId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage");
        var pyLo = MakeLabelOwnerWorkItem(pyLoId, "Service Owner", "Azure/azure-sdk-for-python", "/sdk/storage");

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(netLoId) && ids.Contains(pyLoId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { netLo, pyLo });

        var result = await _helper.GetViewByUser("owner1", "Azure/azure-sdk-for-net", CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        Assert.That(result.PathBasedLabelOwners![0].Repo, Is.EqualTo("Azure/azure-sdk-for-net"));
    }

    // ========================
    // Team expansion tests
    // ========================

    [Test]
    public async Task GetViewByPackage_ExpandsTeamOwners()
    {
        var pkgId = 1;
        var teamOwnerId = 10;
        var labelId = 11;

        // Populate the team user cache with a team
        _mockTeamUserCache.Setup(c => c.GetUsersForTeam("azure/azure-sdk-team")).Returns(new List<string> { "owner1", "owner2", "owner3" });

        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [teamOwnerId, labelId]);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains("Azure.Storage.Blobs")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        // Hydration: owner is a team
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(teamOwnerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(teamOwnerId, "azure/azure-sdk-team"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(teamOwnerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPackage("Azure.Storage.Blobs", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));

        var teamOwner = result.Packages![0].Owners!.First(o => o.GitHubAlias == "azure/azure-sdk-team");
        Assert.That(teamOwner.Members, Is.EquivalentTo(new[] { "owner1", "owner2", "owner3" }));
    }

    [Test]
    public async Task GetViewByPath_ExpandsTeamOwnersOnLabelOwners()
    {
        var loId = 1;
        var teamOwnerId = 10;
        var labelId = 11;

        _mockTeamUserCache.Setup(c => c.GetUsersForTeam("azure/sdk-storage-team")).Returns(new List<string> { "owner4", "owner5" });

        var loWi = MakeLabelOwnerWorkItem(loId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", teamOwnerId, labelId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(teamOwnerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(teamOwnerId, "azure/sdk-storage-team"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        var result = await _helper.GetViewByPath("/sdk/storage", null, CancellationToken.None);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        var teamOwner = result.PathBasedLabelOwners![0].Owners!.First(o => o.GitHubAlias == "azure/sdk-storage-team");
        Assert.That(teamOwner.Members, Is.EquivalentTo(new[] { "owner4", "owner5" }));
    }

    // ========================
    // OwnerType.ToWorkItemString tests
    // ========================

    [TestCase(OwnerType.ServiceOwner, "Service Owner")]
    [TestCase(OwnerType.AzSdkOwner, "Azure SDK Owner")]
    [TestCase(OwnerType.PrLabel, "PR Label")]
    public void OwnerType_ToWorkItemString_ReturnsExpected(OwnerType input, string expected)
    {
        Assert.That(input.ToWorkItemString(), Is.EqualTo(expected));
    }

    // ========================
    // FindOrCreateLabelOwner tests
    // ========================

    [Test]
    public async Task FindOrCreateLabelOwner_Existing_ExactLabelMatch_ReturnsWithoutCreating()
    {
        var labelId = 100;
        var loWi = MakeLabelOwnerWorkItem(55, "Service Owner", "Azure/azure-sdk-for-net", "", labelId);
        var labelWiRaw = MakeLabelWorkItem(labelId, "Storage");
        var labelWi = new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" };

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("Azure/azure-sdk-for-net") && q.Contains("Service Owner") && q.Contains("Custom.Section") && q.Contains("Client Libraries")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelWiRaw });

        var result = await _helper.FindOrCreateLabelOwnerAsync("Azure/azure-sdk-for-net", OwnerType.ServiceOwner, null, [labelWi], "Client Libraries", default);

        Assert.That(result.WorkItemId, Is.EqualTo(55));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FindOrCreateLabelOwner_Existing_LabelMismatch_CreatesNew()
    {
        var otherLabelId = 200;
        var expectedLabelId = 100;
        var loWi = MakeLabelOwnerWorkItem(55, "Service Owner", "Azure/azure-sdk-for-net", "", otherLabelId);
        var otherLabelWiRaw = MakeLabelWorkItem(otherLabelId, "Networking");
        var expectedLabelWi = new LabelWorkItem { WorkItemId = expectedLabelId, LabelName = "Storage" };

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("Azure/azure-sdk-for-net") && q.Contains("Service Owner") && q.Contains("Custom.Section") && q.Contains("Client Libraries")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(otherLabelId)),
                It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { otherLabelWiRaw });

        var createdLoWi = MakeLabelOwnerWorkItem(99, "Service Owner", "Azure/azure-sdk-for-net", "");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: Storage", It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdLoWi);

        var result = await _helper.FindOrCreateLabelOwnerAsync("Azure/azure-sdk-for-net", OwnerType.ServiceOwner, null, [expectedLabelWi], "Client Libraries", default);

        Assert.That(result.WorkItemId, Is.EqualTo(99));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: Storage", It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task FindOrCreateLabelOwner_NotFound_Creates()
    {
        var labelWi = new LabelWorkItem { WorkItemId = 100, LabelName = "Storage" };

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("Azure/azure-sdk-for-net") && q.Contains("Custom.Section") && q.Contains("Client Libraries")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var createdLoWi = MakeLabelOwnerWorkItem(77, "Service Owner", "Azure/azure-sdk-for-net", "");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: Storage", It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdLoWi);

        var result = await _helper.FindOrCreateLabelOwnerAsync("Azure/azure-sdk-for-net", OwnerType.ServiceOwner, null, [labelWi], "Client Libraries", default);

        Assert.That(result.WorkItemId, Is.EqualTo(77));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: Storage", It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task FindOrCreateLabelOwner_WithPath_UsesPathInTitle()
    {
        var labelWi = new LabelWorkItem { WorkItemId = 100, LabelName = "Storage" };

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("sdk/service/") && q.Contains("Custom.Section") && q.Contains("Client Libraries")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var createdLoWi = MakeLabelOwnerWorkItem(88, "Service Owner", "Azure/azure-sdk-for-net", "sdk/service/");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: sdk/service/", It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdLoWi);

        var result = await _helper.FindOrCreateLabelOwnerAsync("Azure/azure-sdk-for-net", OwnerType.ServiceOwner, "sdk/service/", [labelWi], "Client Libraries", default);

        Assert.That(result.WorkItemId, Is.EqualTo(88));
    }

    // ========================
    // AddOwnersToPackage tests
    // ========================

    [Test]
    public async Task AddOwnerToPackage_PackageNotFound_ReturnsError()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains("NoSuchPackage")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var ownerWi = new OwnerWorkItem { GitHubAlias = "user1" };
        var result = await _helper.AddOwnersToPackage([ownerWi], "NoSuchPackage", "Azure/azure-sdk-for-net", default);

        Assert.That(result.ResponseError, Does.Contain("No Package work item found"));
    }

    [Test]
    public async Task AddOwnerToPackage_AlreadyLinked_SkipsAdd()
    {
        const int pkgId = 1, ownerId = 10;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [ownerId]);
        var ownerRawWi = MakeOwnerWorkItem(ownerId, "user1");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains("Azure.Storage.Blobs")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { ownerRawWi });

        var result = await _helper.AddOwnersToPackage([new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net", default);

        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(It.IsAny<int>(), It.Is<string>(s => s == "related"), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AddOwnerToPackage_NewLink_CreatesLink()
    {
        const int pkgId = 1, ownerId = 10;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains("Azure.Storage.Blobs")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(pkgId, "related", ownerId, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WorkItem());

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.AddOwnersToPackage([new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(pkgId, "related", ownerId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================
    // AddLabelsToPackage tests
    // ========================

    [Test]
    public async Task AddLabelsToPackage_AlreadyLinked_SkipsAdd()
    {
        const int pkgId = 1, labelId = 20;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [labelId]);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.AddLabelsToPackage([new LabelWorkItem { WorkItemId = labelId, LabelName = "StorageLabel" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net", default);

        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(It.IsAny<int>(), It.Is<string>(s => s == "related"), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AddLabelsToPackage_NewLink_CreatesLink()
    {
        const int pkgId = 1, labelId = 20;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(pkgId, "related", labelId, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WorkItem());
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.AddLabelsToPackage([new LabelWorkItem { WorkItemId = labelId, LabelName = "StorageLabel" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(pkgId, "related", labelId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================
    // RemoveOwnersFromPackage tests
    // ========================

    [Test]
    public async Task RemoveOwnerFromPackage_PackageNotFound_ReturnsError()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.RemoveOwnersFromPackage([new OwnerWorkItem { GitHubAlias = "user1" }], "NoSuchPackage", "Azure/azure-sdk-for-net", default);

        Assert.That(result.ResponseError, Does.Contain("No Package work item found"));
    }

    [Test]
    public async Task RemoveOwnerFromPackage_NotLinked_ReturnsSkipMessage()
    {
        const int pkgId = 1, ownerId = 10;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs"); // no relation to owner

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.RemoveOwnersFromPackage([new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net", default);

        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(It.IsAny<int>(), It.Is<string>(s => s == "related"), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RemoveOwnerFromPackage_Linked_RemovesLink()
    {
        const int pkgId = 1, ownerId = 10;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [ownerId]);
        var ownerRawWi = MakeOwnerWorkItem(ownerId, "user1");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(pkgId, "related", ownerId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { ownerRawWi });

        var result = await _helper.RemoveOwnersFromPackage([new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(pkgId, "related", ownerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================
    // RemoveLabelsFromPackage tests
    // ========================

    [Test]
    public async Task RemoveLabelsFromPackage_NotLinked_SkipsRemoval()
    {
        const int pkgId = 1, labelId = 20;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs"); // no label relation

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.RemoveLabelsFromPackage([new LabelWorkItem { WorkItemId = labelId, LabelName = "StorageLabel" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net", default);

        Assert.That(result.View, Is.Not.Null);
    }

    [Test]
    public async Task RemoveLabelsFromPackage_Linked_RemovesLink()
    {
        const int pkgId = 1, labelId = 20;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [labelId]);
        var labelRawWi = MakeLabelWorkItem(labelId, "StorageLabel");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(pkgId, "related", labelId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelRawWi });

        var result = await _helper.RemoveLabelsFromPackage([new LabelWorkItem { WorkItemId = labelId, LabelName = "StorageLabel" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(pkgId, "related", labelId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================
    // AddOwnersAndLabelsToPath tests
    // ========================

    [Test]
    public async Task AddOwnersAndLabelsToPath_ExistingLabelOwner_AddsNewOwner()
    {
        const int labelOwnerWiId = 100;
        const int ownerId = 10;
        const int labelId = 20;

        // FindOrCreateLabelOwnerAsync will query for existing Label Owner
        var labelOwnerRawWi = MakeLabelOwnerWorkItem(labelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", labelId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/storage") && q.Contains("Azure/azure-sdk-for-net")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration of the label owner candidate — returns the label work item so SetEquals matches
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(labelId, "Storage") });

        // Label is already linked to the label owner, so CreateWorkItemRelationAsync for label should NOT be called
        // Owner is NOT related, so CreateWorkItemRelationAsync for owner should be called
        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", ownerId, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WorkItem());

        // GetViewByPath at the end — return empty for simplicity
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", ownerId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Verify FindOrCreateLabelOwnerAsync included section in WIQL query
        _mockDevOps.Verify(d => d.FetchWorkItemsPagedAsync(
            It.Is<string>(q => q.Contains("Custom.Section") && q.Contains("Client Libraries")),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task AddOwnersAndLabelsToPath_OwnerAlreadyLinked_SkipsAdd()
    {
        const int labelOwnerWiId = 100;
        const int ownerId = 10;
        const int labelId = 20;

        // Label owner already has the owner linked
        var labelOwnerRawWi = MakeLabelOwnerWorkItem(labelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", labelId, ownerId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/storage")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration — label matches so FindOrCreateLabelOwnerAsync returns existing
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(labelId, "Storage") });

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", ownerId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AddOwnersAndLabelsToPath_MultipleOwners_MixedResult()
    {
        const int labelOwnerWiId = 100;
        const int owner1Id = 10;
        const int owner2Id = 11;
        const int labelId = 20;

        // Label owner already has owner1 linked but not owner2
        var labelOwnerRawWi = MakeLabelOwnerWorkItem(labelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", labelId, owner1Id);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/storage")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(labelId, "Storage") });

        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", owner2Id, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WorkItem());

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[]
        {
            new OwnerWorkItem { WorkItemId = owner1Id, GitHubAlias = "existingUser" },
            new OwnerWorkItem { WorkItemId = owner2Id, GitHubAlias = "newUser" }
        };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", owner1Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", owner2Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AddOwnersAndLabelsToPath_NoExistingLabelOwner_CreatesNew()
    {
        const int newLabelOwnerWiId = 200;
        const int ownerId = 10;
        const int labelId = 20;

        // No existing label owner found
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/newpath")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        // CreateWorkItemAsync returns a new Label Owner work item
        var createdWi = MakeLabelOwnerWorkItem(newLabelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/newpath");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", It.Is<string>(t => t.Contains("/sdk/newpath")), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdWi);

        // Link label to the new label owner
        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", labelId, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WorkItem());
        // Link owner to the new label owner
        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", ownerId, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WorkItem());

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/newpath") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/newpath", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), "Label Owner", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", labelId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", ownerId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AddOwnersAndLabelsToPath_CreatesNewLabelOwnerWhenLabelsDoNotMatch()
    {
        const int labelOwnerWiId = 100;
        const int ownerId = 10;
        const int label1Id = 20;
        const int label2Id = 21;

        // Label owner has label1 linked but not label2
        var labelOwnerRawWi = MakeLabelOwnerWorkItem(labelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", label1Id);

        // FindOrCreateLabelOwnerAsync: no exact match (label set differs), so it creates a new one
        // But we need both labels to match for the existing label owner to be returned.
        // Since label sets don't match, a new one will be created.
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/storage")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration returns only label1
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(label1Id)),
                It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(label1Id, "Storage") });

        // No exact label-set match, so a new Label Owner is created
        const int newLabelOwnerWiId = 200;
        var createdWi = MakeLabelOwnerWorkItem(newLabelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdWi);

        // Link both labels and owner to the new label owner
        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WorkItem());

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[]
        {
            new LabelWorkItem { WorkItemId = label1Id, LabelName = "Storage" },
            new LabelWorkItem { WorkItemId = label2Id, LabelName = "Blobs" }
        };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        // Both labels should be linked to the new label owner
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", label1Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", label2Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", ownerId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================
    // RemoveOwnersFromLabelsAndPath tests
    // ========================

    [Test]
    public async Task RemoveOwnersFromLabelsAndPath_NoLabelOwnerFound_ReturnsError()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/nopath") && q.Contains("Custom.Section") && q.Contains("Client Libraries")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = 20, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/nopath", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Does.Contain("No Label Owner work item found for path '/sdk/nopath'"));
    }

    [Test]
    public async Task RemoveOwnersFromLabelsAndPath_OwnerLinked_RemovesLink()
    {
        const int labelOwnerWiId = 100;
        const int ownerId = 10;
        const int labelId = 20;

        // QueryLabelOwnersByPath returns a label owner with the owner and label linked
        var labelOwnerRawWi = MakeLabelOwnerWorkItem(labelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", ownerId, labelId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/storage")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration of the label owner — returns owner and label work items
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "user1"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", ownerId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", ownerId, It.IsAny<CancellationToken>()), Times.Once);
        // Verify QueryLabelOwnersByPath included section in WIQL query
        _mockDevOps.Verify(d => d.FetchWorkItemsPagedAsync(
            It.Is<string>(q => q.Contains("Custom.Section") && q.Contains("Client Libraries")),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task RemoveOwnersFromLabelsAndPath_OwnerNotLinked_SkipsRemoval()
    {
        const int labelOwnerWiId = 100;
        const int ownerId = 10;
        const int labelId = 20;

        // Label owner has only the label linked, not the owner
        var labelOwnerRawWi = MakeLabelOwnerWorkItem(labelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", labelId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/storage")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration — only label, no owner
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(labelId, "Storage") });

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(It.IsAny<int>(), "related", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        // Verify QueryLabelOwnersByPath included section in WIQL query
        _mockDevOps.Verify(d => d.FetchWorkItemsPagedAsync(
            It.Is<string>(q => q.Contains("Custom.Section") && q.Contains("Client Libraries")),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task RemoveOwnersFromLabelsAndPath_MultipleOwners_MixedResult()
    {
        const int labelOwnerWiId = 100;
        const int owner1Id = 10;
        const int owner2Id = 11;
        const int labelId = 20;

        // Label owner has owner1 linked but not owner2
        var labelOwnerRawWi = MakeLabelOwnerWorkItem(labelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", owner1Id, labelId);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/storage")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(owner1Id) && ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(owner1Id, "linkedUser"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", owner1Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[]
        {
            new OwnerWorkItem { WorkItemId = owner1Id, GitHubAlias = "linkedUser" },
            new OwnerWorkItem { WorkItemId = owner2Id, GitHubAlias = "notLinkedUser" }
        };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.View, Is.Not.Null);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", owner1Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", owner2Id, It.IsAny<CancellationToken>()), Times.Never);
        // Verify QueryLabelOwnersByPath included section in WIQL query
        _mockDevOps.Verify(d => d.FetchWorkItemsPagedAsync(
            It.Is<string>(q => q.Contains("Custom.Section") && q.Contains("Client Libraries")),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task RemoveOwnersFromLabelsAndPath_MultipleLabelOwners_MatchesCorrectOne()
    {
        const int labelOwner1Id = 100;
        const int labelOwner2Id = 101;
        const int ownerId = 10;
        const int label1Id = 20;
        const int label2Id = 21;

        // Two label owners at the same path but with different label sets
        var lo1RawWi = MakeLabelOwnerWorkItem(labelOwner1Id, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", label1Id, ownerId);
        var lo2RawWi = MakeLabelOwnerWorkItem(labelOwner2Id, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", label2Id);

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/storage")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem> { lo1RawWi, lo2RawWi });

        // Hydration — fetch all related IDs from both label owners
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<int>(), WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "user1"),
                MakeLabelWorkItem(label1Id, "Storage"),
                MakeLabelWorkItem(label2Id, "Blobs")
            });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(labelOwner1Id, "related", ownerId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        // Request removal from the label owner that has label1 ("Storage")
        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = label1Id, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", OwnerType.ServiceOwner, "Client Libraries", default);

        Assert.That(result.ResponseError, Is.Null);
        // Should remove from labelOwner1 (which has label1), not labelOwner2
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwner1Id, "related", ownerId, It.IsAny<CancellationToken>()), Times.Once);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwner2Id, "related", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        // Verify QueryLabelOwnersByPath included section in WIQL query
        _mockDevOps.Verify(d => d.FetchWorkItemsPagedAsync(
            It.Is<string>(q => q.Contains("Custom.Section") && q.Contains("Client Libraries")),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ========================
    // ThrowIfInvalidTeamAlias tests
    // ========================

    private static Team CreateTeam(string slug, Team? parent = null)
    {
        return new Team(
            url: $"https://api.github.com/orgs/Azure/teams/{slug}",
            htmlUrl: $"https://github.com/orgs/Azure/teams/{slug}",
            id: slug.GetHashCode(),
            nodeId: $"T_{slug.GetHashCode()}",
            slug: slug,
            name: slug,
            description: "",
            privacy: TeamPrivacy.Closed,
            permission: "push",
            teamRepositoryPermissions: null,
            membersCount: 1,
            reposCount: 0,
            organization: null,
            parent: parent,
            ldapDistinguishedName: null
        );
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_FoundInCache_DoesNotThrow()
    {
        var alias = "Azure/my-cached-team";
        _mockTeamUserCache
            .Setup(c => c.TeamUserDict)
            .Returns(new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "my-cached-team", new List<string> { "user1", "user2" } }
            });

        Assert.DoesNotThrowAsync(() => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));

        // Should not call GitHub API when found in cache
        _mockGitHub.Verify(
            g => g.GetTeamByNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_FoundInCache_EmptyTeam_DoesNotThrow()
    {
        var alias = "Azure/azure-sdk-partners";
        _mockTeamUserCache
            .Setup(c => c.TeamUserDict)
            .Returns(new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "azure-sdk-partners", new List<string>() }
            });

        Assert.DoesNotThrowAsync(() => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));

        // Should not call GitHub API when the team key exists in the cache, even with zero members
        _mockGitHub.Verify(
            g => g.GetTeamByNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_NotInCache_DirectlyIsAzureSdkWrite_DoesNotThrow()
    {
        var alias = "Azure/azure-sdk-write";
        var team = CreateTeam("azure-sdk-write");

        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "azure-sdk-write", It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        Assert.DoesNotThrowAsync(() => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));
        _mockGitHub.Verify(g => g.GetTeamByNameAsync("Azure", "azure-sdk-write", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_NotInCache_ParentIsAzureSdkWrite_DoesNotThrow()
    {
        var alias = "Azure/my-child-team";
        var parentTeam = CreateTeam("azure-sdk-write");
        var childTeam = CreateTeam("my-child-team", parent: parentTeam);

        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "my-child-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync(childTeam);
        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "azure-sdk-write", It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentTeam);

        Assert.DoesNotThrowAsync(() => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));
        _mockGitHub.Verify(g => g.GetTeamByNameAsync("Azure", "my-child-team", It.IsAny<CancellationToken>()), Times.Once);
        _mockGitHub.Verify(g => g.GetTeamByNameAsync("Azure", "azure-sdk-write", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_NotInCache_GrandparentIsAzureSdkWrite_DoesNotThrow()
    {
        var alias = "Azure/deep-child-team";
        var azureSdkWrite = CreateTeam("azure-sdk-write");
        var midTeam = CreateTeam("mid-team", parent: azureSdkWrite);
        var deepChild = CreateTeam("deep-child-team", parent: midTeam);

        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "deep-child-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deepChild);
        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "mid-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync(midTeam);
        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "azure-sdk-write", It.IsAny<CancellationToken>()))
            .ReturnsAsync(azureSdkWrite);

        Assert.DoesNotThrowAsync(() => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));
        _mockGitHub.Verify(g => g.GetTeamByNameAsync("Azure", "deep-child-team", It.IsAny<CancellationToken>()), Times.Once);
        _mockGitHub.Verify(g => g.GetTeamByNameAsync("Azure", "mid-team", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_NotInCache_NotDescendantOfAzureSdkWrite_Throws()
    {
        var alias = "Azure/unrelated-team";
        var unrelatedTeam = CreateTeam("unrelated-team", parent: null);

        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "unrelated-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync(unrelatedTeam);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("is not a child of 'azure-sdk-write'"));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_NotInCache_ParentChainDoesNotReachAzureSdkWrite_Throws()
    {
        var alias = "Azure/wrong-tree-team";
        var otherRoot = CreateTeam("other-root", parent: null);
        var wrongTreeTeam = CreateTeam("wrong-tree-team", parent: otherRoot);

        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "wrong-tree-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync(wrongTreeTeam);
        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "other-root", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherRoot);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("is not a child of 'azure-sdk-write'"));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_InvalidFormat_MultipleSlashes_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias("Azure/sub/team", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Team aliases must be in the format '<org>/<team>' with exactly one '/'"));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_NoSlash_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias("just-a-name", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Team aliases must be in the format '<org>/<team>' with exactly one '/'"));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_NotInCache_NotFoundOnGitHub_Throws()
    {
        var alias = "Azure/nonexistent-team";

        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "nonexistent-team", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Octokit.NotFoundException("Not Found", System.Net.HttpStatusCode.NotFound));

        Assert.ThrowsAsync<Octokit.NotFoundException>(
            () => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_AtPrefixed_FoundInCache_DoesNotThrow()
    {
        var alias = "@Azure/azure-sdk-eng";
        _mockTeamUserCache
            .Setup(c => c.TeamUserDict)
            .Returns(new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "azure-sdk-eng", new List<string> { "engineer1", "engineer2" } }
            });

        Assert.DoesNotThrowAsync(() => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));

        // Should not call GitHub API when found in cache
        _mockGitHub.Verify(
            g => g.GetTeamByNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_AtPrefixed_NotInCache_ValidatesViaGitHub()
    {
        var alias = "@Azure/azure-sdk-release";
        var parentTeam = CreateTeam("azure-sdk-write");
        var childTeam = CreateTeam("azure-sdk-release", parent: parentTeam);

        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "azure-sdk-release", It.IsAny<CancellationToken>()))
            .ReturnsAsync(childTeam);
        _mockGitHub
            .Setup(g => g.GetTeamByNameAsync("Azure", "azure-sdk-write", It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentTeam);

        Assert.DoesNotThrowAsync(() => _helper.ThrowIfInvalidTeamAlias(alias, CancellationToken.None));
        _mockGitHub.Verify(g => g.GetTeamByNameAsync("Azure", "azure-sdk-release", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_EmptyTeamName_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias("Azure/", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Both the organization and team name must be non-empty"));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_EmptyOrgName_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias("/azure-sdk-core", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Both the organization and team name must be non-empty"));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_AtPrefixedEmptyOrg_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias("@/azure-sdk-tooling", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Both the organization and team name must be non-empty"));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_NonAzureOrg_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias("contoso/azure-sdk-storage", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Only teams in the 'Azure' organization are supported"));
    }

    [Test]
    public void ThrowIfInvalidTeamAlias_AtPrefixedNonAzureOrg_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _helper.ThrowIfInvalidTeamAlias("@microsoft/azure-sdk-infra", CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Only teams in the 'Azure' organization are supported"));
    }

    // ========================
    // CheckPackageOwners tests
    // ========================

    /// <summary>
    /// Sets up FetchWorkItemsPagedAsync to return label owners by type and repo.
    /// </summary>
    private void SetupLabelOwnerQuery(string labelType, string repo, List<WorkItem> results)
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains($"'{labelType}'") && q.Contains(repo)),
                It.IsAny<int>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);
    }

    private void SetupPackageQuery(string packageName, List<WorkItem> results)
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains(packageName)),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);
    }

    private void SetupHydration(List<int> expectedIds, List<WorkItem> results)
    {
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => expectedIds.All(id => ids.Contains(id))),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);
    }

    // Test 1: Primary path — all checks pass
    [Test]
    public async Task CheckPackageOwners_AllChecksPass()
    {
        const int pkgId = 100, owner1Id = 201, owner2Id = 202, labelId = 301, soId = 400;
        const string repo = "Azure/azure-sdk-for-net";

        // Package with 2 owners + 1 label
        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [owner1Id, owner2Id, labelId])]);
        // Hydrate package -> owners + label
        SetupHydration([owner1Id, owner2Id, labelId], [
            MakeOwnerWorkItem(owner1Id, "user1"),
            MakeOwnerWorkItem(owner2Id, "user2"),
            MakeLabelWorkItem(labelId, "TestLabel")
        ]);
        // Service Owner Label Owner with superset labels and 2 owners
        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", owner1Id, owner2Id, labelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        // Hydrate SO -> owners + labels
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(owner1Id) && ids.Contains(owner2Id) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(owner1Id, "user1"),
                MakeOwnerWorkItem(owner2Id, "user2"),
                MakeLabelWorkItem(labelId, "TestLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.AllPassed, Is.True);
        Assert.That(result.ValidationPath, Is.EqualTo("Package"));
        Assert.That(result.OwnerCheck!.Passed, Is.True);
        Assert.That(result.PrLabelCheck!.Passed, Is.True);
        Assert.That(result.ServiceOwnerCheck!.Passed, Is.True);
        Assert.That(result.ExitCode, Is.EqualTo(0));
    }

    // Test 2: Package not found
    [Test]
    public async Task CheckPackageOwners_PackageNotFound_ReturnsError()
    {
        SetupPackageQuery("NonExistent.Pkg", []);

        var result = await _helper.CheckPackageOwners("NonExistent.Pkg", "sdk/test", "Azure/azure-sdk-for-net", CancellationToken.None);

        Assert.That(result.ResponseError, Does.Contain("No Package work item found"));
        Assert.That(result.AllPassed, Is.False);
        Assert.That(result.ExitCode, Is.EqualTo(1));
    }

    // Test 3: Insufficient owners (1 owner < 2 required) — no fallback
    [Test]
    public async Task CheckPackageOwners_InsufficientOwners_Fails()
    {
        const int pkgId = 100, ownerId = 201, labelId = 301, soId = 400;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [ownerId, labelId])]);
        SetupHydration([ownerId, labelId], [
            MakeOwnerWorkItem(ownerId, "user1"),
            MakeLabelWorkItem(labelId, "TestLabel")
        ]);
        // Service Owner
        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", ownerId, labelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(ownerId, "user1"),
                MakeLabelWorkItem(labelId, "TestLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.ValidationPath, Is.EqualTo("Package"));
        Assert.That(result.OwnerCheck!.Passed, Is.False);
        Assert.That(result.OwnerCheck.Actual, Is.EqualTo(1));
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 4: No owners → triggers path fallback
    [Test]
    public async Task CheckPackageOwners_NoOwners_TriggersPathFallback()
    {
        const int pkgId = 100, labelId = 301;
        const int prLabelLoId = 500, prOwner1Id = 601, prOwner2Id = 602, prLabelLabelId = 701;
        const int soId = 800, soOwner1Id = 901, soOwner2Id = 902;
        const string repo = "Azure/azure-sdk-for-net";

        // Package with no owners
        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [labelId])]);
        SetupHydration([labelId], [MakeLabelWorkItem(labelId, "TestLabel")]);

        // PR Label Label Owner matching path
        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/test/", prOwner1Id, prOwner2Id, prLabelLabelId);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        // Hydrate PR Label Label Owner
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwner1Id) && ids.Contains(prOwner2Id) && ids.Contains(prLabelLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwner1Id, "prUser1"),
                MakeOwnerWorkItem(prOwner2Id, "prUser2"),
                MakeLabelWorkItem(prLabelLabelId, "PrLabel1")
            ]);

        // Service Owner matching labels
        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwner1Id, soOwner2Id, prLabelLabelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id) && ids.Contains(prLabelLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(prLabelLabelId, "PrLabel1")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.ValidationPath, Is.EqualTo("PathFallback"));
        Assert.That(result.PathFallbackCheck, Is.Not.Null);
        Assert.That(result.PathFallbackCheck!.PrLabelOwnerCheck!.Passed, Is.True);
        Assert.That(result.PathFallbackCheck!.ServiceOwnerCheck!.Passed, Is.True);
        Assert.That(result.AllPassed, Is.True);
    }

    // Test 5: Package has owners but no PR labels
    [Test]
    public async Task CheckPackageOwners_NoPrLabel_Fails()
    {
        const int pkgId = 100, owner1Id = 201, owner2Id = 202;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [owner1Id, owner2Id])]);
        SetupHydration([owner1Id, owner2Id], [
            MakeOwnerWorkItem(owner1Id, "user1"),
            MakeOwnerWorkItem(owner2Id, "user2"),
        ]);
        SetupLabelOwnerQuery("Service Owner", repo, []);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.ValidationPath, Is.EqualTo("Package"));
        Assert.That(result.OwnerCheck!.Passed, Is.True);
        Assert.That(result.PrLabelCheck!.Passed, Is.False);
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 6: Insufficient service owners (only 1 individual)
    [Test]
    public async Task CheckPackageOwners_InsufficientServiceOwners_Fails()
    {
        const int pkgId = 100, owner1Id = 201, owner2Id = 202, labelId = 301, soId = 400, soOwnerId = 501;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [owner1Id, owner2Id, labelId])]);
        SetupHydration([owner1Id, owner2Id, labelId], [
            MakeOwnerWorkItem(owner1Id, "user1"),
            MakeOwnerWorkItem(owner2Id, "user2"),
            MakeLabelWorkItem(labelId, "TestLabel")
        ]);

        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwnerId, labelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwnerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwnerId, "soUser1"),
                MakeLabelWorkItem(labelId, "TestLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.ServiceOwnerCheck!.Passed, Is.False);
        Assert.That(result.ServiceOwnerCheck.Actual, Is.EqualTo(1));
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 7: No service owner has labels that are a superset of package labels
    [Test]
    public async Task CheckPackageOwners_NoServiceOwners_Fails()
    {
        const int pkgId = 100, owner1Id = 201, owner2Id = 202, labelId = 301;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [owner1Id, owner2Id, labelId])]);
        SetupHydration([owner1Id, owner2Id, labelId], [
            MakeOwnerWorkItem(owner1Id, "user1"),
            MakeOwnerWorkItem(owner2Id, "user2"),
            MakeLabelWorkItem(labelId, "TestLabel")
        ]);

        SetupLabelOwnerQuery("Service Owner", repo, []);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.ServiceOwnerCheck!.Passed, Is.False);
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 8: Team expansion counts individuals — 1 team with 3 members satisfies 2-owner requirement
    [Test]
    public async Task CheckPackageOwners_TeamExpansion_CountsIndividuals()
    {
        const int pkgId = 100, teamOwnerId = 201, labelId = 301, soId = 400, so1Id = 501, so2Id = 502;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [teamOwnerId, labelId])]);

        // Team owner with 3 expanded members
        _mockTeamUserCache.Setup(c => c.GetUsersForTeam("azure/test-team")).Returns(["member1", "member2", "member3"]);

        SetupHydration([teamOwnerId, labelId], [
            MakeOwnerWorkItem(teamOwnerId, "azure/test-team"),
            MakeLabelWorkItem(labelId, "TestLabel")
        ]);

        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", so1Id, so2Id, labelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(so1Id) && ids.Contains(so2Id) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(so1Id, "soUser1"),
                MakeOwnerWorkItem(so2Id, "soUser2"),
                MakeLabelWorkItem(labelId, "TestLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.OwnerCheck!.Passed, Is.True);
        Assert.That(result.OwnerCheck.Actual, Is.EqualTo(3));
        Assert.That(result.AllPassed, Is.True);
    }

    // Test 9: Team expansion for service owners
    [Test]
    public async Task CheckPackageOwners_TeamExpansion_ServiceOwners()
    {
        const int pkgId = 100, owner1Id = 201, owner2Id = 202, labelId = 301, soId = 400, soTeamId = 501;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [owner1Id, owner2Id, labelId])]);
        SetupHydration([owner1Id, owner2Id, labelId], [
            MakeOwnerWorkItem(owner1Id, "user1"),
            MakeOwnerWorkItem(owner2Id, "user2"),
            MakeLabelWorkItem(labelId, "TestLabel")
        ]);

        _mockTeamUserCache.Setup(c => c.GetUsersForTeam("azure/so-team")).Returns(["soMember1", "soMember2", "soMember3"]);

        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soTeamId, labelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soTeamId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soTeamId, "azure/so-team"),
                MakeLabelWorkItem(labelId, "TestLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.ServiceOwnerCheck!.Passed, Is.True);
        Assert.That(result.ServiceOwnerCheck.Actual, Is.EqualTo(3));
        Assert.That(result.AllPassed, Is.True);
    }

    // Test 10: Multiple labels — Service Owner must have all labels (superset)
    [Test]
    public async Task CheckPackageOwners_MultipleLabels_ServiceOwnerSupersetRequired()
    {
        const int pkgId = 100, owner1Id = 201, owner2Id = 202, label1Id = 301, label2Id = 302;
        const int soId = 400, soOwner1Id = 501, soOwner2Id = 502;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [owner1Id, owner2Id, label1Id, label2Id])]);
        SetupHydration([owner1Id, owner2Id, label1Id, label2Id], [
            MakeOwnerWorkItem(owner1Id, "user1"),
            MakeOwnerWorkItem(owner2Id, "user2"),
            MakeLabelWorkItem(label1Id, "LabelA"),
            MakeLabelWorkItem(label2Id, "LabelB")
        ]);

        // Service Owner has both labels
        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwner1Id, soOwner2Id, label1Id, label2Id);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id) && ids.Contains(label1Id) && ids.Contains(label2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(label1Id, "LabelA"),
                MakeLabelWorkItem(label2Id, "LabelB")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.ServiceOwnerCheck!.Passed, Is.True);
        Assert.That(result.AllPassed, Is.True);
    }

    // Test 11: Fragmented service owners — multiple SO records collectively cover labels but no single one does
    [Test]
    public async Task CheckPackageOwners_FragmentedServiceOwners_Fails()
    {
        const int pkgId = 100, owner1Id = 201, owner2Id = 202, label1Id = 301, label2Id = 302;
        const int so1Id = 400, so2Id = 401, soOwner1Id = 501, soOwner2Id = 502;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [owner1Id, owner2Id, label1Id, label2Id])]);
        SetupHydration([owner1Id, owner2Id, label1Id, label2Id], [
            MakeOwnerWorkItem(owner1Id, "user1"),
            MakeOwnerWorkItem(owner2Id, "user2"),
            MakeLabelWorkItem(label1Id, "LabelA"),
            MakeLabelWorkItem(label2Id, "LabelB")
        ]);

        // SO 1 has LabelA only, SO 2 has LabelB only
        var so1Wi = MakeLabelOwnerWorkItem(so1Id, "Service Owner", repo, "", soOwner1Id, soOwner2Id, label1Id);
        var so2Wi = MakeLabelOwnerWorkItem(so2Id, "Service Owner", repo, "", soOwner1Id, soOwner2Id, label2Id);
        SetupLabelOwnerQuery("Service Owner", repo, [so1Wi, so2Wi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(label1Id, "LabelA"),
                MakeLabelWorkItem(label2Id, "LabelB")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.ServiceOwnerCheck!.Passed, Is.False);
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 12: Multiple package versions — uses latest
    [Test]
    public async Task CheckPackageOwners_MultiplePackageVersions_UsesLatestByName()
    {
        const int pkg1Id = 100, pkg2Id = 101, owner1Id = 201, owner2Id = 202, labelId = 301;
        const int soId = 400, soOwner1Id = 501, soOwner2Id = 502;
        const string repo = "Azure/azure-sdk-for-net";

        // Two versions of same package: 1.0 and 2.0
        SetupPackageQuery("Azure.Test.Pkg", [
            MakePackageWorkItem(pkg1Id, "Azure.Test.Pkg", version: "1.0", relatedIds: []),
            MakePackageWorkItem(pkg2Id, "Azure.Test.Pkg", version: "2.0", relatedIds: [owner1Id, owner2Id, labelId])
        ]);
        SetupHydration([owner1Id, owner2Id, labelId], [
            MakeOwnerWorkItem(owner1Id, "user1"),
            MakeOwnerWorkItem(owner2Id, "user2"),
            MakeLabelWorkItem(labelId, "TestLabel")
        ]);

        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwner1Id, soOwner2Id, labelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(labelId, "TestLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        Assert.That(result.AllPassed, Is.True);
        Assert.That(result.OwnerCheck!.Actual, Is.EqualTo(2));
    }

    // Test 13: Overlapping owners — same user in team + individual doesn't double-count
    [Test]
    public async Task CheckPackageOwners_OverlappingOwners_Deduplication()
    {
        const int pkgId = 100, indivOwnerId = 201, teamOwnerId = 202, labelId = 301;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [indivOwnerId, teamOwnerId, labelId])]);

        // Team contains same user as individual owner
        _mockTeamUserCache.Setup(c => c.GetUsersForTeam("azure/test-team")).Returns(["user1"]);

        SetupHydration([indivOwnerId, teamOwnerId, labelId], [
            MakeOwnerWorkItem(indivOwnerId, "user1"),
            MakeOwnerWorkItem(teamOwnerId, "azure/test-team"),
            MakeLabelWorkItem(labelId, "TestLabel")
        ]);
        SetupLabelOwnerQuery("Service Owner", repo, []);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test", repo, CancellationToken.None);

        // user1 appears both individually and in team — should count as 1
        Assert.That(result.OwnerCheck!.Actual, Is.EqualTo(1));
        Assert.That(result.OwnerCheck.Passed, Is.False);
    }

    // Test 14: Fallback — both PR Label and Service Owner pass
    [Test]
    public async Task CheckPackageOwners_Fallback_BothPrLabelAndServiceOwnerPass()
    {
        const int pkgId = 100;
        const int prLabelLoId = 500, prOwner1Id = 601, prOwner2Id = 602, prLabelId = 701;
        const int soId = 800, soOwner1Id = 901, soOwner2Id = 902;
        const string repo = "Azure/azure-sdk-for-net";

        // Package with no owners
        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/test/", prOwner1Id, prOwner2Id, prLabelId);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwner1Id) && ids.Contains(prOwner2Id) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwner1Id, "prUser1"),
                MakeOwnerWorkItem(prOwner2Id, "prUser2"),
                MakeLabelWorkItem(prLabelId, "FallbackLabel")
            ]);

        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwner1Id, soOwner2Id, prLabelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(prLabelId, "FallbackLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.ValidationPath, Is.EqualTo("PathFallback"));
        Assert.That(result.AllPassed, Is.True);
        Assert.That(result.PathFallbackCheck!.PrLabelOwnerCheck!.Passed, Is.True);
        Assert.That(result.PathFallbackCheck.ServiceOwnerCheck!.Passed, Is.True);
    }

    // Test 15: Fallback — PR Label owners insufficient
    [Test]
    public async Task CheckPackageOwners_Fallback_PrLabelOwnersInsufficient()
    {
        const int pkgId = 100;
        const int prLabelLoId = 500, prOwnerId = 601, prLabelId = 701;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/test/", prOwnerId, prLabelId);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwnerId) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwnerId, "prUser1"),
                MakeLabelWorkItem(prLabelId, "FallbackLabel")
            ]);

        // Still need SO query for complete check
        SetupLabelOwnerQuery("Service Owner", repo, []);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.PathFallbackCheck!.PrLabelOwnerCheck!.Passed, Is.False);
        Assert.That(result.PathFallbackCheck.PrLabelOwnerCheck.Actual, Is.EqualTo(1));
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 16: Fallback — Service Owners insufficient
    [Test]
    public async Task CheckPackageOwners_Fallback_ServiceOwnersInsufficient()
    {
        const int pkgId = 100;
        const int prLabelLoId = 500, prOwner1Id = 601, prOwner2Id = 602, prLabelId = 701;
        const int soId = 800, soOwnerId = 901;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/test/", prOwner1Id, prOwner2Id, prLabelId);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwner1Id) && ids.Contains(prOwner2Id) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwner1Id, "prUser1"),
                MakeOwnerWorkItem(prOwner2Id, "prUser2"),
                MakeLabelWorkItem(prLabelId, "FallbackLabel")
            ]);

        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwnerId, prLabelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwnerId) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwnerId, "soUser1"),
                MakeLabelWorkItem(prLabelId, "FallbackLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.PathFallbackCheck!.PrLabelOwnerCheck!.Passed, Is.True);
        Assert.That(result.PathFallbackCheck.ServiceOwnerCheck!.Passed, Is.False);
        Assert.That(result.PathFallbackCheck.ServiceOwnerCheck.Actual, Is.EqualTo(1));
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 17: Fallback — no matching paths
    [Test]
    public async Task CheckPackageOwners_Fallback_NoMatchingPaths()
    {
        const int pkgId = 100;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // PR Label Label Owner with non-matching path
        var prLabelLo = MakeLabelOwnerWorkItem(500, "PR Label", repo, "/sdk/other/");
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.ValidationPath, Is.EqualTo("PathFallback"));
        Assert.That(result.PathFallbackCheck!.PrLabelOwnerCheck!.Passed, Is.False);
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 18: Fallback — glob match
    [Test]
    public async Task CheckPackageOwners_Fallback_GlobMatch()
    {
        const int pkgId = 100;
        const int prLabelLoId = 500, prOwner1Id = 601, prOwner2Id = 602, prLabelId = 701;
        const int soId = 800, soOwner1Id = 901, soOwner2Id = 902;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // PR Label Label Owner with glob path /sdk/contoso/
        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/contoso/", prOwner1Id, prOwner2Id, prLabelId);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwner1Id) && ids.Contains(prOwner2Id) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwner1Id, "prUser1"),
                MakeOwnerWorkItem(prOwner2Id, "prUser2"),
                MakeLabelWorkItem(prLabelId, "ContosoLabel")
            ]);

        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwner1Id, soOwner2Id, prLabelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(prLabelId, "ContosoLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/contoso/Azure.Contoso.WidgetManager", repo, CancellationToken.None);

        Assert.That(result.AllPassed, Is.True);
        Assert.That(result.PathFallbackCheck!.PrLabelOwnerCheck!.MatchedPath, Is.EqualTo("/sdk/contoso/"));
    }

    // Test 19: Fallback — multiple matching PR Label owners, uses last by path
    [Test]
    public async Task CheckPackageOwners_Fallback_MultipleMatchingPrLabelOwners_UsesLastByPath()
    {
        const int pkgId = 100;
        const int prLo1Id = 500, prLo2Id = 501;
        const int prOwner1Id = 601, prOwner2Id = 602, prLabel1Id = 701, prLabel2Id = 702;
        const int soId = 800, soOwner1Id = 901, soOwner2Id = 902;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Two PR Label Label Owners, both matching. /sdk/test/ (broader) and /sdk/test/sub/ (more specific)
        var prLo1 = MakeLabelOwnerWorkItem(prLo1Id, "PR Label", repo, "/sdk/test/", prOwner1Id, prLabel1Id);
        var prLo2 = MakeLabelOwnerWorkItem(prLo2Id, "PR Label", repo, "/sdk/test/sub/", prOwner1Id, prOwner2Id, prLabel2Id);
        SetupLabelOwnerQuery("PR Label", repo, [prLo1, prLo2]);

        // Hydrate the selected (last by path = /sdk/test/sub/) PR Label Label Owner
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwner1Id) && ids.Contains(prOwner2Id) && ids.Contains(prLabel2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwner1Id, "prUser1"),
                MakeOwnerWorkItem(prOwner2Id, "prUser2"),
                MakeLabelWorkItem(prLabel2Id, "SpecificLabel")
            ]);

        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwner1Id, soOwner2Id, prLabel2Id);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id) && ids.Contains(prLabel2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(prLabel2Id, "SpecificLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/sub/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.AllPassed, Is.True);
        Assert.That(result.PathFallbackCheck!.PrLabelOwnerCheck!.MatchedPath, Is.EqualTo("/sdk/test/sub/"));
        Assert.That(result.PathFallbackCheck.PrLabelOwnerCheck.Labels, Does.Contain("SpecificLabel"));
    }

    // Test 20: Fallback — multiple labels, Service Owner must be superset
    [Test]
    public async Task CheckPackageOwners_Fallback_MultipleLabels_ServiceOwnerSupersetRequired()
    {
        const int pkgId = 100;
        const int prLabelLoId = 500, prOwner1Id = 601, prOwner2Id = 602, prLabel1Id = 701, prLabel2Id = 702;
        const int soId = 800, soOwner1Id = 901, soOwner2Id = 902;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/test/", prOwner1Id, prOwner2Id, prLabel1Id, prLabel2Id);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwner1Id) && ids.Contains(prOwner2Id) && ids.Contains(prLabel1Id) && ids.Contains(prLabel2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwner1Id, "prUser1"),
                MakeOwnerWorkItem(prOwner2Id, "prUser2"),
                MakeLabelWorkItem(prLabel1Id, "LabelA"),
                MakeLabelWorkItem(prLabel2Id, "LabelB")
            ]);

        // Service Owner has both labels
        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwner1Id, soOwner2Id, prLabel1Id, prLabel2Id);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id) && ids.Contains(prLabel1Id) && ids.Contains(prLabel2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(prLabel1Id, "LabelA"),
                MakeLabelWorkItem(prLabel2Id, "LabelB")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.AllPassed, Is.True);
    }

    // Test 21: Fallback — fragmented service owners fail
    [Test]
    public async Task CheckPackageOwners_Fallback_FragmentedServiceOwners_Fails()
    {
        const int pkgId = 100;
        const int prLabelLoId = 500, prOwner1Id = 601, prOwner2Id = 602, prLabel1Id = 701, prLabel2Id = 702;
        const int so1Id = 800, so2Id = 801, soOwner1Id = 901, soOwner2Id = 902;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/test/", prOwner1Id, prOwner2Id, prLabel1Id, prLabel2Id);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwner1Id) && ids.Contains(prOwner2Id) && ids.Contains(prLabel1Id) && ids.Contains(prLabel2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwner1Id, "prUser1"),
                MakeOwnerWorkItem(prOwner2Id, "prUser2"),
                MakeLabelWorkItem(prLabel1Id, "LabelA"),
                MakeLabelWorkItem(prLabel2Id, "LabelB")
            ]);

        // Fragmented: SO 1 has LabelA only, SO 2 has LabelB only
        var so1Wi = MakeLabelOwnerWorkItem(so1Id, "Service Owner", repo, "", soOwner1Id, soOwner2Id, prLabel1Id);
        var so2Wi = MakeLabelOwnerWorkItem(so2Id, "Service Owner", repo, "", soOwner1Id, soOwner2Id, prLabel2Id);
        SetupLabelOwnerQuery("Service Owner", repo, [so1Wi, so2Wi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(prLabel1Id, "LabelA"),
                MakeLabelWorkItem(prLabel2Id, "LabelB")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.PathFallbackCheck!.ServiceOwnerCheck!.Passed, Is.False);
        Assert.That(result.AllPassed, Is.False);
    }

    // Test 22: Fallback — same people as PR Label and Service Owners
    [Test]
    public async Task CheckPackageOwners_Fallback_SameOwnersForBothTypes()
    {
        const int pkgId = 100;
        const int prLabelLoId = 500, owner1Id = 601, owner2Id = 602, prLabelId = 701;
        const int soId = 800;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/test/", owner1Id, owner2Id, prLabelId);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(owner1Id) && ids.Contains(owner2Id) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(owner1Id, "sharedUser1"),
                MakeOwnerWorkItem(owner2Id, "sharedUser2"),
                MakeLabelWorkItem(prLabelId, "SharedLabel")
            ]);

        // Service Owner has the same owners
        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", owner1Id, owner2Id, prLabelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.AllPassed, Is.True);
    }

    // Test 23: Fallback — Service Owner is pathless but still valid
    [Test]
    public async Task CheckPackageOwners_Fallback_ServiceOwnerPathless()
    {
        const int pkgId = 100;
        const int prLabelLoId = 500, prOwner1Id = 601, prOwner2Id = 602, prLabelId = 701;
        const int soId = 800, soOwner1Id = 901, soOwner2Id = 902;
        const string repo = "Azure/azure-sdk-for-net";

        SetupPackageQuery("Azure.Test.Pkg", [MakePackageWorkItem(pkgId, "Azure.Test.Pkg", relatedIds: [])]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => !ids.Any()),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var prLabelLo = MakeLabelOwnerWorkItem(prLabelLoId, "PR Label", repo, "/sdk/test/", prOwner1Id, prOwner2Id, prLabelId);
        SetupLabelOwnerQuery("PR Label", repo, [prLabelLo]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(prOwner1Id) && ids.Contains(prOwner2Id) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(prOwner1Id, "prUser1"),
                MakeOwnerWorkItem(prOwner2Id, "prUser2"),
                MakeLabelWorkItem(prLabelId, "TestLabel")
            ]);

        // Service Owner with no path (pathless) — still matches on labels
        var soWi = MakeLabelOwnerWorkItem(soId, "Service Owner", repo, "", soOwner1Id, soOwner2Id, prLabelId);
        SetupLabelOwnerQuery("Service Owner", repo, [soWi]);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(soOwner1Id) && ids.Contains(soOwner2Id) && ids.Contains(prLabelId)),
                It.IsAny<int>(),
                WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeOwnerWorkItem(soOwner1Id, "soUser1"),
                MakeOwnerWorkItem(soOwner2Id, "soUser2"),
                MakeLabelWorkItem(prLabelId, "TestLabel")
            ]);

        var result = await _helper.CheckPackageOwners("Azure.Test.Pkg", "sdk/test/Azure.Test.Pkg", repo, CancellationToken.None);

        Assert.That(result.AllPassed, Is.True);
        Assert.That(result.PathFallbackCheck!.ServiceOwnerCheck!.Passed, Is.True);
    }

    // NormalizePath static helper tests
    [TestCase("sdk/test", "/sdk/test")]
    [TestCase("/sdk/test", "/sdk/test")]
    [TestCase("sdk\\test", "/sdk/test")]
    [TestCase("", "")]
    public void NormalizePath_ReturnsExpected(string input, string expected)
    {
        Assert.That(CodeownersManagementHelper.NormalizePath(input), Is.EqualTo(expected));
    }
}
