using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class PackageApprovalStatusToolTests
{
    [Test]
    public void GetApprovalStatus_IsOwnedByPackageCommandGroupOnly()
    {
        var releaseStatusService = Mock.Of<IPackageReleaseStatusService>();
        var packageTool = new PackageApprovalStatusTool(releaseStatusService, new TestLogger<PackageApprovalStatusTool>());

        Assert.That(packageTool.CommandHierarchy.Single().Verb, Is.EqualTo("pkg"));
        Assert.That(packageTool.GetCommandInstances().Single().Name, Is.EqualTo("get-approval-status"));
    }
}