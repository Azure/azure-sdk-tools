// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class CodeownersManagementHelperTests
{
    private Mock<IDevOpsService> _mockDevOps;
    private Mock<ICodeownersValidatorHelper> _mockValidator;
    private CodeownersManagementHelper _helper;

    [SetUp]
    public void Setup()
    {
        _mockDevOps = new Mock<IDevOpsService>();
        _mockValidator = new Mock<ICodeownersValidatorHelper>();
        _helper = new CodeownersManagementHelper(
            _mockDevOps.Object,
            _mockValidator.Object,
            new TestLogger<CodeownersManagementHelper>()
        );
    }

    // ========================
    // FindOrCreateOwner tests
    // ========================

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task FindOrCreateOwner_ExistingOwner_ReturnsExisting()
    {
        // Arrange: mock QueryWorkItemsByTypeAndFieldAsync to return an existing Owner work item
        // Act: call FindOrCreateOwnerAsync
        // Assert: returns existing owner, CreateTypedWorkItemAsync NOT called
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task FindOrCreateOwner_NewOwner_CreatesAndReturns()
    {
        // Arrange: mock QueryWorkItemsByTypeAndFieldAsync to return empty, mock ValidateCodeOwnerAsync to return valid
        // Act: call FindOrCreateOwnerAsync
        // Assert: CreateTypedWorkItemAsync called, returns new owner
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task FindOrCreateOwner_InvalidAlias_ThrowsError()
    {
        // Arrange: mock ValidateCodeOwnerAsync to return IsValidCodeOwner = false
        // Act/Assert: FindOrCreateOwnerAsync throws Exception
        await Task.CompletedTask;
    }

    // ========================
    // Add operation tests
    // ========================

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task AddOwnerToPackage_CreatesRelatedLink()
    {
        // Arrange: mock owner exists, package exists
        // Act: AddOwnerToPackageAsync
        // Assert: AddRelatedLinkAsync called with correct IDs
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task AddOwnerToPackage_DuplicateLink_SkipsSilently()
    {
        // Arrange: mock AddRelatedLinkAsync is idempotent (no throw)
        // Act: AddOwnerToPackageAsync
        // Assert: no error, returns success message
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task AddOwnerToLabel_ServiceOwner_CreatesRelationships()
    {
        // Arrange: mock owner, label exists, label owner created
        // Act: AddOwnerToLabelAsync with "service-owner"
        // Assert: AddRelatedLinkAsync called for Owner→LabelOwner and Label→LabelOwner
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task AddOwnerToLabel_PrLabel_CreatesWithPath()
    {
        // Arrange: mock owner, label exists, path provided
        // Act: AddOwnerToLabelAsync with "pr-label" and path
        // Assert: Label Owner created with RepoPath set
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task AddOwnerToPath_CreatesLabelOwnerAndLink()
    {
        // Arrange: mock owner valid, no existing label owner for path
        // Act: AddOwnerToPathAsync
        // Assert: CreateTypedWorkItemAsync called for Label Owner, AddRelatedLinkAsync called
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task AddLabelToPath_CreatesRelationship()
    {
        // Arrange: mock label exists, label owner exists for path
        // Act: AddLabelToPathAsync
        // Assert: AddRelatedLinkAsync called for Label→LabelOwner
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task AddLabelToPath_LabelNotFound_ThrowsError()
    {
        // Arrange: mock label does not exist
        // Act/Assert: AddLabelToPathAsync throws Exception about label not found
        await Task.CompletedTask;
    }

    // ========================
    // Remove operation tests
    // ========================

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task RemoveOwnerFromPackage_RemovesRelatedLink()
    {
        // Arrange: mock owner and package exist
        // Act: RemoveOwnerFromPackageAsync
        // Assert: RemoveRelatedLinkAsync called
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task RemoveOwnerFromLabel_RemovesRelatedLink()
    {
        // Arrange: mock owner, label owner with matching type/repo
        // Act: RemoveOwnerFromLabelAsync
        // Assert: RemoveRelatedLinkAsync called
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task RemoveOwnerFromLabel_LastOwner_Warns()
    {
        // Arrange: mock label owner has only this owner
        // Act: RemoveOwnerFromLabelAsync
        // Assert: returns message with warning
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task RemoveOwnerFromPath_RemovesRelatedLink()
    {
        // Arrange: mock owner, label owner matching path+type
        // Act: RemoveOwnerFromPathAsync
        // Assert: RemoveRelatedLinkAsync called
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task RemoveLabelFromPath_RemovesRelatedLink()
    {
        // Arrange: mock label, label owner matching path
        // Act: RemoveLabelFromPathAsync
        // Assert: RemoveRelatedLinkAsync called for label→label owner
        await Task.CompletedTask;
    }

    // ========================
    // View operation tests
    // ========================

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task GetViewByUser_ReturnsPackagesAndLabelOwners()
    {
        // Arrange: use WorkItemDataBuilder to create owner with related packages and label owners
        // Act: GetViewByUserAsync
        // Assert: result contains packages and label owners with correct details
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task GetViewByLabel_ReturnsPackagesAndLabelOwners()
    {
        // Arrange: use WorkItemDataBuilder to create label with related packages and label owners
        // Act: GetViewByLabelAsync
        // Assert: result contains correct associations
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task GetViewByPath_ReturnsMatchingLabelOwners()
    {
        // Arrange: use WorkItemDataBuilder to create label owners with matching paths
        // Act: GetViewByPathAsync
        // Assert: result contains label owners grouped by path
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task GetViewByPackage_ReturnsOwnersAndLabels()
    {
        // Arrange: use WorkItemDataBuilder to create package with related owners and labels
        // Act: GetViewByPackageAsync
        // Assert: result contains package details with owners and labels
        await Task.CompletedTask;
    }

    // ========================
    // Static helper tests (no mocking needed)
    // ========================

    [Test]
    public void NormalizeAlias_StripsAtSign()
    {
        Assert.That(CodeownersManagementHelper.NormalizeAlias("@johndoe"), Is.EqualTo("johndoe"));
        Assert.That(CodeownersManagementHelper.NormalizeAlias("johndoe"), Is.EqualTo("johndoe"));
        Assert.That(CodeownersManagementHelper.NormalizeAlias(" @johndoe "), Is.EqualTo("johndoe"));
    }

    [Test]
    public void ResolveOwnerType_ValidTypes_ReturnsMapping()
    {
        Assert.That(CodeownersManagementHelper.ResolveOwnerType("service-owner"), Is.EqualTo("Service Owner"));
        Assert.That(CodeownersManagementHelper.ResolveOwnerType("azsdk-owner"), Is.EqualTo("Azure SDK Owner"));
        Assert.That(CodeownersManagementHelper.ResolveOwnerType("pr-label"), Is.EqualTo("PR Label"));
    }

    [Test]
    public void ResolveOwnerType_InvalidType_ThrowsException()
    {
        Assert.Throws<Exception>(() => CodeownersManagementHelper.ResolveOwnerType("invalid-type"));
    }
}
