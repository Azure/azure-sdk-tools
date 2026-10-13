using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ApiReviewHub;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ApiReviewHub;

[TestFixture]
public class ApiReviewHubToolTests
{
    [Test]
    public void CreateCommand_AllowsOmittedPackageType()
    {
        var tool = new ApiReviewHubTool(
            Mock.Of<IApiReviewHubService>(),
            new TestLogger<ApiReviewHubTool>());
        var command = tool.GetCommandInstances().Single();

        var parseResult = command.Parse(
            "--language python --package-name azure-test --target-branch feature");

        Assert.That(parseResult.Errors, Is.Empty);
    }

    [TestCase("mgmt", "mgmt")]
    [TestCase("client", "client")]
    [TestCase("spring", "client")]
    [TestCase("functions", "client")]
    public async Task RequestReviewPullRequest_NormalizesPackageType(string packageType, string expectedPackageType)
    {
        ReviewPullRequestCreationRequest? capturedRequest = null;
        var service = new Mock<IApiReviewHubService>();
        service
            .Setup(x => x.RequestReviewPullRequestAsync(
                It.IsAny<ReviewPullRequestCreationRequest>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback<ReviewPullRequestCreationRequest, string, bool, TimeSpan, CancellationToken>(
                (request, _, _, _, _) => capturedRequest = request)
            .ReturnsAsync(new OperationStatus { Status = "succeeded" });
        var tool = new ApiReviewHubTool(service.Object, new TestLogger<ApiReviewHubTool>());

        await tool.RequestReviewPullRequest(
            "python",
            "azure-test",
            "Azure",
            "azure-sdk-for-python",
            "feature",
            packageType);

        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.PackageType, Is.EqualTo(expectedPackageType));
    }

    [Test]
    public async Task RequestReviewPullRequest_OmitsPackageTypeWhenNotProvided()
    {
        ReviewPullRequestCreationRequest? capturedRequest = null;
        var service = new Mock<IApiReviewHubService>();
        service
            .Setup(x => x.RequestReviewPullRequestAsync(
                It.IsAny<ReviewPullRequestCreationRequest>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback<ReviewPullRequestCreationRequest, string, bool, TimeSpan, CancellationToken>(
                (request, _, _, _, _) => capturedRequest = request)
            .ReturnsAsync(new OperationStatus { Status = "succeeded" });
        var tool = new ApiReviewHubTool(service.Object, new TestLogger<ApiReviewHubTool>());

        await tool.RequestReviewPullRequest(
            "python",
            "azure-test",
            "Azure",
            "azure-sdk-for-python",
            "feature");

        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.PackageType, Is.Null);
    }

    [Test]
    public async Task RequestReviewPullRequest_RejectsUnsupportedPackageType()
    {
        var service = new Mock<IApiReviewHubService>();
        var tool = new ApiReviewHubTool(service.Object, new TestLogger<ApiReviewHubTool>());

        var response = await tool.RequestReviewPullRequest(
            "python",
            "azure-test",
            "Azure",
            "azure-sdk-for-python",
            "feature",
            "unsupported");

        Assert.That(response.ResponseError, Does.Contain("Unsupported package type 'unsupported'"));
        service.Verify(x => x.RequestReviewPullRequestAsync(
            It.IsAny<ReviewPullRequestCreationRequest>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
