using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;
using System.Text.Json;

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

    [TestCase(".net")]
    [TestCase("dotnet")]
    [TestCase("C#")]
    [TestCase("JavaScript")]
    [TestCase("TypeScript")]
    [TestCase("C++")]
    public void GetApprovalStatus_CommandAcceptsLanguageAliases(string language)
    {
        var packageTool = new PackageApprovalStatusTool(
            Mock.Of<IPackageReleaseStatusService>(),
            new TestLogger<PackageApprovalStatusTool>());
        var command = packageTool.GetCommandInstances().Single();

        var parseResult = command.Parse(
            $"--language {language} --package-name azure-test --package-version 1.0.0");

        Assert.That(parseResult.Errors, Is.Empty);
    }

    [Test]
    public void GetApprovalStatus_CommandRejectsUnknownLanguage()
    {
        var packageTool = new PackageApprovalStatusTool(
            Mock.Of<IPackageReleaseStatusService>(),
            new TestLogger<PackageApprovalStatusTool>());
        var command = packageTool.GetCommandInstances().Single();

        var parseResult = command.Parse(
            "--language unknown --package-name azure-test --package-version 1.0.0");

        Assert.That(parseResult.Errors.Single().Message, Does.Contain("Invalid language 'unknown'"));
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
                    IsReleaseApproved = true,
                    IsApiApproved = true,
                    IsPackageNameApproved = true,
                    Reason = "approved",
                    ApiApprovals =
                    [
                        new ApiReviewHubApiApprovalRecord
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

        Assert.That(response.ToString(), Does.Contain("API approval record ID: approval-record-id"));
        Assert.That(response.Result!.ReviewHub.ApiApprovals![0].Id, Is.EqualTo("approval-record-id"));
    }

    [TestCase(".NET", "csharp")]
    [TestCase("C#", "csharp")]
    [TestCase("JavaScript", "js")]
    [TestCase("TypeScript", "js")]
    [TestCase("C++", "cpp")]
    [TestCase("Py", "python")]
    public async Task GetApprovalStatus_NormalizesLanguageAliases(string language, string expectedLanguage)
    {
        var releaseStatusService = new Mock<IPackageReleaseStatusService>();
        releaseStatusService
            .Setup(x => x.GetApprovalStatusAsync(
                It.IsAny<string>(),
                expectedLanguage,
                "azure-test",
                "1.0.0",
                "",
                "",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageReleaseStatusResult());
        var packageTool = new PackageApprovalStatusTool(releaseStatusService.Object, new TestLogger<PackageApprovalStatusTool>());

        await packageTool.GetApprovalStatus(language, "azure-test", "1.0.0");

        releaseStatusService.VerifyAll();
    }

    [TestCase(true, true, true, "approved", 0)]
    [TestCase(true, true, true, "reviewNotRequired", 0)]
    [TestCase(false, true, false, "packageNameNotApproved", 1)]
    [TestCase(false, true, false, "reviewNotRequired", 1)]
    [TestCase(false, false, true, "missingApproval", 1)]
    [TestCase(false, false, true, "rejected", 1)]
    [TestCase(false, false, false, "packageNameNotApproved", 1)]
    [TestCase(false, false, false, "repositoryNotSupported", 1)]
    [TestCase(false, false, false, "unknownPackage", 1)]
    [TestCase(false, true, true, "missingApiHash", 1)]
    public async Task GetApprovalStatus_DisplaysIndependentApprovalsAndUsesOverallExitCode(
        bool isApproved, bool isApiApproved, bool isPackageNameApproved, string reason, int expectedExitCode)
    {
        var result = new PackageReleaseStatusResult
        {
            IsApproved = isApproved,
            FinalSource = "ApiReviewHub",
            Reason = reason,
            ReviewHub = new ApiReviewHubReleaseGateResult
            {
                StatusCode = 200,
                IsReleaseApproved = isApproved,
                IsApiApproved = isApiApproved,
                IsPackageNameApproved = isPackageNameApproved,
                Reason = reason,
                Details = ["API review is optional; package name approval is still required."]
            }
        };
        var releaseStatusService = new Mock<IPackageReleaseStatusService>();
        releaseStatusService.Setup(x => x.GetApprovalStatusAsync(
                It.IsAny<string>(), "python", "azure-test", "1.0.0", "hash", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var tool = new PackageApprovalStatusTool(releaseStatusService.Object, new TestLogger<PackageApprovalStatusTool>());

        var response = await tool.HandleCommand(tool.GetCommandInstances().Single().Parse(
            "--language python --package-name azure-test --package-version 1.0.0 --api-hash hash"), CancellationToken.None);
        var text = response.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(response.ExitCode, Is.EqualTo(expectedExitCode));
            Assert.That(response.OperationStatus, Is.EqualTo(isApproved ? Status.Succeeded : Status.Failed));
            Assert.That(text, Does.Contain($"Release Approved: {isApproved}"));
            Assert.That(text, Does.Contain($"API Approved: {isApiApproved}"));
            Assert.That(text, Does.Contain($"Package Name Approved: {isPackageNameApproved}"));
            Assert.That(text, Does.Contain("== Final Result =="));
            Assert.That(text, Does.Contain($"Approved: {isApproved}"));
            Assert.That(text, Does.Contain(result.ReviewHub.Details[0]));
            Assert.That(response.ResponseError, isApproved ? Is.Null : Is.Not.Null);
            if (reason == "packageNameNotApproved")
            {
                Assert.That(response.ResponseError, Does.Contain("package name is not approved"));
            }
        });
    }

    [TestCase(OutputHelper.OutputModes.Json, true)]
    [TestCase(OutputHelper.OutputModes.Json, false)]
    [TestCase(OutputHelper.OutputModes.Mcp, true)]
    [TestCase(OutputHelper.OutputModes.Mcp, false)]
    public async Task GetApprovalStatus_PreservesStructuredContract(OutputHelper.OutputModes mode, bool isApproved)
    {
        var result = new PackageReleaseStatusResult
        {
            IsApproved = isApproved,
            FinalSource = "ApiReviewHub",
            Reason = isApproved ? "approved" : "packageNameNotApproved",
            ReviewHub = new ApiReviewHubReleaseGateResult
            {
                StatusCode = 200,
                IsReleaseApproved = isApproved,
                IsApiApproved = true,
                IsPackageNameApproved = isApproved,
                Reason = isApproved ? "approved" : "packageNameNotApproved",
                AppliedInheritanceRule = "prereleaseToStable",
                Details = ["Inherited API approval."],
                ApiApprovals =
                [
                    new ApiReviewHubApiApprovalRecord
                    {
                        Id = "approval-id",
                        ApiHash = "hash",
                        Version = "1.0.0b1",
                        Status = "approved",
                        CommitSha = "commit",
                        PullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/1",
                        LastUpdatedBy = "reviewer",
                        LastUpdatedOn = "2026-07-27T00:00:00Z"
                    }
                ]
            }
        };
        var releaseStatusService = new Mock<IPackageReleaseStatusService>();
        releaseStatusService.Setup(x => x.GetApprovalStatusAsync(
                It.IsAny<string>(), "python", "azure-test", "1.0.0", "hash", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var tool = new PackageApprovalStatusTool(releaseStatusService.Object, new TestLogger<PackageApprovalStatusTool>());
        PackageReleaseStatusResponse response;
        if (mode == OutputHelper.OutputModes.Json)
        {
            var command = tool.GetCommandInstances().Single();
            command.Options.Add(SharedOptions.Format);
            response = (PackageReleaseStatusResponse)await tool.HandleCommand(command.Parse(
                "--language python --package-name azure-test --package-version 1.0.0 --api-hash hash --output json"), CancellationToken.None);
        }
        else
        {
            response = await tool.GetApprovalStatus("python", "azure-test", "1.0.0", "hash");
            Assert.That(response.ToString(), Does.Contain("Applied Inheritance Rule: prereleaseToStable"));
            Assert.That(response.ToString(), Does.Contain("hash [provided hash] (version 1.0.0b1)"));
            Assert.That(response.ToString(), Does.Contain("Updated by: reviewer on 2026-07-27T00:00:00Z"));
            Assert.That(response.ToString(), Does.Contain("Pull request: https://github.com/Azure/azure-sdk-for-python/pull/1"));
        }

        using var json = JsonDocument.Parse(new OutputHelper(mode).Format(response));
        var structuredResult = json.RootElement.GetProperty("result");
        var reviewHub = structuredResult.GetProperty("reviewHub");
        var evidence = reviewHub.GetProperty("apiApprovals")[0];

        Assert.Multiple(() =>
        {
            Assert.That(structuredResult.GetProperty("isApproved").GetBoolean(), Is.EqualTo(isApproved));
            Assert.That(reviewHub.GetProperty("isApproved").GetBoolean(), Is.EqualTo(isApproved));
            Assert.That(reviewHub.TryGetProperty("isReleaseApproved", out _), Is.False);
            Assert.That(reviewHub.GetProperty("isApiApproved").GetBoolean(), Is.True);
            Assert.That(reviewHub.GetProperty("isPackageNameApproved").GetBoolean(), Is.EqualTo(isApproved));
            Assert.That(reviewHub.GetProperty("reason").GetString(), Is.EqualTo(result.Reason));
            Assert.That(reviewHub.GetProperty("appliedInheritanceRule").GetString(), Is.EqualTo("prereleaseToStable"));
            Assert.That(reviewHub.GetProperty("details")[0].GetString(), Is.EqualTo("Inherited API approval."));
            Assert.That(evidence.GetProperty("id").GetString(), Is.EqualTo("approval-id"));
            Assert.That(evidence.GetProperty("apiHash").GetString(), Is.EqualTo("hash"));
            Assert.That(evidence.GetProperty("version").GetString(), Is.EqualTo("1.0.0b1"));
            Assert.That(evidence.GetProperty("commitSha").GetString(), Is.EqualTo("commit"));
            Assert.That(evidence.GetProperty("status").GetString(), Is.EqualTo("approved"));
            Assert.That(reviewHub.TryGetProperty("approvals", out _), Is.False);
            Assert.That(json.RootElement.TryGetProperty("details", out _), Is.EqualTo(mode == OutputHelper.OutputModes.Mcp));
            Assert.That(json.RootElement.GetProperty("operation_status").GetString(), Is.EqualTo(isApproved ? "Succeeded" : "Failed"));
            Assert.That(response.ExitCode, Is.EqualTo(isApproved ? 0 : 1));
        });
    }
}