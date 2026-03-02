// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class CodeownersManagementHelperTests
{
    private Mock<IDevOpsService> _mockDevOps;
    private Mock<ITeamUserCache> _mockTeamUserCache;
    private CodeownersManagementHelper _helper;

    [SetUp]
    public void Setup()
    {
        _mockDevOps = new Mock<IDevOpsService>();
        _mockTeamUserCache = new Mock<ITeamUserCache>();
        _mockTeamUserCache.Setup(c => c.GetUsersForTeam(It.IsAny<string>())).Returns(new List<string>());
        _helper = new CodeownersManagementHelper(
            _mockDevOps.Object,
            _mockTeamUserCache.Object
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
            { "Custom.RepoPath", repoPath }
        }, relatedIds);

    // ========================
    // Assertion helpers
    // ========================

    private static void AssertPackage(PackageWorkItem pkg, string expectedName, string[]? expectedOwners = null, string[]? expectedLabels = null)
    {
        Assert.That(pkg.PackageName, Is.EqualTo(expectedName));
        if (expectedOwners != null)
        {
            Assert.That(pkg.Owners.Select(o => o.GitHubAlias), Is.EquivalentTo(expectedOwners));
        }
        if (expectedLabels != null)
        {
            Assert.That(pkg.Labels.Select(l => l.LabelName), Is.EquivalentTo(expectedLabels));
        }
    }

    private static void AssertLabelOwner(LabelOwnerWorkItem lo, string expectedRepo, string expectedPath, string[]? expectedOwners = null, string[]? expectedLabels = null)
    {
        Assert.That(lo.Repository, Is.EqualTo(expectedRepo));
        Assert.That(lo.RepoPath, Is.EqualTo(expectedPath));
        if (expectedOwners != null)
        {
            Assert.That(lo.Owners.Select(o => o.GitHubAlias), Is.EquivalentTo(expectedOwners));
        }
        if (expectedLabels != null)
        {
            Assert.That(lo.Labels.Select(l => l.LabelName), Is.EquivalentTo(expectedLabels));
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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByUser("nonexistent-owner", null);

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        // GetWorkItemsByIdsAsync for related IDs of the owner — returns package + label owner
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", language: ".NET", relatedIds: [ownerId, labelId]);
        var loWi = MakeLabelOwnerWorkItem(loId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage", ownerId, labelId);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(pkgId) && ids.Contains(loId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { pkgWi, loWi });

        // Hydration: fetch owners and labels for the package's and label owner's related IDs
        var ownerForHydration = MakeOwnerWorkItem(ownerId, "owner1");
        var labelForHydration = MakeLabelWorkItem(labelId, "Storage");

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem> { ownerForHydration, labelForHydration });

        var result = await _helper.GetViewByUser("owner1", null);

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        var netPkg = MakePackageWorkItem(netPkgId, "Azure.Storage.Blobs", language: ".NET", relatedIds: [ownerId]);
        var pyPkg = MakePackageWorkItem(pyPkgId, "azure-storage-blob", language: "Python", relatedIds: [ownerId]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(netPkgId) && ids.Contains(pyPkgId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { netPkg, pyPkg });

        // Hydration for the one .NET package
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem> { MakeOwnerWorkItem(ownerId, "owner1") });

        var result = await _helper.GetViewByUser("owner1", "azure-sdk-for-python");

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        // Empty response for no IDs
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByUser("owner1", null);

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByLabel(["NonExistent"], null);

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { labelWi });

        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [ownerId, labelId]);

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(pkgId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner2"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        var result = await _helper.GetViewByLabel(["Storage"], null);

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { labelAWi });
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label'") && q.Contains("Custom.Label") && q.Contains("Blobs")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>()))
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
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { pkgWi, loWi });

        // Hydration: fetch owners and labels for the package's and label owner's related IDs
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelAId)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner1"),
                MakeLabelWorkItem(labelAId, "Storage")
            });

        var result = await _helper.GetViewByLabel(["Storage", "Blobs"], null);

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { labelWi });
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label'") && q.Contains("Custom.Label") && q.Contains("Missing")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByLabel(["Storage", "Missing"], null);

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
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner2"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        var result = await _helper.GetViewByPath("/sdk/storage", null);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Is.Null); // GetViewByPath returns no packages
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        AssertLabelOwner(result.PathBasedLabelOwners![0], "Azure/azure-sdk-for-net", "/sdk/storage", ["owner2"], ["Storage"]);
    }

    [Test]
    public async Task GetViewByPath_WithRepoFilter_IncludesRepoInQuery()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && q.Contains("Azure/azure-sdk-for-net")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPath("/sdk/storage", "Azure/azure-sdk-for-net");

        Assert.That(result.ResponseError, Is.Null);
        _mockDevOps.Verify(d => d.FetchWorkItemsPagedAsync(
            It.Is<string>(q => q.Contains("Custom.Repository") && q.Contains("Azure/azure-sdk-for-net")),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()), Times.Once);
    }

    [Test]
    public async Task GetViewByPath_NoMatches_ReturnsEmptyResult()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPath("/nonexistent/path", null);

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPackage("NoSuch.Package");

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        // Hydration for package's related IDs
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "owner2"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        // FetchRelatedLabelOwners from the package's related IDs
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>()); // No label owners in this set

        var result = await _helper.GetViewByPackage("Azure.Storage.Blobs");

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgV1Wi, pkgV2Wi });

        // Hydration
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>());
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPackage("Azure.Storage.Blobs");

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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { netPkgWi });

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")
                    && q.Contains("Custom.Package")
                    && q.Contains("Azure.Storage.Blobs")
                    && !q.Contains("Custom.Language")),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<WorkItemExpand>()))
            .ThrowsAsync(new InvalidOperationException("Unfiltered package query should not be called when repo filter is provided."));

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(owner1Id) && ids.Contains(owner2Id)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(owner1Id, "owner1"),
                MakeOwnerWorkItem(owner2Id, "owner2")
            });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(owner1Id) && ids.Contains(owner2Id)),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPackage("Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

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
            It.IsAny<WorkItemExpand>()), Times.Once);
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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        var netLo = MakeLabelOwnerWorkItem(netLoId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage");
        var pyLo = MakeLabelOwnerWorkItem(pyLoId, "Service Owner", "Azure/azure-sdk-for-python", "/sdk/storage");

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(netLoId) && ids.Contains(pyLoId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { netLo, pyLo });

        var result = await _helper.GetViewByUser("owner1", "Azure/azure-sdk-for-net");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        Assert.That(result.PathBasedLabelOwners![0].Repository, Is.EqualTo("Azure/azure-sdk-for-net"));
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
                It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        // Hydration: owner is a team
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(teamOwnerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(teamOwnerId, "azure/azure-sdk-team"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(teamOwnerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.GetViewByPackage("Azure.Storage.Blobs");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Packages, Has.Count.EqualTo(1));

        var teamOwner = result.Packages![0].Owners.First(o => o.GitHubAlias == "azure/azure-sdk-team");
        Assert.That(teamOwner.ExpandedMembers, Is.EquivalentTo(new[] { "owner1", "owner2", "owner3" }));
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
                WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(teamOwnerId) && ids.Contains(labelId)),
                It.IsAny<int>(),
                WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(teamOwnerId, "azure/sdk-storage-team"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        var result = await _helper.GetViewByPath("/sdk/storage", null);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        var teamOwner = result.PathBasedLabelOwners![0].Owners.First(o => o.GitHubAlias == "azure/sdk-storage-team");
        Assert.That(teamOwner.ExpandedMembers, Is.EquivalentTo(new[] { "owner4", "owner5" }));
    }
}
