// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
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
    private Mock<ICodeownersValidatorHelper> _mockValidator;
    private CodeownersManagementHelper _helper;

    [SetUp]
    public void Setup()
    {
        _mockDevOps = new Mock<IDevOpsService>();
        _mockTeamUserCache = new Mock<ITeamUserCache>();
        _mockTeamUserCache.Setup(c => c.GetUsersForTeam(It.IsAny<string>())).Returns(new List<string>());
        _mockValidator = new Mock<ICodeownersValidatorHelper>();
        _helper = new CodeownersManagementHelper(
            _mockDevOps.Object,
            _mockTeamUserCache.Object,
            _mockValidator.Object
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

    private static void AssertLabelOwner(LabelOwnerResponse lo, string expectedRepo, string expectedPath, string[]? expectedOwners = null, string[]? expectedLabels = null)
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
        var teamOwner = result.PathBasedLabelOwners![0].Owners!.First(o => o.GitHubAlias == "azure/sdk-storage-team");
        Assert.That(teamOwner.Members, Is.EquivalentTo(new[] { "owner4", "owner5" }));
    }

    // ========================
    // MapOwnerType tests
    // ========================

    [TestCase("service-owner", "Service Owner")]
    [TestCase("azsdk-owner", "Azure SDK Owner")]
    [TestCase("pr-label", "PR Label")]
    [TestCase("SERVICE-OWNER", "Service Owner")]
    public void MapOwnerType_ReturnsExpected(string input, string expected)
    {
        Assert.That(CodeownersManagementHelper.MapOwnerType(input), Is.EqualTo(expected));
    }

    [Test]
    public void MapOwnerType_InvalidType_Throws()
    {
        Assert.Throws<ArgumentException>(() => CodeownersManagementHelper.MapOwnerType("unknown-type"));
    }

    // ========================
    // FindOrCreateOwner tests
    // ========================

    [Test]
    public async Task FindOrCreateOwner_ExistingOwner_ReturnsWithoutCreating()
    {
        var ownerWi = MakeOwnerWorkItem(1, "existinguser");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Owner'") && q.Contains("existinguser")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { ownerWi });

        var result = await _helper.FindOrCreateOwner("@existinguser");

        Assert.That(result.GitHubAlias, Is.EqualTo("existinguser"));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [Test]
    public async Task FindOrCreateOwner_NewOwner_ValidatesAndCreates()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Owner'") && q.Contains("newuser")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        _mockValidator.Setup(v => v.ValidateCodeOwnerAsync("newuser", It.IsAny<bool>()))
            .ReturnsAsync(new CodeownersValidationResult { Username = "newuser", IsValidCodeOwner = true });

        var createdWi = MakeOwnerWorkItem(99, "newuser");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Owner", "newuser", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(createdWi);

        var result = await _helper.FindOrCreateOwner("newuser");

        Assert.That(result.GitHubAlias, Is.EqualTo("newuser"));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), "Owner", "newuser", It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    [Test]
    public async Task FindOrCreateOwner_InvalidUser_Throws()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Owner'") && q.Contains("baduser")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        _mockValidator.Setup(v => v.ValidateCodeOwnerAsync("baduser", It.IsAny<bool>()))
            .ReturnsAsync(new CodeownersValidationResult { Username = "baduser", IsValidCodeOwner = false, Message = "Not a valid code owner" });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => _helper.FindOrCreateOwner("baduser"));
        Assert.That(ex!.Message, Does.Contain("not a valid Azure SDK code owner"));
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
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("Azure/azure-sdk-for-net") && q.Contains("Service Owner")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { labelWiRaw });

        var result = await _helper.FindOrCreateLabelOwnerAsync("Azure/azure-sdk-for-net", "service-owner", null, [labelWi]);

        Assert.That(result.WorkItemId, Is.EqualTo(55));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
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
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("Azure/azure-sdk-for-net") && q.Contains("Service Owner")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { loWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(otherLabelId)),
                It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { otherLabelWiRaw });

        var createdLoWi = MakeLabelOwnerWorkItem(99, "Service Owner", "Azure/azure-sdk-for-net", "");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: Storage", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(createdLoWi);

        var result = await _helper.FindOrCreateLabelOwnerAsync("Azure/azure-sdk-for-net", "service-owner", null, [expectedLabelWi]);

        Assert.That(result.WorkItemId, Is.EqualTo(99));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: Storage", It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    [Test]
    public async Task FindOrCreateLabelOwner_NotFound_Creates()
    {
        var labelWi = new LabelWorkItem { WorkItemId = 100, LabelName = "Storage" };

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("Azure/azure-sdk-for-net")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var createdLoWi = MakeLabelOwnerWorkItem(77, "Service Owner", "Azure/azure-sdk-for-net", "");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: Storage", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(createdLoWi);

        var result = await _helper.FindOrCreateLabelOwnerAsync("Azure/azure-sdk-for-net", "service-owner", null, [labelWi]);

        Assert.That(result.WorkItemId, Is.EqualTo(77));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: Storage", It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    [Test]
    public async Task FindOrCreateLabelOwner_WithPath_UsesPathInTitle()
    {
        var labelWi = new LabelWorkItem { WorkItemId = 100, LabelName = "Storage" };

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("sdk/service/")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var createdLoWi = MakeLabelOwnerWorkItem(88, "Service Owner", "Azure/azure-sdk-for-net", "sdk/service/");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", "Service Owner: sdk/service/", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(createdLoWi);

        var result = await _helper.FindOrCreateLabelOwnerAsync("Azure/azure-sdk-for-net", "service-owner", "sdk/service/", [labelWi]);

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
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var ownerWi = new OwnerWorkItem { GitHubAlias = "user1" };
        var result = await _helper.AddOwnersToPackage([ownerWi], "NoSuchPackage", "Azure/azure-sdk-for-net");

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
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { ownerRawWi });

        var result = await _helper.AddOwnersToPackage([new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

        Assert.That(result.Operation, Does.Contain("Skipped adding @user1"));
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(It.IsAny<int>(), It.Is<string>(s => s == "related"), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task AddOwnerToPackage_NewLink_CreatesLink()
    {
        const int pkgId = 1, ownerId = 10;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'") && q.Contains("Azure.Storage.Blobs")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(pkgId, "related", ownerId, It.IsAny<string?>())).ReturnsAsync(new WorkItem());

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.AddOwnersToPackage([new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Added @user1"));
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(pkgId, "related", ownerId, It.IsAny<string?>()), Times.Once);
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
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.AddLabelsToPackage([new LabelWorkItem { WorkItemId = labelId, LabelName = "StorageLabel" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

        Assert.That(result.Operation, Does.Contain("Skipped adding label 'StorageLabel'"));
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(It.IsAny<int>(), It.Is<string>(s => s == "related"), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task AddLabelsToPackage_NewLink_CreatesLink()
    {
        const int pkgId = 1, labelId = 20;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(pkgId, "related", labelId, It.IsAny<string?>())).ReturnsAsync(new WorkItem());
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.AddLabelsToPackage([new LabelWorkItem { WorkItemId = labelId, LabelName = "StorageLabel" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("StorageLabel"));
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(pkgId, "related", labelId, It.IsAny<string?>()), Times.Once);
    }

    // ========================
    // RemoveOwnersFromPackage tests
    // ========================

    [Test]
    public async Task RemoveOwnerFromPackage_PackageNotFound_ReturnsError()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.RemoveOwnersFromPackage([new OwnerWorkItem { GitHubAlias = "user1" }], "NoSuchPackage", "Azure/azure-sdk-for-net");

        Assert.That(result.ResponseError, Does.Contain("No Package work item found"));
    }

    [Test]
    public async Task RemoveOwnerFromPackage_NotLinked_ReturnsSkipMessage()
    {
        const int pkgId = 1, ownerId = 10;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs"); // no relation to owner

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.RemoveOwnersFromPackage([new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

        Assert.That(result.Operation, Does.Contain("Skipped removing @user1"));
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(It.IsAny<int>(), It.Is<string>(s => s == "related"), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task RemoveOwnerFromPackage_Linked_RemovesLink()
    {
        const int pkgId = 1, ownerId = 10;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [ownerId]);
        var ownerRawWi = MakeOwnerWorkItem(ownerId, "user1");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(pkgId, "related", ownerId)).Returns(Task.CompletedTask);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { ownerRawWi });

        var result = await _helper.RemoveOwnersFromPackage([new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Removed @user1"));
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(pkgId, "related", ownerId), Times.Once);
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
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem>());

        var result = await _helper.RemoveLabelsFromPackage([new LabelWorkItem { WorkItemId = labelId, LabelName = "StorageLabel" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

        Assert.That(result.Operation, Does.Contain("Skipped removing label 'StorageLabel'"));
    }

    [Test]
    public async Task RemoveLabelsFromPackage_Linked_RemovesLink()
    {
        const int pkgId = 1, labelId = 20;
        var pkgWi = MakePackageWorkItem(pkgId, "Azure.Storage.Blobs", relatedIds: [labelId]);
        var labelRawWi = MakeLabelWorkItem(labelId, "StorageLabel");

        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Package'")),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { pkgWi });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(pkgId, "related", labelId)).Returns(Task.CompletedTask);
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>(), It.IsAny<WorkItemExpand>()))
            .ReturnsAsync(new List<WorkItem> { labelRawWi });

        var result = await _helper.RemoveLabelsFromPackage([new LabelWorkItem { WorkItemId = labelId, LabelName = "StorageLabel" }], "Azure.Storage.Blobs", "Azure/azure-sdk-for-net");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Removed label 'StorageLabel'"));
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(pkgId, "related", labelId), Times.Once);
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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration of the label owner candidate — returns the label work item so SetEquals matches
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(labelId, "Storage") });

        // Label is already linked to the label owner, so CreateWorkItemRelationAsync for label should NOT be called
        // Owner is NOT related, so CreateWorkItemRelationAsync for owner should be called
        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", ownerId, It.IsAny<string?>())).ReturnsAsync(new WorkItem());

        // GetViewByPath at the end — return empty for simplicity
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Added @user1"));
        Assert.That(result.Operation, Does.Contain("/sdk/storage"));
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", ownerId, It.IsAny<string?>()), Times.Once);
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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration — label matches so FindOrCreateLabelOwnerAsync returns existing
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(labelId, "Storage") });

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Skipped adding @user1"));
        Assert.That(result.Operation, Does.Contain("already linked"));
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", ownerId, It.IsAny<string?>()), Times.Never);
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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(labelId, "Storage") });

        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", owner2Id, It.IsAny<string?>())).ReturnsAsync(new WorkItem());

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[]
        {
            new OwnerWorkItem { WorkItemId = owner1Id, GitHubAlias = "existingUser" },
            new OwnerWorkItem { WorkItemId = owner2Id, GitHubAlias = "newUser" }
        };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Skipped adding @existingUser"));
        Assert.That(result.Operation, Does.Contain("Added @newUser"));
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", owner1Id, It.IsAny<string?>()), Times.Never);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(labelOwnerWiId, "related", owner2Id, It.IsAny<string?>()), Times.Once);
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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        // CreateWorkItemAsync returns a new Label Owner work item
        var createdWi = MakeLabelOwnerWorkItem(newLabelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/newpath");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", It.Is<string>(t => t.Contains("/sdk/newpath")), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(createdWi);

        // Link label to the new label owner
        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", labelId, It.IsAny<string?>())).ReturnsAsync(new WorkItem());
        // Link owner to the new label owner
        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", ownerId, It.IsAny<string?>())).ReturnsAsync(new WorkItem());

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/newpath") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/newpath", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Added @user1"));
        _mockDevOps.Verify(d => d.CreateWorkItemAsync(It.IsAny<WorkItemBase>(), "Label Owner", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", labelId, It.IsAny<string?>()), Times.Once);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", ownerId, It.IsAny<string?>()), Times.Once);
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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration returns only label1
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(label1Id)),
                It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(label1Id, "Storage") });

        // No exact label-set match, so a new Label Owner is created
        const int newLabelOwnerWiId = 200;
        var createdWi = MakeLabelOwnerWorkItem(newLabelOwnerWiId, "Service Owner", "Azure/azure-sdk-for-net", "/sdk/storage");
        _mockDevOps.Setup(d => d.CreateWorkItemAsync(
                It.IsAny<WorkItemBase>(), "Label Owner", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(createdWi);

        // Link both labels and owner to the new label owner
        _mockDevOps.Setup(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", It.IsAny<int?>(), It.IsAny<string?>())).ReturnsAsync(new WorkItem());

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[]
        {
            new LabelWorkItem { WorkItemId = label1Id, LabelName = "Storage" },
            new LabelWorkItem { WorkItemId = label2Id, LabelName = "Blobs" }
        };

        var result = await _helper.AddOwnersAndLabelsToPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Added @user1"));
        // Both labels should be linked to the new label owner
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", label1Id, It.IsAny<string?>()), Times.Once);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", label2Id, It.IsAny<string?>()), Times.Once);
        _mockDevOps.Verify(d => d.CreateWorkItemRelationAsync(newLabelOwnerWiId, "related", ownerId, It.IsAny<string?>()), Times.Once);
    }

    // ========================
    // RemoveOwnersFromLabelsAndPath tests
    // ========================

    [Test]
    public async Task RemoveOwnersFromLabelsAndPath_NoLabelOwnerFound_ReturnsError()
    {
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("'Label Owner'") && q.Contains("/sdk/nopath")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = 20, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/nopath", "service-owner");

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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration of the label owner — returns owner and label work items
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(ownerId) && ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "user1"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", ownerId)).Returns(Task.CompletedTask);

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", ownerId), Times.Once);
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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration — only label, no owner
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem> { MakeLabelWorkItem(labelId, "Storage") });

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Skipped removing @user1"));
        Assert.That(result.Operation, Does.Contain("not linked"));
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(It.IsAny<int>(), "related", It.IsAny<int>()), Times.Never);
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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { labelOwnerRawWi });

        // Hydration
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(owner1Id) && ids.Contains(labelId)),
                It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(owner1Id, "linkedUser"),
                MakeLabelWorkItem(labelId, "Storage")
            });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", owner1Id)).Returns(Task.CompletedTask);

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        var owners = new[]
        {
            new OwnerWorkItem { WorkItemId = owner1Id, GitHubAlias = "linkedUser" },
            new OwnerWorkItem { WorkItemId = owner2Id, GitHubAlias = "notLinkedUser" }
        };
        var labels = new[] { new LabelWorkItem { WorkItemId = labelId, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Operation, Does.Contain("Skipped removing @notLinkedUser"));
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", owner1Id), Times.Once);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwnerWiId, "related", owner2Id), Times.Never);
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
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem> { lo1RawWi, lo2RawWi });

        // Hydration — fetch all related IDs from both label owners
        _mockDevOps.Setup(d => d.GetWorkItemsByIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<int>(), WorkItemExpand.All))
            .ReturnsAsync(new List<WorkItem>
            {
                MakeOwnerWorkItem(ownerId, "user1"),
                MakeLabelWorkItem(label1Id, "Storage"),
                MakeLabelWorkItem(label2Id, "Blobs")
            });

        _mockDevOps.Setup(d => d.RemoveWorkItemRelationAsync(labelOwner1Id, "related", ownerId)).Returns(Task.CompletedTask);

        // GetViewByPath at the end
        _mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                It.Is<string>(q => q.Contains("/sdk/storage") && !q.Contains("'Label Owner'")),
                It.IsAny<int>(), It.IsAny<int>(), WorkItemExpand.Relations))
            .ReturnsAsync(new List<WorkItem>());

        // Request removal from the label owner that has label1 ("Storage")
        var owners = new[] { new OwnerWorkItem { WorkItemId = ownerId, GitHubAlias = "user1" } };
        var labels = new[] { new LabelWorkItem { WorkItemId = label1Id, LabelName = "Storage" } };

        var result = await _helper.RemoveOwnersFromLabelsAndPath(owners, labels, "Azure/azure-sdk-for-net", "/sdk/storage", "service-owner");

        Assert.That(result.ResponseError, Is.Null);
        // Should remove from labelOwner1 (which has label1), not labelOwner2
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwner1Id, "related", ownerId), Times.Once);
        _mockDevOps.Verify(d => d.RemoveWorkItemRelationAsync(labelOwner2Id, "related", It.IsAny<int>()), Times.Never);
    }
}
