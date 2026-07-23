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
            Result = new ApiReviewHubRequestReviewPullRequestResult
            {
                OperationId = "op-123",
                Status = "succeeded",
                Operation = new OperationStatus
                {
                    ReviewPullRequest = ParseJsonElement("""
                        {
                          "url": "https://github.com/Azure/azure-sdk-for-python/pull/13"
                        }
                        """)
                }
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
            Result = new ApiReviewHubRequestReviewPullRequestResult
            {
                OperationId = "op-123",
                Status = "succeeded",
                Operation = new OperationStatus
                {
                    ReviewPullRequest = ParseJsonElement("""
                        {
                          "number": 13
                        }
                        """)
                }
            }
        };

        var output = response.ToString();

        Assert.That(output, Does.Not.Contain("Review PR:"));
        Assert.That(output, Is.Empty);
    }

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
