using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class PackageApprovalStatusToolTests
{
    [Test]
    public void GetApprovalStatus_IsOwnedByPackageCommandGroupOnly()
    {
        var apiReviewHubService = Mock.Of<IApiReviewHubService>();
        var releaseStatusService = Mock.Of<IApiReviewReleaseStatusService>();
        var apiReviewHubLogger = new TestLogger<ApiReviewHubTool>();
        var packageTool = new PackageApprovalStatusTool(apiReviewHubService, releaseStatusService, apiReviewHubLogger);
        var apiReviewHubTool = new ApiReviewHubTool(apiReviewHubService, releaseStatusService, apiReviewHubLogger);

        Assert.That(packageTool.CommandHierarchy.Single().Verb, Is.EqualTo("pkg"));
        Assert.That(packageTool.GetCommandInstances().Single().Name, Is.EqualTo("get-approval-status"));
        Assert.That(apiReviewHubTool.GetCommandInstances().Any(command => command.Name == "get-approval-status"), Is.False);
    }
}