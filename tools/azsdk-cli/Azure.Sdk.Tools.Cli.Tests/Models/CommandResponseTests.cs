// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

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
                document.RootElement.GetProperty("next_steps")[0].GetString(),
                Is.EqualTo(CommandResponse.AzureDevOpsAccessRequiredMessage));
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

        Assert.That(response.NextSteps, Is.Null);
    }
}