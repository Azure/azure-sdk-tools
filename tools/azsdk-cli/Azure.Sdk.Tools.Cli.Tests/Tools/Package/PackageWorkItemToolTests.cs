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

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", workItemId: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Id, Is.EqualTo(31370));
            Assert.That(result.Fields, Contains.Key("Custom.Package"));
            Assert.That(result.Fields!["Custom.Package"], Is.EqualTo("azure-storage-blob"));
        });
    }

    [Test]
    public async Task GetPackageWorkItemIncludesRequestedFieldKeys()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);
        devOpsService.Setup(service => service.GetWorkItemsByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 31370 })), 200, WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreatePackageWorkItem(
                31370,
                "azure-storage-blob",
                "12.30",
                "Python",
                pendingApiReviews: "https://apiview.dev/review/123",
                specProjectPath: "specification/storage/data-plane/Azure.Storage.Blob")]);

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", workItemId: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Fields, Contains.Key("Custom.PendingAPIReviews"));
            Assert.That(result.Fields, Contains.Key("Custom.SpecProjectPath"));
            Assert.That(result.Fields!["Custom.PendingAPIReviews"], Is.EqualTo("https://apiview.dev/review/123"));
            Assert.That(result.Fields!["Custom.SpecProjectPath"], Is.EqualTo("specification/storage/data-plane/Azure.Storage.Blob"));
        });
    }

    [Test]
    public async Task GetPackageWorkItemIncludesRawWorkItemData()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);
        devOpsService.Setup(service => service.GetWorkItemsByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 31370 })), 200, WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python")]);

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", workItemId: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(31370));
            Assert.That(result.Rev, Is.EqualTo(5));
            Assert.That(result.Url, Does.Contain("/workItems/31370"));
            Assert.That(result.Fields, Is.Not.Null);
            Assert.That(result.Fields, Contains.Key("Custom.Package"));
            Assert.That(result.Fields!["Custom.Package"], Is.EqualTo("azure-storage-blob"));
            Assert.That(result.Relations, Is.Not.Null);
            Assert.That(result.Relations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task GetPackageWorkItemReturnsErrorWhenNoMatchExists()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", workItemId: null, CancellationToken.None);

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

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", workItemId: null, CancellationToken.None);

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

        var result = await tool.GetPackageWorkItem("azure-storage-blob", packageVersion, "Python", workItemId: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Fields, Contains.Key("Custom.PackageVersion"));
            Assert.That(result.Fields!["Custom.PackageVersion"], Is.EqualTo(expectedPackageVersionMajorMinor));
        });
    }

    [Test]
    public async Task GetPackageWorkItemReturnsErrorForInvalidPackageVersion()
    {
        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.x", "Python", workItemId: null, CancellationToken.None);

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
            fields["System.State"] == "Resolved" && fields["Custom.APIReviewStatus"] == "Approved"), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python", "Resolved"));

        var result = await tool.UpdatePackageWorkItem("azure-storage-blob", "12.30", "Python", new Dictionary<string, string>
        {
            ["System.State"] = "Resolved",
            ["Custom.APIReviewStatus"] = "Approved",
        }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Id, Is.EqualTo(31370));
            Assert.That(result.Fields, Contains.Key("System.State"));
            Assert.That(result.Fields!["System.State"], Is.EqualTo("Resolved"));
        });
    }

    [Test]
    public async Task UpdatePackageWorkItemReturnsErrorForInvalidPackageVersion()
    {
        var result = await tool.UpdatePackageWorkItem("azure-storage-blob", "12.x", "Python", new Dictionary<string, string>
        {
            ["System.State"] = "Resolved"
        }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Package version must be a major version"));
        });
    }

    [Test]
    public async Task GetPackageWorkItemUsesWorkItemIdWhenProvidedWithoutMetadata()
    {
        devOpsService.Setup(service => service.GetWorkItemsByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 31370 })), 200, WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python")]);

        var result = await tool.GetPackageWorkItem(null, null, null, 31370, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Id, Is.EqualTo(31370));
            Assert.That(result.Fields, Contains.Key("Custom.Package"));
            Assert.That(result.Fields!["Custom.Package"], Is.EqualTo("azure-storage-blob"));
        });

        devOpsService.Verify(service => service.FindPackageWorkItemIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task UpdatePackageWorkItemUsesWorkItemIdWhenProvidedWithoutMetadata()
    {
        devOpsService.Setup(service => service.UpdateWorkItemAsync(31370, It.Is<Dictionary<string, string>>(fields => fields["System.State"] == "Resolved"), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python", "Resolved"));

        var result = await tool.UpdatePackageWorkItem(null, null, null, new Dictionary<string, string>
        {
            ["System.State"] = "Resolved"
        }, 31370, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Id, Is.EqualTo(31370));
            Assert.That(result.Fields, Contains.Key("System.State"));
            Assert.That(result.Fields!["System.State"], Is.EqualTo("Resolved"));
        });

        devOpsService.Verify(service => service.FindPackageWorkItemIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetPackageWorkItemReturnsErrorWhenWorkItemIdDoesNotMatchResolvedMetadata()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);

        var result = await tool.GetPackageWorkItem("azure-storage-blob", "12.30", "Python", 40000, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("does not match"));
            Assert.That(result.ResponseError, Does.Contain("40000"));
            Assert.That(result.ResponseError, Does.Contain("31370"));
        });
    }

    [Test]
    public async Task UpdatePackageWorkItemReturnsErrorWhenWorkItemIdDoesNotMatchResolvedMetadata()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);

        var result = await tool.UpdatePackageWorkItem("azure-storage-blob", "12.30", "Python", new Dictionary<string, string>
        {
            ["System.State"] = "Resolved"
        }, 40000, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("does not match"));
        });

        devOpsService.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetPackageWorkItemReturnsErrorForPartialMetadataWhenWorkItemIdProvided()
    {
        var result = await tool.GetPackageWorkItem("azure-storage-blob", null, "Python", 31370, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("must provide all"));
        });
    }

    [Test]
    public async Task GetPackageWorkItemReturnsErrorWhenNoIdentifierProvided()
    {
        var result = await tool.GetPackageWorkItem(null, null, null, workItemId: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Provide either --work-item-id only"));
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

        parseResult = getCommand.Parse("--package-name azure-storage-blob --package-version 12.30.1-beta.1 --language Python", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        parseResult = getCommand.Parse("--work-item-id 31370", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        parseConfig = new CommandLineConfiguration(updateCommand)
        {
            ResponseFileTokenReplacer = null
        };
        parseResult = updateCommand.Parse("--package-name azure-storage-blob --package-version 12.30 --language Python --field System.State=Resolved --field Custom.APIReviewStatus=Approved", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        parseResult = updateCommand.Parse("--work-item-id 31370 --field System.State=Resolved", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        parseResult = updateCommand.Parse("--work-item-id 31370 --field System.State=Resolved --multiline-fields-format Custom.PendingAPIReviews=markdown", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);
    }

    [Test]
    public async Task HandleCommandDoesNotReadUpdateOnlyOptionsForGetWorkItem()
    {
        devOpsService.Setup(service => service.GetWorkItemsByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 31370 })), 200, WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python")]);

        var getCommand = tool.GetCommandInstances().Single(command => command.Name == "get-work-item");
        var parseConfig = new CommandLineConfiguration(getCommand)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = getCommand.Parse("--work-item-id 31370", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        var result = await tool.HandleCommand(parseResult, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.ResponseError, Is.Null);
        });
    }

    [Test]
    public async Task HandleCommandReturnsStructuredErrorForInvalidFieldPatch()
    {
        var updateCommand = tool.GetCommandInstances().Single(command => command.Name == "update-work-item");
        var parseConfig = new CommandLineConfiguration(updateCommand)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = updateCommand.Parse("--package-name azure-storage-blob --package-version 12.30 --language Python --field System.State", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        var result = await tool.HandleCommand(parseResult, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Invalid field patch"));
        });
    }

    [Test]
    public async Task HandleCommandUnescapesNewlineInFieldPatchValues()
    {
        devOpsService.Setup(service => service.UpdateWorkItemAsync(31370, It.Is<Dictionary<string, string>>(fields =>
            fields["Custom.PendingAPIReviews"] == "Test\nTest3"), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python"));

        var updateCommand = tool.GetCommandInstances().Single(command => command.Name == "update-work-item");
        var parseConfig = new CommandLineConfiguration(updateCommand)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = updateCommand.Parse("--work-item-id 31370 --field Custom.PendingAPIReviews=Test\\nTest3", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        var result = await tool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
    }

    [Test]
    public async Task HandleCommandPreservesTrailingBackslashInFieldPatchValues()
    {
        devOpsService.Setup(service => service.UpdateWorkItemAsync(31370, It.Is<Dictionary<string, string>>(fields =>
            fields["Custom.PendingAPIReviews"] == "Test\\"), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python"));

        var updateCommand = tool.GetCommandInstances().Single(command => command.Name == "update-work-item");
        var parseConfig = new CommandLineConfiguration(updateCommand)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = updateCommand.Parse("--work-item-id 31370 --field Custom.PendingAPIReviews=Test\\", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        var result = await tool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
    }

    [Test]
    public async Task HandleCommandPassesMultilineFieldFormatsToService()
    {
        devOpsService.Setup(service => service.UpdateWorkItemAsync(
                31370,
                It.Is<Dictionary<string, string>>(fields => fields["Custom.PendingAPIReviews"] == "Test\nTest3"),
            It.Is<Dictionary<string, string>>(formats => formats["Custom.PendingAPIReviews"] == "Markdown"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePackageWorkItem(31370, "azure-storage-blob", "12.30", "Python"));

        var updateCommand = tool.GetCommandInstances().Single(command => command.Name == "update-work-item");
        var parseConfig = new CommandLineConfiguration(updateCommand)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = updateCommand.Parse("--work-item-id 31370 --field Custom.PendingAPIReviews=Test\\nTest3 --multiline-fields-format Custom.PendingAPIReviews=markdown", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        var result = await tool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
    }

    [Test]
    public async Task HandleCommandReturnsErrorForInvalidMultilineFieldFormat()
    {
        var updateCommand = tool.GetCommandInstances().Single(command => command.Name == "update-work-item");
        var parseConfig = new CommandLineConfiguration(updateCommand)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = updateCommand.Parse("--work-item-id 31370 --field System.State=Resolved --multiline-fields-format Custom.PendingAPIReviews=foo", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        var result = await tool.HandleCommand(parseResult, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Invalid multiline field format"));
        });
    }

    private static WorkItem CreatePackageWorkItem(
        int id,
        string packageName,
        string packageVersionMajorMinor,
        string language,
        string state = "Active",
        string pendingApiReviews = "",
        string specProjectPath = "")
    {
        return new WorkItem
        {
            Id = id,
            Rev = 5,
            Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{id}",
            Fields = new Dictionary<string, object>
            {
                ["Custom.Package"] = packageName,
                ["Custom.PackageVersion"] = packageVersionMajorMinor,
                ["Custom.Language"] = language,
                ["System.State"] = state,
                ["Custom.PackageDisplayName"] = packageName,
                ["Custom.PackageType"] = "client",
                ["Custom.PendingAPIReviews"] = pendingApiReviews,
                ["Custom.SpecProjectPath"] = specProjectPath,
            },
            Relations =
            [
                new WorkItemRelation
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = "https://dev.azure.com/org/project/_apis/wit/workItems/12345"
                }
            ]
        };
    }
}