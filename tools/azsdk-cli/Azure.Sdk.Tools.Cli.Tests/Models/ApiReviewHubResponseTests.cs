// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class ApiReviewHubResponseTests
{
    [Test]
    public void ToString_IncludesReviewPullRequestUrl_WhenPresent()
    {
        var response = new ApiReviewHubResponse
        {
            Result = new OperationStatus
            {
                OperationId = "op-123",
                Status = "succeeded",
                ReviewPullRequest = ParseJsonElement("""
                    {
                      "url": "https://github.com/Azure/azure-sdk-for-python/pull/13"
                    }
                    """)
            }
        };

        var output = response.ToString();

        Assert.That(output, Does.Contain("Review PR: https://github.com/Azure/azure-sdk-for-python/pull/13"));
        Assert.That(output, Does.Not.Contain("\"operationId\""));
    }

    [Test]
    public void ToString_DoesNotIncludeReviewPullRequestLine_WhenUrlIsMissing()
    {
        var response = new ApiReviewHubResponse
        {
            Result = new OperationStatus
            {
                OperationId = "op-123",
                Status = "succeeded",
                ReviewPullRequest = ParseJsonElement("""
                    {
                      "number": 13
                    }
                    """)
            }
        };

        var output = response.ToString();

        Assert.That(output, Does.Not.Contain("Review PR:"));
        Assert.That(output, Is.Empty);
    }

    [Test]
    public void ToString_IncludesResultMessage_WhenPresent()
    {
        var response = new ApiReviewHubResponse
        {
            Result = new OperationStatus
            {
                OperationId = "op-123",
                Status = "succeeded",
                Message = "An API Review Hub review PR already exists for pkg.",
                ReviewPullRequest = ParseJsonElement("""
                    {
                      "url": "https://github.com/Azure/azure-sdk-for-python/pull/13"
                    }
                    """)
            }
        };

        var output = response.ToString();

        Assert.That(output, Does.Contain("An API Review Hub review PR already exists for pkg."));
        Assert.That(output, Does.Contain("Review PR: https://github.com/Azure/azure-sdk-for-python/pull/13"));
    }

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
