// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class CommandResponseTests
{
    [Test]
    public void AzureDevOpsAccessDeniedErrorIncludesAccessInstructions()
    {
        var response = new DefaultCommandResponse
        {
            ResponseError = "TF215106: Access denied"
        };

        var output = new OutputHelper(OutputHelper.OutputModes.Mcp).Format(response);
        using var document = JsonDocument.Parse(output);

        Assert.Multiple(() =>
        {
            Assert.That(
                document.RootElement.GetProperty("permission_guidance").GetString(),
                Is.EqualTo(CommandResponse.AzureDevOpsAccessRequiredMessage));
            Assert.That(document.RootElement.TryGetProperty("next_steps", out _), Is.False);
            Assert.That(response.ToString(), Does.Contain(CommandResponse.AzureDevOpsAccessRequiredMessage));
        });
    }

    [Test]
    public void UnrelatedAccessDeniedErrorDoesNotIncludeAccessInstructions()
    {
        var response = new DefaultCommandResponse
        {
            ResponseError = "Access denied to a local file"
        };

        var output = new OutputHelper(OutputHelper.OutputModes.Mcp).Format(response);
        using var document = JsonDocument.Parse(output);

        Assert.Multiple(() =>
        {
            Assert.That(response.PermissionGuidance, Is.Null);
            Assert.That(document.RootElement.TryGetProperty("permission_guidance", out _), Is.False);
        });
    }

    [Test]
    public void CheckPackageResponseIncludesAccessInstructionsInCliOutput()
    {
        var response = new CheckPackageResponse
        {
            ResponseError = "TF215106: Access denied"
        };

        Assert.That(response.ToString(), Does.Contain(CommandResponse.AzureDevOpsAccessRequiredMessage));
    }

    [Test]
    public void PackageMarkReleasedResponseIncludesAccessInstructionsInCliOutput()
    {
        var response = new PackageMarkReleasedResponse
        {
            ResponseErrors = ["TF215106: Access denied"]
        };

        Assert.That(response.ToString(), Does.Contain(CommandResponse.AzureDevOpsAccessRequiredMessage));
    }
}