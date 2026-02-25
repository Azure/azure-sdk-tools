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
    private CodeownersManagementHelper _helper;

    [SetUp]
    public void Setup()
    {
        _mockDevOps = new Mock<IDevOpsService>();
        _helper = new CodeownersManagementHelper(
            _mockDevOps.Object,
            new TestLogger<CodeownersManagementHelper>()
        );
    }

    // ========================
    // View operation tests
    // ========================

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task GetViewByUser_ReturnsPackagesAndLabelOwners()
    {
        // Arrange: use WorkItemDataBuilder to create owner with related packages and label owners
        // Act: GetViewByUser
        // Assert: result contains packages and label owners with correct details
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task GetViewByLabel_ReturnsPackagesAndLabelOwners()
    {
        // Arrange: use WorkItemDataBuilder to create label with related packages and label owners
        // Act: GetViewByLabel
        // Assert: result contains correct associations
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task GetViewByPath_ReturnsMatchingLabelOwners()
    {
        // Arrange: use WorkItemDataBuilder to create label owners with matching paths
        // Act: GetViewByPath
        // Assert: result contains label owners grouped by path
        await Task.CompletedTask;
    }

    [Test]
    [Ignore("Not yet implemented — requires WorkItemDataBuilder integration")]
    public async Task GetViewByPackage_ReturnsOwnersAndLabels()
    {
        // Arrange: use WorkItemDataBuilder to create package with related owners and labels
        // Act: GetViewByPackage
        // Assert: result contains package details with owners and labels
        await Task.CompletedTask;
    }

    // ========================
    // Static helper tests (no mocking needed)
    // ========================

    [Test]
    public void NormalizeGitHubAlias_StripsAtSign()
    {
        Assert.That(CodeownersManagementHelper.NormalizeGitHubAlias("@johndoe"), Is.EqualTo("johndoe"));
        Assert.That(CodeownersManagementHelper.NormalizeGitHubAlias("johndoe"), Is.EqualTo("johndoe"));
        Assert.That(CodeownersManagementHelper.NormalizeGitHubAlias(" @johndoe "), Is.EqualTo("johndoe"));
    }
}
