// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

public class PackageWorkItemToolTests
{
    private Mock<IDevOpsService> devOpsService;
    private PackageWorkItemTool tool;

    [SetUp]
    public void Setup()
    {
        devOpsService = new Mock<IDevOpsService>();
        tool = new PackageWorkItemTool(devOpsService.Object, new TestLogger<PackageWorkItemTool>());
    }

    [Test]
    public async Task GetPackageWorkItemReturnsObjectForSingularMatch()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);
        devOpsService.Setup(service => service.GetWorkItemsByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 31370 })), 200, WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python")]);

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.WorkItemId, Is.EqualTo(31370));
            Assert.That(result.PackageName, Is.EqualTo("azure-storage-blob"));
            Assert.That(result.Version, Is.EqualTo("12.30"));
        });
    }

    [Test]
    public async Task GetPackageWorkItemReturnsErrorWhenNoMatchExists()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("No package work item found"));
        });
    }

    [Test]
    public async Task GetPackageWorkItemReturnsErrorWhenMultipleMatchesExist()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2]);

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Expected one package work item"));
            Assert.That(result.ResponseError, Does.Contain("1, 2"));
            Assert.That(result.NextSteps, Is.Null);
        });
    }

    [Test]
    [TestCase("12.30.0", "12.30")]
    [TestCase("12.30.0-beta.1", "12.30")]
    [TestCase("12", "12.0")]
    public async Task GetPackageWorkItemNormalizesPackageVersionBeforeLookup(string packageVersion, string expectedPackageVersionMajorMinor)
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", expectedPackageVersionMajorMinor, It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);
        devOpsService.Setup(service => service.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), 200, WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreatePackageWorkItem(31370, "azure-storage-blob", expectedPackageVersionMajorMinor, "Python")]);

        var result = await tool.GetPackageWorkItem("azure-storage-blob", packageVersion, "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Version, Is.EqualTo(expectedPackageVersionMajorMinor));
        });
    }

    [Test]
    public async Task GetPackageWorkItemReturnsErrorForInvalidPackageVersion()
    {
        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.x", "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Package version must be a major version, major.minor version, or full SemVer version"));
        });
    }

    [Test]
    public async Task UpdatePackageWorkItemPatchesResolvedWorkItem()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);
        devOpsService.Setup(service => service.UpdateWorkItemAsync(31370, It.Is<Dictionary<string, string>>(fields =>
                fields["System.State"] == "Resolved" && fields["Custom.APIReviewStatus"] == "Approved"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python", "Resolved"));

        var result = await tool.UpdatePackageWorkItem("azure-storage-blob", "12.30", "Python", new Dictionary<string, string>
        {
            ["System.State"] = "Resolved",
            ["Custom.APIReviewStatus"] = "Approved",
        }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.WorkItemId, Is.EqualTo(31370));
            Assert.That(result.State, Is.EqualTo("Resolved"));
        });
    }

    [Test]
    public void CommandParsesPackageWorkItemLookupOptions()
    {
        var commands = tool.GetCommandInstances();
        var getCommand = commands.Single(command => command.Name == "get-work-item");
        var updateCommand = commands.Single(command => command.Name == "update-work-item");
        var parseConfig = new CommandLineConfiguration(getCommand)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = getCommand.Parse("--package-name azure-storage-blob --package-version 12.30 --language Python", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        parseResult = getCommand.Parse("--package-name azure-storage-blob --package-version-major-minor 12.30 --language Python", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        parseConfig = new CommandLineConfiguration(updateCommand)
        {
            ResponseFileTokenReplacer = null
        };
        parseResult = updateCommand.Parse("--package-name azure-storage-blob --package-version 12.30 --language Python --field System.State=Resolved --field Custom.APIReviewStatus=Approved", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);
    }

    private static WorkItem CreatePackageWorkItem(int id, string packageName, string packageVersionMajorMinor, string language, string state = "Active")
    {
        return new WorkItem
        {
            Id = id,
            Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{id}",
            Fields = new Dictionary<string, object>
            {
                ["Custom.Package"] = packageName,
                ["Custom.PackageVersion"] = packageVersionMajorMinor,
                ["Custom.Language"] = language,
                ["System.State"] = state,
                ["Custom.PackageDisplayName"] = packageName,
                ["Custom.PackageType"] = "client",
            }
        };
    }
}