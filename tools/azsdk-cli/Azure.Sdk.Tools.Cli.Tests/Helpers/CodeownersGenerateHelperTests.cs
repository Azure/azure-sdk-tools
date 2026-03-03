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
public class CodeownersGenerateHelperTests
{
    private TempDirectory _tempDir = null!;
    private ILogger<CodeownersGenerateHelper> _logger = null!;
    private Mock<IDevOpsService> _mockDevOpsService = null!;
    private Mock<IPowershellHelper> _mockPowershellHelper = null!;
    private Mock<IInputSanitizer> _mockInputSanitizer = null!;
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = TempDirectory.Create("codeownersGenerateHelperTests");
        _repoRoot = _tempDir.DirectoryPath;
        _logger = new TestLogger<CodeownersGenerateHelper>();
        _mockDevOpsService = new Mock<IDevOpsService>();
        _mockPowershellHelper = new Mock<IPowershellHelper>();
        _mockInputSanitizer = new Mock<IInputSanitizer>();
    }

    [TearDown]
    public void TearDown()
    {
        _tempDir.Dispose();
    }

    #region Helper Methods

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
        var method = typeof(CodeownersGenerateHelper).GetMethod(
            "BuildCodeownersEntries",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var helper = new CodeownersGenerateHelper(
            _mockDevOpsService.Object,
            _mockPowershellHelper.Object,
            _logger
        );

        return method?.Invoke(helper, [data, packageLookup, _repoRoot]) as List<CodeownersEntry> ?? [];
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
