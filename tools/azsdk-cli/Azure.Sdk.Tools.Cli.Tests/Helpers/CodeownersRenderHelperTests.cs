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

    #endregion

    #region GetLanguageFromRepoName Tests

    [TestCase("Azure/azure-sdk-for-net", "net", ".NET")]
    [TestCase("Azure/azure-sdk-for-python", "python", "Python")]
    [TestCase("Azure/azure-sdk-for-java", "java", "Java")]
    [TestCase("Azure/azure-sdk-for-js", "js", "JavaScript")]
    [TestCase("Azure/azure-sdk-for-go", "go", "Go")]
    [TestCase("Azure/azure-sdk-for-cpp", "cpp", "C++")]
    [TestCase("azure-sdk-for-net", "net", ".NET")]
    public void GetLanguageFromRepoName_ReturnsExpectedLanguage(string repoName, string expectedSuffix, string expectedLanguage)
    {
        // Setup mock to return expected language for the suffix
        _mockInputSanitizer.Setup(s => s.SanitizeLanguage(expectedSuffix)).Returns(expectedLanguage);

        var helper = new CodeownersRenderHelper(
            _mockDevOpsService.Object,
            _mockPowershellHelper.Object,
            _mockInputSanitizer.Object,
            _logger);

        var result = helper.GetLanguageFromRepoName(repoName);

        Assert.That(result, Is.EqualTo(expectedLanguage));
        _mockInputSanitizer.Verify(s => s.SanitizeLanguage(expectedSuffix), Times.Once);
    }

    [Test]
    public void GetLanguageFromRepoName_ThrowsForInvalidRepoName()
    {
        var helper = new CodeownersRenderHelper(
            _mockDevOpsService.Object,
            _mockPowershellHelper.Object,
            _mockInputSanitizer.Object,
            _logger);

        Assert.Throws<ArgumentException>(() => helper.GetLanguageFromRepoName("unknown-repo"));
    }

    #endregion

    #region BuildPathExpression Tests

    [TestCase(@"C:\repos\sdk\storage\Azure.Storage.Blobs", @"C:\repos", "/sdk/storage/Azure.Storage.Blobs/")]
    [TestCase(@"C:\repos\sdk\core", @"C:\repos", "/sdk/core/")]
    [TestCase("sdk/storage", "", "/sdk/storage/")]
    public void BuildPathExpression_ReturnsExpectedPath(string dirPath, string repoRoot, string expected)
    {
        var result = CodeownersRenderHelper.BuildPathExpression(dirPath, repoRoot);
        Assert.That(result, Is.EqualTo(expected));
    }

    #endregion

    #region Integration-like Tests

    [Test]
    public void BuildCodeownersEntries_SkipsPackagesNotInRepo()
    {
        // A package work item with an owner and a label
        var data = new WorkItemDataBuilder()
            .AddOwner("owner1", out var ownerId)
            .AddLabel("label1", out var labelId)
            .AddPackage("Unrelated.Package", out _, relatedTo: [ownerId, labelId])
            .Build();

        // The repo has an unrelated package
        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase)
        {
            { "Azure.Storage.Blobs", new RepoPackage { Name = "Azure.Storage.Blobs", DirectoryPath = Path.Combine(_repoRoot, "sdk", "storage", "Azure.Storage.Blobs") } }
        };

        var entries = InvokeBuildCodeownersEntries(data, packageLookup);

        Assert.That(entries, Has.Count.EqualTo(0));
    }

    [Test]
    public void BuildCodeownersEntries_SkipsUnlinkedLabelOwnersWithNoSourceOwners()
    {
        // TODO: Linter should report on this
        // A label owner work item with a path and no owners
        var data = new WorkItemDataBuilder()
            .AddLabel("label1", out var labelId)
            .AddLabelOwner("Service Owner", out _, repoPath: "/sdk/repo/path", relatedTo: [labelId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase);

        var entries = InvokeBuildCodeownersEntries(data, packageLookup);
        Assert.That(entries, Has.Count.EqualTo(0));
    }

    [Test]
    public void BuildCodeownersEntries_SkipsPathlessEntryWithNoLabels()
    {
        // TODO: Linter should report this
        // A label owner work item with no path and no label
        var data = new WorkItemDataBuilder()
            .AddOwner("owner1", out var ownerId)
            .AddLabelOwner("Service Owner", out _, relatedTo: [ownerId])
            .Build();

        var packageLookup = new Dictionary<string, RepoPackage>(StringComparer.OrdinalIgnoreCase);

        var entries = InvokeBuildCodeownersEntries(data, packageLookup);
        Assert.That(entries, Has.Count.EqualTo(0));
    }

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
