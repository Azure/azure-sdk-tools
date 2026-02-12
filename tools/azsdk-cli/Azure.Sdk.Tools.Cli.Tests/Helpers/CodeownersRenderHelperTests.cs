// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class CodeownersRenderHelperTests
{
    private TempDirectory _tempDir = null!;
    private ILogger<CodeownersRenderHelper> _logger = null!;
    private Mock<IDevOpsService> _mockDevOpsService = null!;
    private Mock<IPowershellHelper> _mockPowershellHelper = null!;
    private Mock<IInputSanitizer> _mockInputSanitizer = null!;
    private string _repoRoot = null!;
    private string _codeownersPath = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = TempDirectory.Create("CodeownersRenderHelperTests");
        _repoRoot = _tempDir.DirectoryPath;
        _logger = new TestLogger<CodeownersRenderHelper>();
        _mockDevOpsService = new Mock<IDevOpsService>();
        _mockPowershellHelper = new Mock<IPowershellHelper>();
        _mockInputSanitizer = new Mock<IInputSanitizer>();

        // Create .github directory and CODEOWNERS file
        var githubDir = Path.Combine(_repoRoot, ".github");
        Directory.CreateDirectory(githubDir);
        _codeownersPath = Path.Combine(githubDir, "CODEOWNERS");
    }

    [TearDown]
    public void TearDown()
    {
        _tempDir.Dispose();
    }

    #region Helper Methods

    private static WorkItem CreatePackageWorkItem(int id, string packageName, params int[] relatedIds)
    {
        return CreatePackageWorkItem(id, packageName, "", relatedIds);
    }

    private static WorkItem CreatePackageWorkItem(int id, string packageName, string versionMajorMinor, params int[] relatedIds)
    {
        var wi = new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.Package", packageName },
                { "Custom.PackageVersionMajorMinor", versionMajorMinor }
            },
            Relations = relatedIds.Select(relId => new WorkItemRelation
            {
                Rel = "System.LinkTypes.Related",
                Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{relId}"
            }).ToList()
        };
        return wi;
    }

    private static WorkItem CreateOwnerWorkItem(int id, string gitHubAlias)
    {
        return new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.GitHubAlias", gitHubAlias }
            }
        };
    }

    private static WorkItem CreateLabelWorkItem(int id, string labelName)
    {
        return new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.Label", labelName }
            }
        };
    }

    private static WorkItem CreateLabelOwnerWorkItem(int id, string labelType, string repository, string repoPath, params int[] relatedIds)
    {
        var wi = new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.LabelType", labelType },
                { "Custom.Repository", repository },
                { "Custom.RepoPath", repoPath }
            },
            Relations = relatedIds.Select(relId => new WorkItemRelation
            {
                Rel = "System.LinkTypes.Related",
                Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{relId}"
            }).ToList()
        };
        return wi;
    }

    private static PackageWorkItem MapToPackageWorkItem(WorkItem wi)
    {
        return new PackageWorkItem
        {
            WorkItemId = wi.Id!.Value,
            PackageName = GetFieldValue(wi, "Custom.Package"),
            PackageVersionMajorMinor = GetFieldValue(wi, "Custom.PackageVersionMajorMinor"),
            RelatedIds = WorkItemData.ExtractRelatedIds(wi)
        };
    }

    private static OwnerWorkItem MapToOwnerWorkItem(WorkItem wi)
    {
        return new OwnerWorkItem
        {
            WorkItemId = wi.Id!.Value,
            GitHubAlias = GetFieldValue(wi, "Custom.GitHubAlias")
        };
    }

    private static LabelWorkItem MapToLabelWorkItem(WorkItem wi)
    {
        return new LabelWorkItem
        {
            WorkItemId = wi.Id!.Value,
            LabelName = GetFieldValue(wi, "Custom.Label")
        };
    }

    private static LabelOwnerWorkItem MapToLabelOwnerWorkItem(WorkItem wi)
    {
        return new LabelOwnerWorkItem
        {
            WorkItemId = wi.Id!.Value,
            LabelType = GetFieldValue(wi, "Custom.LabelType"),
            Repository = GetFieldValue(wi, "Custom.Repository"),
            RepoPath = GetFieldValue(wi, "Custom.RepoPath"),
            RelatedIds = WorkItemData.ExtractRelatedIds(wi)
        };
    }

    private static string GetFieldValue(WorkItem wi, string fieldName)
    {
        return wi.Fields.TryGetValue(fieldName, out var value) ? value?.ToString() ?? "" : "";
    }

    private void CreateCodeownersFile(string content)
    {
        File.WriteAllText(_codeownersPath, content);
    }

    private string GetCodeownersTemplate()
    {
        return """
            # Repository-level code owners
            *    @repo-maintainer

            ####################
            # Client Libraries
            ####################

            # Placeholder content that will be replaced

            ####################
            # Other Section
            ####################

            /other/    @other-owner
            """;
    }

    #endregion

    #region GetLanguageFromRepoName Tests

    [TestCase("Azure/azure-sdk-for-net", "net", ".NET")]
    [TestCase("Azure/azure-sdk-for-python", "python", "Python")]
    [TestCase("Azure/azure-sdk-for-java", "java", "Java")]
    [TestCase("Azure/azure-sdk-for-js", "js", "JavaScript")]
    [TestCase("Azure/azure-sdk-for-go", "go", "Go")]
    [TestCase("Azure/azure-sdk-for-cpp", "cpp", "C++")]
    [TestCase("azure-sdk-for-net", "net", ".NET")]
    public void GetLanguageFromRepoName_ReturnsExpectedLanguage(string repoName, string suffix, string expectedLanguage)
    {
        // Setup mock to return expected language for the suffix
        _mockInputSanitizer.Setup(s => s.SanitizeLanguage(suffix)).Returns(expectedLanguage);

        // Use reflection to test private instance method
        var method = typeof(CodeownersRenderHelper).GetMethod(
            "GetLanguageFromRepoName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var helper = new CodeownersRenderHelper(
            _mockDevOpsService.Object,
            _mockPowershellHelper.Object,
            _mockInputSanitizer.Object,
            _logger);

        var result = method?.Invoke(helper, [repoName]);

        Assert.That(result, Is.EqualTo(expectedLanguage));
    }

    [Test]
    public void GetLanguageFromRepoName_ThrowsForInvalidRepoName()
    {
        var method = typeof(CodeownersRenderHelper).GetMethod(
            "GetLanguageFromRepoName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var helper = new CodeownersRenderHelper(
            _mockDevOpsService.Object,
            _mockPowershellHelper.Object,
            _mockInputSanitizer.Object,
            _logger);

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => method?.Invoke(helper, ["unknown-repo"]));
        Assert.That(ex?.InnerException, Is.TypeOf<ArgumentException>());
    }

    #endregion

    #region BuildPathExpression Tests

    [TestCase(@"C:\repos\sdk\storage\Azure.Storage.Blobs", @"C:\repos", "/sdk/storage/Azure.Storage.Blobs/")]
    [TestCase(@"C:\repos\sdk\core", @"C:\repos", "/sdk/core/")]
    [TestCase("sdk/storage", "", "/sdk/storage/")]
    public void BuildPathExpression_ReturnsExpectedPath(string dirPath, string repoRoot, string expected)
    {
        var method = typeof(CodeownersRenderHelper).GetMethod(
            "BuildPathExpression",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = method?.Invoke(null, [dirPath, repoRoot]);

        Assert.That(result, Is.EqualTo(expected));
    }

    #endregion

    #region Package Model Tests

    [Test]
    public void PackageWorkItem_MapsWorkItemCorrectly()
    {
        var wi = CreatePackageWorkItem(100, "Azure.Storage.Blobs", 200, 300);

        var package = MapToPackageWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(package.WorkItemId, Is.EqualTo(100));
            Assert.That(package.PackageName, Is.EqualTo("Azure.Storage.Blobs"));
            Assert.That(package.RelatedIds, Has.Count.EqualTo(2));
            Assert.That(package.RelatedIds, Does.Contain(200));
            Assert.That(package.RelatedIds, Does.Contain(300));
        });
    }

    [Test]
    public void PackageWorkItem_HandlesEmptyRelations()
    {
        var wi = new WorkItem
        {
            Id = 100,
            Fields = new Dictionary<string, object> { { "Custom.Package", "TestPackage" } },
            Relations = null
        };

        var package = MapToPackageWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(package.WorkItemId, Is.EqualTo(100));
            Assert.That(package.PackageName, Is.EqualTo("TestPackage"));
            Assert.That(package.RelatedIds, Is.Empty);
        });
    }

    #endregion

    #region Owner Model Tests

    [Test]
    public void OwnerWorkItem_MapsWorkItemCorrectly()
    {
        var wi = CreateOwnerWorkItem(200, "johndoe");

        var owner = MapToOwnerWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(owner.WorkItemId, Is.EqualTo(200));
            Assert.That(owner.GitHubAlias, Is.EqualTo("johndoe"));
        });
    }

    [Test]
    public void OwnerWorkItem_HandlesEmptyAlias()
    {
        var wi = new WorkItem
        {
            Id = 200,
            Fields = new Dictionary<string, object>()
        };

        var owner = MapToOwnerWorkItem(wi);

        Assert.That(owner.GitHubAlias, Is.EqualTo(""));
    }

    #endregion

    #region Label Model Tests

    [Test]
    public void LabelWorkItem_MapsWorkItemCorrectly()
    {
        var wi = CreateLabelWorkItem(300, "Storage");

        var label = MapToLabelWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(label.WorkItemId, Is.EqualTo(300));
            Assert.That(label.LabelName, Is.EqualTo("Storage"));
        });
    }

    #endregion

    #region LabelOwner Model Tests

    [Test]
    public void LabelOwnerWorkItem_MapsWorkItemCorrectly()
    {
        var wi = CreateLabelOwnerWorkItem(400, "Service Owner", "Azure/azure-sdk-for-net", "sdk/storage", 200, 300);

        var labelOwner = MapToLabelOwnerWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(labelOwner.WorkItemId, Is.EqualTo(400));
            Assert.That(labelOwner.LabelType, Is.EqualTo("Service Owner"));
            Assert.That(labelOwner.Repository, Is.EqualTo("Azure/azure-sdk-for-net"));
            Assert.That(labelOwner.RepoPath, Is.EqualTo("sdk/storage"));
            Assert.That(labelOwner.RelatedIds, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void LabelOwnerWorkItem_HandlesEmptyRepoPath()
    {
        var wi = CreateLabelOwnerWorkItem(400, "Azure SDK Owner", "Azure/azure-sdk-for-net", "", 200);

        var labelOwner = MapToLabelOwnerWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(labelOwner.RepoPath, Is.EqualTo(""));
            Assert.That(labelOwner.LabelType, Is.EqualTo("Azure SDK Owner"));
        });
    }

    #endregion

    #region WorkItemData Tests

    [Test]
    public void WorkItemData_StoresDataCorrectly()
    {
        var packages = new Dictionary<int, PackageWorkItem>
        {
            { 100, MapToPackageWorkItem(CreatePackageWorkItem(100, "TestPackage")) }
        };
        var owners = new Dictionary<int, OwnerWorkItem>
        {
            { 200, MapToOwnerWorkItem(CreateOwnerWorkItem(200, "johndoe")) }
        };
        var labels = new Dictionary<int, LabelWorkItem>
        {
            { 300, MapToLabelWorkItem(CreateLabelWorkItem(300, "TestLabel")) }
        };
        var labelOwners = new List<LabelOwnerWorkItem>
        {
            MapToLabelOwnerWorkItem(CreateLabelOwnerWorkItem(400, "Service Owner", "repo", "path"))
        };

        var data = new WorkItemData(packages, owners, labels, labelOwners);

        Assert.Multiple(() =>
        {
            Assert.That(data.Packages, Has.Count.EqualTo(1));
            Assert.That(data.Owners, Has.Count.EqualTo(1));
            Assert.That(data.Labels, Has.Count.EqualTo(1));
            Assert.That(data.LabelOwners, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void HydrateRelationships_PopulatesPackageReferences()
    {
        // Arrange
        var data = new WorkItemDataBuilder()
            .AddOwner("alice", out var aliceId)
            .AddOwner("bob", out var bobId)
            .AddLabel("Storage", out var storageId)
            .AddLabel("Core", out var coreId)
            .AddPackage("Azure.Storage.Blobs", out var pkgId, relatedTo: [aliceId, bobId, storageId, coreId])
            .Build();

        // Assert - hydration happened automatically in Build()
        var pkg = data.Packages[pkgId];
        Assert.Multiple(() =>
        {
            Assert.That(pkg.Owners, Has.Count.EqualTo(2));
            Assert.That(pkg.Owners.Select(o => o.GitHubAlias), Is.EquivalentTo(new[] { "alice", "bob" }));
            Assert.That(pkg.Labels, Has.Count.EqualTo(2));
            Assert.That(pkg.Labels.Select(l => l.LabelName), Is.EquivalentTo(new[] { "Storage", "Core" }));
        });
    }

    [Test]
    public void HydrateRelationships_PopulatesLabelOwnerReferences()
    {
        // Arrange
        var data = new WorkItemDataBuilder()
            .AddOwner("serviceowner", out var ownerId)
            .AddLabel("MyService", out var labelId)
            .AddServiceOwner(out var loId, relatedTo: [ownerId, labelId])
            .Build();

        // Assert
        var lo = data.LabelOwners.First(l => l.WorkItemId == loId);
        Assert.Multiple(() =>
        {
            Assert.That(lo.Owners, Has.Count.EqualTo(1));
            Assert.That(lo.Owners[0].GitHubAlias, Is.EqualTo("serviceowner"));
            Assert.That(lo.Labels, Has.Count.EqualTo(1));
            Assert.That(lo.Labels[0].LabelName, Is.EqualTo("MyService"));
        });
    }

    [Test]
    public void HydrateRelationships_LinksLabelOwnersToPackages()
    {
        // Arrange
        var data = new WorkItemDataBuilder()
            .AddOwner("pkgowner", out var pkgOwnerId)
            .AddOwner("svcowner", out var svcOwnerId)
            .AddLabel("Storage", out var labelId)
            .AddServiceOwner(out var loId, relatedTo: [svcOwnerId, labelId])
            .AddPackage("Azure.Storage.Blobs", out var pkgId, relatedTo: [pkgOwnerId, labelId, loId])
            .Build();

        // Assert
        var pkg = data.Packages[pkgId];
        Assert.Multiple(() =>
        {
            Assert.That(pkg.LabelOwners, Has.Count.EqualTo(1));
            Assert.That(pkg.LabelOwners[0].LabelType, Is.EqualTo("Service Owner"));
            Assert.That(pkg.LabelOwners[0].Owners.Select(o => o.GitHubAlias), Does.Contain("svcowner"));
        });
    }

    #endregion

    #region CodeownersSectionFinder Tests

    [Test]
    public void FindClientLibrariesSection_FindsSectionCorrectly()
    {
        var lines = new List<string>
        {
            "# Header",
            "",
            "####################",
            "# Client Libraries",
            "####################",
            "",
            "/sdk/storage/    @owner1",
            "",
            "####################",
            "# Other Section",
            "####################"
        };

        var (headerStart, contentStart, sectionEnd) = CodeownersSectionFinder.FindClientLibrariesSection(lines);

        Assert.Multiple(() =>
        {
            Assert.That(headerStart, Is.EqualTo(2));
            Assert.That(contentStart, Is.EqualTo(5)); // First line after header (blank line)
            Assert.That(sectionEnd, Is.EqualTo(8));
        });
    }

    [Test]
    public void FindClientLibrariesSection_ReturnsMinusOneWhenNotFound()
    {
        var lines = new List<string>
        {
            "# Header",
            "/path/    @owner"
        };

        var (headerStart, contentStart, sectionEnd) = CodeownersSectionFinder.FindClientLibrariesSection(lines);

        Assert.That(contentStart, Is.EqualTo(-1));
    }

    #endregion

    #region CodeownersEntrySorter Tests

    [Test]
    public void SortOwnersInPlace_SortsOwnersAlphabetically()
    {
        var entries = new List<CodeownersEntry>
        {
            new()
            {
                PathExpression = "/sdk/storage/",
                SourceOwners = ["charlie", "alice", "bob"],
                ServiceOwners = ["zoe", "anna"],
                AzureSdkOwners = ["mike", "lisa"]
            }
        };

        CodeownersEntrySorter.SortOwnersInPlace(entries);

        Assert.Multiple(() =>
        {
            Assert.That(entries[0].SourceOwners, Is.EqualTo(new List<string> { "alice", "bob", "charlie" }));
            Assert.That(entries[0].ServiceOwners, Is.EqualTo(new List<string> { "anna", "zoe" }));
            Assert.That(entries[0].AzureSdkOwners, Is.EqualTo(new List<string> { "lisa", "mike" }));
        });
    }

    [Test]
    public void SortLabelsInPlace_SortsLabelsAlphabetically()
    {
        var entries = new List<CodeownersEntry>
        {
            new()
            {
                PathExpression = "/sdk/storage/",
                PRLabels = ["Storage", "Core", "Analytics"],
                ServiceLabels = ["Blob", "Azure"]
            }
        };

        CodeownersEntrySorter.SortLabelsInPlace(entries);

        Assert.Multiple(() =>
        {
            Assert.That(entries[0].PRLabels, Is.EqualTo(new List<string> { "Analytics", "Core", "Storage" }));
            Assert.That(entries[0].ServiceLabels, Is.EqualTo(new List<string> { "Azure", "Blob" }));
        });
    }

    [Test]
    public void SortEntries_SortsPathedEntriesFirst()
    {
        var entries = new List<CodeownersEntry>
        {
            new() { PathExpression = "", ServiceLabels = ["Pathless"] },
            new() { PathExpression = "/sdk/storage/", PRLabels = ["Storage"] },
            new() { PathExpression = "/sdk/core/", PRLabels = ["Core"] }
        };

        var sorted = CodeownersEntrySorter.SortEntries(entries);

        Assert.Multiple(() =>
        {
            Assert.That(sorted[0].PathExpression, Is.EqualTo("/sdk/core/"));
            Assert.That(sorted[1].PathExpression, Is.EqualTo("/sdk/storage/"));
            Assert.That(sorted[^1].PathExpression, Is.EqualTo(""));
        });
    }

    #endregion

    #region FormatCodeownersEntry Tests

    [Test]
    public void FormatCodeownersEntry_FormatsSimpleEntry()
    {
        var entry = new CodeownersEntry
        {
            PathExpression = "/sdk/storage/",
            SourceOwners = ["alice", "bob"]
        };

        var formatted = entry.FormatCodeownersEntry();

        Assert.That(formatted, Is.EqualTo("/sdk/storage/    @alice @bob"));
    }

    [Test]
    public void FormatCodeownersEntry_FormatsEntryWithPRLabels()
    {
        var entry = new CodeownersEntry
        {
            PathExpression = "/sdk/storage/",
            SourceOwners = ["alice"],
            PRLabels = ["Storage", "Core"]
        };

        var formatted = entry.FormatCodeownersEntry();

        // Labels are formatted in list order (sorting happens separately)
        Assert.That(formatted, Does.Contain("# PRLabel: %Storage %Core"));
        Assert.That(formatted, Does.Contain("/sdk/storage/    @alice"));
    }

    [Test]
    public void FormatCodeownersEntry_FormatsEntryWithServiceOwners()
    {
        var entry = new CodeownersEntry
        {
            PathExpression = "",
            ServiceLabels = ["Storage"],
            ServiceOwners = ["serviceowner1", "serviceowner2"]
        };

        var formatted = entry.FormatCodeownersEntry();

        Assert.That(formatted, Does.Contain("# ServiceLabel: %Storage"));
        Assert.That(formatted, Does.Contain("# ServiceOwners: @serviceowner1 @serviceowner2"));
    }

    [Test]
    public void FormatCodeownersEntry_FormatsEntryWithAzureSdkOwners()
    {
        var entry = new CodeownersEntry
        {
            PathExpression = "/sdk/core/",
            SourceOwners = ["developer"],
            AzureSdkOwners = ["sdkowner1"],
            ServiceLabels = ["Core"]
        };

        var formatted = entry.FormatCodeownersEntry();

        Assert.That(formatted, Does.Contain("# AzureSdkOwners: @sdkowner1"));
    }

    #endregion

    #region Integration-like Tests

    [Test]
    public void BuildCodeownersEntries_CreatesEntriesFromPackages()
    {
        // Arrange
        var data = new WorkItemDataBuilder()
            .AddOwner("storagedev", out var ownerId)
            .AddLabel("Storage", out var labelId)
            .AddPackage("Azure.Storage.Blobs", out _, relatedTo: [ownerId, labelId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase)
        {
            { "Azure.Storage.Blobs", new RepoPackage { Name = "Azure.Storage.Blobs", DirectoryPath = Path.Combine(_repoRoot, "sdk", "storage", "Azure.Storage.Blobs") } }
        };

        // Act
        var entries = InvokeBuildCodeownersEntries(data, packageLookup);

        // Assert
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(entries[0].PathExpression, Does.Contain("/sdk/storage/Azure.Storage.Blobs/"));
            Assert.That(entries[0].SourceOwners, Does.Contain("storagedev"));
            Assert.That(entries[0].PRLabels, Does.Contain("Storage"));
        });
    }

    [Test]
    public void BuildCodeownersEntries_CreatesPathlessEntriesFromUnlinkedLabelOwners()
    {
        // Arrange: Label Owner without RepoPath, not linked to any package
        var data = new WorkItemDataBuilder()
            .AddOwner("serviceresponse", out var ownerId)
            .AddLabel("TestService", out var labelId)
            .AddServiceOwner(out _, relatedTo: [ownerId, labelId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase);

        // Act
        var entries = InvokeBuildCodeownersEntries(data, packageLookup);

        // Assert
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(entries[0].PathExpression, Is.EqualTo(""));
            Assert.That(entries[0].ServiceLabels, Does.Contain("TestService"));
            Assert.That(entries[0].ServiceOwners, Does.Contain("serviceresponse"));
        });
    }

    [Test]
    public void BuildCodeownersEntries_CreatesServiceLevelPathEntries()
    {
        // Arrange: Label Owner with RepoPath but not linked to any package
        var data = new WorkItemDataBuilder()
            .AddOwner("aidev", out var ownerId)
            .AddLabel("AI", out var labelId)
            .AddPRLabelOwner(out _, repoPath: "sdk/ai", relatedTo: [ownerId, labelId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase);

        // Act
        var entries = InvokeBuildCodeownersEntries(data, packageLookup);

        // Assert
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(entries[0].PathExpression, Is.EqualTo("/sdk/ai/"));
            Assert.That(entries[0].SourceOwners, Does.Contain("aidev"));
            Assert.That(entries[0].PRLabels, Does.Contain("AI"));
        });
    }

    [Test]
    public void BuildCodeownersEntries_IncludesServiceAttentionLabel()
    {
        // Arrange
        var data = new WorkItemDataBuilder()
            .AddOwner("dev", out var ownerId)
            .AddLabel("TestLabel", out var labelId)
            .AddLabel("Service Attention", out var serviceAttentionId)
            .AddPackage("TestPackage", out _, relatedTo: [ownerId, labelId, serviceAttentionId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase)
        {
            { "TestPackage", new RepoPackage { Name = "TestPackage", DirectoryPath = Path.Combine(_repoRoot, "sdk", "test") } }
        };

        // Act
        var entries = InvokeBuildCodeownersEntries(data, packageLookup);

        // Assert
        Assert.That(entries[0].PRLabels, Does.Contain("TestLabel"));
        Assert.That(entries[0].PRLabels, Does.Contain("Service Attention"));
    }

    [Test]
    public void BuildCodeownersEntries_AddsLabelOwnerMetadataToPackageEntry()
    {
        // Arrange: Package linked to a Label Owner with ServiceOwner type
        var data = new WorkItemDataBuilder()
            .AddOwner("coredev", out var coredevId)
            .AddOwner("coreserviceowner", out var svcOwnerId)
            .AddLabel("Core", out var labelId)
            .AddServiceOwner(out var loId, relatedTo: [svcOwnerId, labelId])
            .AddPackage("Azure.Core", out _, relatedTo: [coredevId, labelId, loId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase)
        {
            { "Azure.Core", new RepoPackage { Name = "Azure.Core", DirectoryPath = Path.Combine(_repoRoot, "sdk", "core", "Azure.Core") } }
        };

        // Act
        var entries = InvokeBuildCodeownersEntries(data, packageLookup);

        // Assert
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(entries[0].SourceOwners, Does.Contain("coredev"));
            Assert.That(entries[0].ServiceOwners, Does.Contain("coreserviceowner"));
            Assert.That(entries[0].ServiceLabels, Does.Contain("Core"));
        });
    }

    [Test]
    public void BuildCodeownersEntries_MultiplePackagesWithSharedLabelOwner()
    {
        // Arrange: Two packages linked to the same Label Owner
        var data = new WorkItemDataBuilder()
            .AddOwner("storagedev", out var storageDevId)
            .AddOwner("coredev", out var coreDevId)
            .AddOwner("sharedserviceowner", out var sharedOwnerId)
            .AddLabel("Storage", out var storageLabel)
            .AddLabel("Core", out var coreLabel)
            .AddServiceOwner(out var loId, relatedTo: [sharedOwnerId, storageLabel, coreLabel])
            .AddPackage("Azure.Storage.Blobs", out _, relatedTo: [storageDevId, storageLabel, loId])
            .AddPackage("Azure.Core", out _, relatedTo: [coreDevId, coreLabel, loId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase)
        {
            { "Azure.Storage.Blobs", new RepoPackage { Name = "Azure.Storage.Blobs", DirectoryPath = Path.Combine(_repoRoot, "sdk", "storage", "Azure.Storage.Blobs") } },
            { "Azure.Core", new RepoPackage { Name = "Azure.Core", DirectoryPath = Path.Combine(_repoRoot, "sdk", "core", "Azure.Core") } }
        };

        // Act
        var entries = InvokeBuildCodeownersEntries(data, packageLookup);

        // Assert - both packages should have the shared service owner
        Assert.That(entries, Has.Count.EqualTo(2));
        foreach (var entry in entries)
        {
            Assert.That(entry.ServiceOwners, Does.Contain("sharedserviceowner"));
        }
    }

    [Test]
    public void BuildCodeownersEntries_PackageWithAzureSdkOwner()
    {
        // Arrange: Package with Azure SDK Owner label owner
        var data = new WorkItemDataBuilder()
            .AddOwner("pkgdev", out var pkgDevId)
            .AddOwner("sdkowner", out var sdkOwnerId)
            .AddLabel("Storage", out var labelId)
            .AddAzureSdkOwner(out var loId, relatedTo: [sdkOwnerId, labelId])
            .AddPackage("Azure.Storage.Blobs", out _, relatedTo: [pkgDevId, labelId, loId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase)
        {
            { "Azure.Storage.Blobs", new RepoPackage { Name = "Azure.Storage.Blobs", DirectoryPath = Path.Combine(_repoRoot, "sdk", "storage", "Azure.Storage.Blobs") } }
        };

        // Act
        var entries = InvokeBuildCodeownersEntries(data, packageLookup);

        // Assert
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(entries[0].SourceOwners, Does.Contain("pkgdev"));
            Assert.That(entries[0].AzureSdkOwners, Does.Contain("sdkowner"));
        });
    }

    private List<CodeownersEntry> InvokeBuildCodeownersEntries(WorkItemData data, Dictionary<string, RepoPackage> packageLookup)
    {
        var method = typeof(CodeownersRenderHelper).GetMethod(
            "BuildCodeownersEntries",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var helper = new CodeownersRenderHelper(
            _mockDevOpsService.Object,
            _mockPowershellHelper.Object,
            _mockInputSanitizer.Object,
            _logger);

        return method?.Invoke(helper, [data, packageLookup, _repoRoot]) as List<CodeownersEntry> ?? [];
    }

    #endregion

    #region End-to-End Output Tests

    [Test]
    public void FullOutputTest_SimplePackageEntry()
    {
        // Arrange
        var entry = new CodeownersEntry
        {
            PathExpression = "/sdk/storage/Azure.Storage.Blobs/",
            SourceOwners = ["alice", "bob"],
            OriginalSourceOwners = ["alice", "bob"],
            PRLabels = ["Storage"]
        };

        var entries = new List<CodeownersEntry> { entry };
        CodeownersEntrySorter.SortOwnersInPlace(entries);
        CodeownersEntrySorter.SortLabelsInPlace(entries);

        var formatted = entries[0].FormatCodeownersEntry();

        // Assert expected format
        var expectedLines = new[]
        {
            "# PRLabel: %Storage",
            "/sdk/storage/Azure.Storage.Blobs/    @alice @bob"
        };

        foreach (var line in expectedLines)
        {
            Assert.That(formatted, Does.Contain(line));
        }
    }

    [Test]
    public void FullOutputTest_ComplexEntry()
    {
        // Arrange: Entry with all metadata
        var entry = new CodeownersEntry
        {
            PathExpression = "/sdk/core/Azure.Core/",
            SourceOwners = ["coredev2", "coredev1"],
            OriginalSourceOwners = ["coredev2", "coredev1"],
            PRLabels = ["Core", "Azure"],
            ServiceLabels = ["Core"],
            ServiceOwners = ["svcowner"],
            OriginalServiceOwners = ["svcowner"],
            AzureSdkOwners = ["sdkowner"],
            OriginalAzureSdkOwners = ["sdkowner"]
        };

        var entries = new List<CodeownersEntry> { entry };
        CodeownersEntrySorter.SortOwnersInPlace(entries);
        CodeownersEntrySorter.SortLabelsInPlace(entries);

        var formatted = entries[0].FormatCodeownersEntry();

        // Assert
        // Note: For pathed entries, ServiceOwners are NOT included (they're inferred from source owners)
        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("# PRLabel: %Azure %Core"));
            Assert.That(formatted, Does.Contain("/sdk/core/Azure.Core/    @coredev1 @coredev2"));
            Assert.That(formatted, Does.Contain("# AzureSdkOwners: @sdkowner"));
            Assert.That(formatted, Does.Contain("# ServiceLabel: %Core"));
            // ServiceOwners is NOT in pathed entries - it's inferred from source owners
            Assert.That(formatted, Does.Not.Contain("# ServiceOwners:"));
        });
    }

    [Test]
    public void FullOutputTest_PathlessEntry()
    {
        // Arrange: Pathless entry for triage
        var entry = new CodeownersEntry
        {
            PathExpression = "",
            ServiceLabels = ["Storage", "Analytics"],
            ServiceOwners = ["storageservice"],
            OriginalServiceOwners = ["storageservice"]
        };

        var entries = new List<CodeownersEntry> { entry };
        CodeownersEntrySorter.SortLabelsInPlace(entries);

        var formatted = entries[0].FormatCodeownersEntry();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("# ServiceLabel: %Analytics %Storage"));
            Assert.That(formatted, Does.Contain("# ServiceOwners: @storageservice"));
            // Should NOT contain a path line
            Assert.That(formatted, Does.Not.Contain("/sdk/"));
        });
    }

    #endregion

    #region RepoPackage Tests

    [Test]
    public void RepoPackage_PropertiesAreSetCorrectly()
    {
        var package = new RepoPackage
        {
            Name = "Azure.Storage.Blobs",
            DirectoryPath = "/sdk/storage/Azure.Storage.Blobs"
        };

        Assert.Multiple(() =>
        {
            Assert.That(package.Name, Is.EqualTo("Azure.Storage.Blobs"));
            Assert.That(package.DirectoryPath, Is.EqualTo("/sdk/storage/Azure.Storage.Blobs"));
        });
    }

    #endregion
}
