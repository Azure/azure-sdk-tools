using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    internal class SpecWorkflowToolTests
    {
        private Mock<IDevOpsService> mockDevOpsService;
        private Mock<IGitHubService> mockGitHubService;
        private Mock<IGitHelper> mockGitHelper;
        private Mock<ITypeSpecHelper> mockTypeSpecHelper;
        private ILogger<SpecWorkflowTool> logger;
        private SpecWorkflowTool specWorkflowTool;
        private IInputSanitizer inputSanitizer;

        [SetUp]
        public void Setup()
        {
            mockDevOpsService = new Mock<IDevOpsService>();
            mockGitHubService = new Mock<IGitHubService>();
            mockGitHelper = new Mock<IGitHelper>();
            mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            logger = new TestLogger<SpecWorkflowTool>();
            inputSanitizer = new InputSanitizer();

            mockGitHelper.Setup(x => x.GetBranchName(It.IsAny<string>()))
                .Returns("testBranch");
            mockTypeSpecHelper.Setup(x => x.IsRepoPathForPublicSpecRepo(It.IsAny<string>()))
                .Returns(true);
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                .Returns(true);

            specWorkflowTool = new SpecWorkflowTool(
                mockGitHubService.Object,
                mockDevOpsService.Object,
                mockGitHelper.Object,
                mockTypeSpecHelper.Object,
                logger,
                inputSanitizer
            );
        }

        [Test]
        public async Task GenerateSDK_WhenPackageNameEmpty()
        {
            var releasePlan = new ReleasePlanDetails
            {
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "python",
                        PackageName = ""
                    }
                }
            };

            mockDevOpsService.Setup(x => x.GetReleasePlanForWorkItemAsync(It.IsAny<int>()))
                           .ReturnsAsync(releasePlan);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "valid/path",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "python",
                pullRequestNumber: 123,
                workItemId: 456
            );

            Assert.That(result.ToString(), Does.Contain("does not have a package name specified for python"));
        }

        [Test]
        public async Task GenerateSDK_WhenLanguageNotInReleasePlan()
        {
            // Test 1: Different language than requested
            var releasePlan = new ReleasePlanDetails
            {
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "java", // Different language than requested
                        PackageName = "com.azure.test"
                    }
                }
            };

            mockDevOpsService.Setup(x => x.GetReleasePlanForWorkItemAsync(It.IsAny<int>()))
                           .ReturnsAsync(releasePlan);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "valid/path",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "python", // Requesting python but release plan has java
                pullRequestNumber: 123,
                workItemId: 456
            );

            Assert.That(result.ToString(), Does.Contain("does not have a language specified"));

            // Test 2: Empty language
            var releasePlanWithEmptyLanguage = new ReleasePlanDetails
            {
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "", // Empty language
                        PackageName = "some-package"
                    }
                }
            };

            mockDevOpsService.Setup(x => x.GetReleasePlanForWorkItemAsync(It.IsAny<int>()))
                           .ReturnsAsync(releasePlanWithEmptyLanguage);

            var resultEmptyLanguage = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "valid/path",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "python",
                pullRequestNumber: 123,
                workItemId: 456
            );

            Assert.That(resultEmptyLanguage.ToString(), Does.Contain("does not have a language specified"));
        }

        [Test]
        public async Task GenerateSDK_WhenSDKInfoListIsEmpty()
        {
            var releasePlan = new ReleasePlanDetails
            {
                SDKInfo = new List<SDKInfo>() // Empty list - no SDK info at all
            };

            mockDevOpsService.Setup(x => x.GetReleasePlanForWorkItemAsync(It.IsAny<int>()))
                           .ReturnsAsync(releasePlan);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "valid/path",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "python",
                pullRequestNumber: 123,
                workItemId: 456
            );

            Assert.That(result.ToString(), Does.Contain("SDK details are not present in the release plan"));
        }

        [Test]
        public async Task GenerateSdk_Uses_WorkItemApi()
        {
            // Test 1: Different language than requested
            var releasePlan = new ReleasePlanDetails
            {
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "Java", // Different language than requested
                        PackageName = "com.azure.test"
                    }
                }
            };

            mockDevOpsService.Setup(x => x.GetReleasePlanAsync(It.IsAny<int>()))
                           .Throws(new Exception("Work item not found"));
            mockDevOpsService.Setup(x => x.GetReleasePlanForWorkItemAsync(It.Is<int>(id => id == 456)))
                .ReturnsAsync(releasePlan);
            mockDevOpsService.Setup(x => x.UpdateApiSpecStatusAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);
            var labels = new List<Label>
            {
               new Label(1, "", SpecWorkflowTool.ARM_SIGN_OFF_LABEL, "", "", "", false)
            };
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(
                new Octokit.PullRequest(123, null, null, null, null, null, null, null, 123, ItemState.Open, null, null, DateTimeOffset.Now, DateTimeOffset.Now, DateTimeOffset.Now, null, null, null, null, null, null, false, null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, labels, null));

            mockDevOpsService.Setup(x => x.RunSDKGenerationPipelineAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new Build()
                {
                    Id = 100,
                    Status = BuildStatus.InProgress,
                });

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                pullRequestNumber: 123,
                workItemId: 456
            );

            Assert.That(result.ToString(), Does.Contain("Azure DevOps pipeline https://dev.azure.com/azure-sdk/internal/_build/results?buildId=100 has been initiated to generate the SDK. Build ID is 100"));
        }

        [Test]
        public async Task GenerateSdk_Without_pr_and_workitem()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.Setup(x => x.RunSDKGenerationPipelineAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new Build()
                {
                    Id = 100,
                    Status = BuildStatus.InProgress,
                });

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java"
            );
            Assert.That(result.ToString(), Does.Contain("Azure DevOps pipeline https://dev.azure.com/azure-sdk/internal/_build/results?buildId=100 has been initiated to generate the SDK. Build ID is 100"));
        }

        [Test]
        public async Task GenerateSdk_With_pr_and_no_workitem()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.Setup(x => x.RunSDKGenerationPipelineAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new Build()
                {
                    Id = 100,
                    Status = BuildStatus.InProgress,
                });
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(
                new Octokit.PullRequest(123, null, null, null, null, null, null, null, 123, ItemState.Open, null, null, DateTimeOffset.Now, DateTimeOffset.Now, DateTimeOffset.Now, null, null, null, null, null, null, false, null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, null, null));


            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                pullRequestNumber: 123
            );
            Assert.That(result.ToString(), Does.Contain("Azure DevOps pipeline https://dev.azure.com/azure-sdk/internal/_build/results?buildId=100 has been initiated to generate the SDK. Build ID is 100"));
        }
    }
}
