// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

public class PackageWorkItemLookupToolTests
{
    private Mock<IDevOpsService> devOpsService;
    private PackageWorkItemLookupTool tool;

    [SetUp]
    public void Setup()
    {
        devOpsService = new Mock<IDevOpsService>();
        tool = new PackageWorkItemLookupTool(devOpsService.Object, new TestLogger<PackageWorkItemLookupTool>());
    }

    [Test]
    public async Task FindPackageWorkItemReturnsIdForSingularMatch()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);

        var result = await tool.FindPackageWorkItem("azure-storage-blob", "12.30", "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.WorkItemId, Is.EqualTo(31370));
        });
    }

    [Test]
    public async Task FindPackageWorkItemReturnsErrorWhenNoMatchExists()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await tool.FindPackageWorkItem("azure-storage-blob", "12.30", "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("No package work item found"));
        });
    }

    [Test]
    public async Task FindPackageWorkItemReturnsErrorWhenMultipleMatchesExist()
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", "12.30", It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2]);

        var result = await tool.FindPackageWorkItem("azure-storage-blob", "12.30", "Python", CancellationToken.None);

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
    public async Task FindPackageWorkItemNormalizesPackageVersionBeforeLookup(string packageVersion, string expectedPackageVersionMajorMinor)
    {
        devOpsService.Setup(service => service.FindPackageWorkItemIdsAsync("azure-storage-blob", "Python", expectedPackageVersionMajorMinor, It.IsAny<CancellationToken>()))
            .ReturnsAsync([31370]);

        var result = await tool.FindPackageWorkItem("azure-storage-blob", packageVersion, "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.PackageVersionMajorMinor, Is.EqualTo(expectedPackageVersionMajorMinor));
        });
    }

    [Test]
    public async Task FindPackageWorkItemReturnsErrorForInvalidPackageVersion()
    {
        var result = await tool.FindPackageWorkItem("azure-storage-blob", "12.x", "Python", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain("Package version must be a major version, major.minor version, or full SemVer version"));
        });
    }

    [Test]
    public void CommandParsesPackageWorkItemLookupOptions()
    {
        var command = tool.GetCommandInstances().First();
        var parseConfig = new CommandLineConfiguration(command)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = command.Parse("--package-name azure-storage-blob --package-version 12.30 --language Python", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);

        parseResult = command.Parse("--package-name azure-storage-blob --package-version-major-minor 12.30 --language Python", parseConfig);
        Assert.That(parseResult.Errors, Is.Empty);
    }
}