// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Mock.Handlers.Package;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Mock.Tests;

[TestFixture]
public class ReleaseSdkHandlerTests
{
    [TestCase(false, "Azure.Template.Contoso", -1)]
    [TestCase(true, "Azure.Template.Contoso", -1)]
    [TestCase(false, "unknown-package", int.MinValue)]
    [TestCase(true, "unknown-package", int.MinValue)]
    public void NegativePlanId_FailsBeforeReadinessOrQueue(bool checkReady, string packageName, int releasePlanId)
    {
        var response = new ReleaseSdkHandler().Handle(CreateArguments(checkReady, packageName, releasePlanId));

        Assert.That(response, Is.TypeOf<SdkReleaseResponse>());
        var release = (SdkReleaseResponse)response;
        Assert.Multiple(() =>
        {
            Assert.That(release.PackageName, Is.EqualTo(packageName));
            Assert.That(release.ReleasePipelineStatus, Is.EqualTo("Failed"));
            Assert.That(release.ResponseError, Is.EqualTo("Release plan ID must be a positive integer, or 0 when no release plan is supplied."));
            Assert.That(release.ReleaseStatusDetails, Is.EqualTo(release.ResponseError));
            Assert.That(release.PipelineBuildId, Is.Zero);
            Assert.That(release.ReleasePipelineRunUrl, Is.Null.Or.Empty);
        });
    }

    [TestCase(false, null)]
    [TestCase(true, null)]
    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 35307)]
    [TestCase(true, 35307)]
    public void OmittedZeroOrPositivePlanId_PreservesReadinessAndQueue(bool checkReady, int? releasePlanId)
    {
        var response = (SdkReleaseResponse)new ReleaseSdkHandler().Handle(
            CreateArguments(checkReady, "Azure.Template.Contoso", releasePlanId));

        Assert.Multiple(() =>
        {
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.ReleasePipelineStatus, Is.EqualTo(checkReady ? "Ready" : "Queued"));
            Assert.That(response.PipelineBuildId, Is.EqualTo(checkReady ? 0 : 80001));
        });
    }

    private static Dictionary<string, object?> CreateArguments(bool checkReady, string packageName, int? releasePlanId)
    {
        var values = new Dictionary<string, object?>
        {
            ["packageName"] = packageName,
            ["language"] = ".NET",
            ["checkReady"] = checkReady
        };
        if (releasePlanId.HasValue)
        {
            values["releasePlanId"] = releasePlanId.Value;
        }

        // Mock MCP dispatch supplies JsonElement values, not boxed CLR arguments.
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(values))!;
        return arguments.ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
    }
}
