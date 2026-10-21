// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Mock.Handlers.Verify;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Mock.Tests;

[TestFixture]
public class VerifySetupHandlerTests
{
    [TestCase(null)]
    [TestCase("{}")]
    [TestCase("{\"langs\":[\"Python\"],\"packagePath\":\"sdk/contoso/azure-contoso\"}")]
    public void Handle_AllGoodHasNoUnresolvedRequirements(string? argumentsJson)
    {
        var response = Invoke(argumentsJson);

        Assert.Multiple(() =>
        {
            Assert.That(response.Results, Is.Not.Null.And.Empty);
            Assert.That(response.ExitCode, Is.Zero);
            Assert.That(response.HasBlockingFailures, Is.False);
            Assert.That(response.IsFixable, Is.False);
        });
    }

    [TestCase(null)]
    [TestCase("{}")]
    [TestCase("{\"langs\":[\"Python\"],\"packagePath\":\"sdk/contoso/azure-contoso\"}")]
    public void Handle_WireResponseIsNonBlockingWithoutAutoInstallClaims(string? argumentsJson)
    {
        var response = Invoke(argumentsJson);
        var json = JsonSerializer.Serialize(response);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("results").ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(root.GetProperty("results").GetArrayLength(), Is.Zero);
            Assert.That(root.GetProperty("blocking").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("fixable").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("operation_status").GetString(), Is.EqualTo("Succeeded"));
            Assert.That(json, Does.Not.Contain("autoInstallAttempted").And.Not.Contain("autoInstallSucceeded"));
        });
    }

    private static VerifySetupResponse Invoke(string? argumentsJson)
    {
        var arguments = argumentsJson == null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
        return (VerifySetupResponse)new VerifySetupHandler().Handle(arguments);
    }
}
