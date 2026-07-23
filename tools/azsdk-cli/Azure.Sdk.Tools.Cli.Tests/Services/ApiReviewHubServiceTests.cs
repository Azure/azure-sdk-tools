// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Net;
using System.Text;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Moq.Protected;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ApiReviewHubServiceTests
{
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private Mock<IAzureService> azureServiceMock = null!;
    private ApiReviewHubService service = null!;

    [SetUp]
    public void Setup()
    {
        httpClientFactoryMock = new Mock<IHttpClientFactory>();
        azureServiceMock = new Mock<IAzureService>();

        var credentialMock = new Mock<TokenCredential>();
        credentialMock
            .Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("mock-token", DateTimeOffset.UtcNow.AddHours(1)));

        azureServiceMock
            .Setup(x => x.GetCredential(It.IsAny<string?>()))
            .Returns(credentialMock.Object);

        service = new ApiReviewHubService(httpClientFactoryMock.Object, azureServiceMock.Object, new TestLogger<ApiReviewHubService>());
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_DeserializesArrayDetails()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                                            "isApproved": false,
                      "reason": "rejected",
                                            "details": ["At least one architect has requested changes for this API."],
                      "approvals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(200));
        Assert.That(result.Reason, Is.EqualTo("rejected"));
        Assert.That(result.Details, Is.EqualTo(new[] { "At least one architect has requested changes for this API." }));
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_PreservesActualSuccessStatusCode()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    """
                    {
                      "isApproved": false,
                      "reason": "pending",
                      "details": ["Approval is still pending."],
                      "approvals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(202));
    }

    [Test]
    public void GetReleaseGateStatusAsync_WithDisallowedHost_ThrowsInvalidOperationException()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetReleaseGateStatusAsync("https://api-review-hub.evil.example", "python", "pkg", "1.0.0", "hash", CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("endpoint host is not allowed"));
    }
}
