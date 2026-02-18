// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tools.Config;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Config;

[TestFixture]
public class CodeownersToolCommandTests
{
    private MockGitHubService _mockGithub;
    private Mock<ICodeownersValidatorHelper> _mockValidator;
    private Mock<ICodeownersGenerateHelper> _mockGenerateHelper;
    private Mock<IGitHelper> _mockGitHelper;
    private Mock<ICodeownersManagementHelper> _mockManagementHelper;
    private CodeownersTool _tool;

    [SetUp]
    public void Setup()
    {
        _mockGithub = new MockGitHubService();
        _mockValidator = new Mock<ICodeownersValidatorHelper>();
        _mockGenerateHelper = new Mock<ICodeownersGenerateHelper>();
        _mockGitHelper = new Mock<IGitHelper>();
        _mockManagementHelper = new Mock<ICodeownersManagementHelper>();

        _tool = new CodeownersTool(
            _mockGithub,
            new TestLogger<CodeownersTool>(),
            null,
            _mockValidator.Object,
            _mockGenerateHelper.Object,
            _mockGitHelper.Object,
            _mockManagementHelper.Object);
    }

    [Test]
    public async Task ViewCommand_NoAxisSpecified_ReturnsError()
    {
        var result = await _tool.ViewCodeowners();
        Assert.That(result.ResponseError, Does.Contain("Exactly one"));
    }

    [Test]
    public async Task ViewCommand_MultipleAxesSpecified_ReturnsError()
    {
        var result = await _tool.ViewCodeowners(user: "alice", label: "Storage");
        Assert.That(result.ResponseError, Does.Contain("Only one"));
    }

    [Test]
    public async Task AddCommand_UserPackage_WithOwnerType_ReturnsError()
    {
        var result = await _tool.AddCodeowners("Azure/azure-sdk-for-net", user: "alice", package: "Azure.Storage.Blobs", ownerType: "service-owner");
        Assert.That(result.ResponseError, Does.Contain("--owner-type must not be specified"));
    }

    [Test]
    public async Task AddCommand_UserLabel_MissingOwnerType_ReturnsError()
    {
        var result = await _tool.AddCodeowners("Azure/azure-sdk-for-net", user: "alice", label: ["Storage"]);
        Assert.That(result.ResponseError, Does.Contain("--owner-type is required"));
    }

    [Test]
    public async Task AddCommand_UserLabel_PrLabel_MissingPath_ReturnsError()
    {
        var result = await _tool.AddCodeowners("Azure/azure-sdk-for-net", user: "alice", label: ["Storage"], ownerType: "pr-label");
        Assert.That(result.ResponseError, Does.Contain("--path is required"));
    }

    [Test]
    public async Task AddCommand_UserPath_MissingOwnerType_ReturnsError()
    {
        var result = await _tool.AddCodeowners("Azure/azure-sdk-for-net", user: "alice", path: "sdk/storage/");
        Assert.That(result.ResponseError, Does.Contain("--owner-type is required"));
    }

    [Test]
    public async Task AddCommand_LabelPath_WithUser_ReturnsError()
    {
        var result = await _tool.AddCodeowners("Azure/azure-sdk-for-net", user: "alice", label: ["Storage"], path: "sdk/storage/");
        // When user+label is present with path but no ownerType, it should error about ownerType
        Assert.That(result.ResponseError, Does.Contain("--owner-type is required"));
    }

    [Test]
    public async Task RemoveCommand_UserPath_MissingOwnerType_ReturnsError()
    {
        var result = await _tool.RemoveCodeowners("Azure/azure-sdk-for-net", user: "alice", path: "sdk/storage/");
        Assert.That(result.ResponseError, Does.Contain("--owner-type is required"));
    }

    [Test]
    public async Task AddCommand_MissingRepo_ReturnsError()
    {
        var result = await _tool.AddCodeowners("", user: "alice", package: "Azure.Storage.Blobs");
        Assert.That(result.ResponseError, Does.Contain("--repo is required"));
    }

    [Test]
    public async Task RemoveCommand_MissingRepo_ReturnsError()
    {
        var result = await _tool.RemoveCodeowners("", user: "alice", package: "Azure.Storage.Blobs");
        Assert.That(result.ResponseError, Does.Contain("--repo is required"));
    }

    [Test]
    public async Task ViewCommand_NormalizesAlias()
    {
        _mockManagementHelper
            .Setup(m => m.GetViewByUserAsync("alice", null))
            .ReturnsAsync(new CodeownersViewResult { Message = "Found" });

        var result = await _tool.ViewCodeowners(user: "@alice");
        Assert.That(result.Message, Is.EqualTo("Found"));
    }
}
