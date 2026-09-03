using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
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

    [Test]
    public async Task GetApprovalStatus_SurfacesApprovalRecordId()
    {
        var releaseStatusService = new Mock<IPackageReleaseStatusService>();
        releaseStatusService
            .Setup(x => x.GetApprovalStatusAsync(It.IsAny<string>(), "python", "azure-test", "1.0.0", "hash", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageReleaseStatusResult
            {
                IsApproved = true,
                FinalSource = "reviewHub",
                Reason = "approved",
                ReviewHub = new ApiReviewHubReleaseGateResult
                {
                    StatusCode = 200,
                    IsApproved = true,
                    Reason = "approved",
                    Approvals =
                    [
                        new ApiReviewHubApprovalRecord
                        {
                            Id = "approval-record-id",
                            ApiHash = "hash",
                            Version = "1.0.0",
                            Status = "approved"
                        }
                    ]
                }
            });
        var packageTool = new PackageApprovalStatusTool(releaseStatusService.Object, new TestLogger<PackageApprovalStatusTool>());

        var response = await packageTool.GetApprovalStatus("python", "azure-test", "1.0.0", "hash");

        Assert.That(response.ToString(), Does.Contain("Approval record ID: approval-record-id"));
        Assert.That(response.Result!.ReviewHub.Approvals![0].Id, Is.EqualTo("approval-record-id"));
    }
}