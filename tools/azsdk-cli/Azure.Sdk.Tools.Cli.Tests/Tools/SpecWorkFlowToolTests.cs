using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.Azure.Pipelines.WebApi;
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
        private Mock<IOutputHelper> mockOutputService;
        private Mock<IGitHubService> mockGitHubService;
        private Mock<IGitHelper> mockGitHelper;
        private Mock<ITypeSpecHelper> mockTypeSpecHelper;
        private ILogger<SpecWorkflowTool> logger;
        private SpecWorkflowTool specWorkflowTool;

        [SetUp]
        public void Setup()
        {
            mockDevOpsService = new Mock<IDevOpsService>();
            mockOutputService = new Mock<IOutputHelper>();
            mockGitHubService = new Mock<IGitHubService>();
            mockGitHelper = new Mock<IGitHelper>();
            mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            logger = new TestLogger<SpecWorkflowTool>();

            mockOutputService.Setup(x => x.Format(It.IsAny<GenericResponse>()))
                           .Returns((GenericResponse r) => string.Join(", ", r.Details));
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
                mockOutputService.Object,
                logger
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

            Assert.That(result, Does.Contain("does not have a package name specified for python"));
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

            Assert.That(result, Does.Contain("does not have a language specified"));

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

            Assert.That(resultEmptyLanguage, Does.Contain("does not have a language specified"));
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

            Assert.That(result, Does.Contain("SDK details are not present in the release plan"));
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

            mockDevOpsService.Setup(x => x.RunSDKGenerationPipelineAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
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
            Assert.That(result, Does.Contain("Azure DevOps pipeline https://dev.azure.com/azure-sdk/internal/_build/results?buildId=100 has been initiated to generate the SDK. Build ID is 100"));
        }

        [Test]
        public async Task GenerateSdk_Without_pr_and_workitem()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.Setup(x => x.RunSDKGenerationPipelineAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
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
            Assert.That(result, Does.Contain("Azure DevOps pipeline https://dev.azure.com/azure-sdk/internal/_build/results?buildId=100 has been initiated to generate the SDK. Build ID is 100"));
        }

        [Test]
        public async Task GenerateSdk_With_pr_and_no_workitem()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.Setup(x => x.RunSDKGenerationPipelineAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
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
            Assert.That(result, Does.Contain("Azure DevOps pipeline https://dev.azure.com/azure-sdk/internal/_build/results?buildId=100 has been initiated to generate the SDK. Build ID is 100"));
        }

        #region CheckApiReadyForSDKGeneration Tests - Reproducing the merged PR status bug

        [Test]
        public async Task CheckApiReadyForSDKGeneration_WhenPRIsMerged_ShouldUpdateStatusToApproved()
        {
            // Arrange - Create a merged PR by using exact same signature as working test
            var mergedPr = new Octokit.PullRequest(
                123, null, null, null, null, null, null, null, 123, ItemState.Closed,
                "Test PR", "Test body", DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now, DateTimeOffset.Now,
                null, null, null, null, null, null, true, // Changed from false to true for merged
                null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, null, null);

            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);
            mockTypeSpecHelper.Setup(x => x.GetSpecRepoRootPath(It.IsAny<string>())).Returns("/test/repo");
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                           .ReturnsAsync(mergedPr);

            // Act
            var result = await specWorkflowTool.CheckApiReadyForSDKGeneration("/test/typespec", 123, 456);

            // Debug: Let's check the actual PR properties to see if merged is set correctly
            Console.WriteLine($"PR State: {mergedPr.State}");
            Console.WriteLine($"PR Merged: {mergedPr.Merged}");

            // Now this should work if the constructor signature is correct
            Assert.That(result, Does.Contain("Your API spec changes are ready to generate SDK"));
            
            // Verify UpdateApiSpecStatusAsync was called with "Approved"
            mockDevOpsService.Verify(x => x.UpdateApiSpecStatusAsync(456, "Approved"), Times.Once);
        }

        [Test]
        public async Task CheckApiReadyForSDKGeneration_WhenPRIsMergedButNoWorkItemId_ShouldNotUpdateStatus()
        {
            // Arrange - Setup a merged PR but no work item ID
            var mergedPr = new Octokit.PullRequest(
                123, null, null, null, null, null, null, null, 123, ItemState.Closed,
                "Test PR", "Test body", DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now, DateTimeOffset.Now,
                null, null, null, null, null, null, true, // merged = true
                null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, null, null);


            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);
            mockTypeSpecHelper.Setup(x => x.GetSpecRepoRootPath(It.IsAny<string>())).Returns("/test/repo");
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                           .ReturnsAsync(mergedPr);

            // Act - No work item ID provided
            var result = await specWorkflowTool.CheckApiReadyForSDKGeneration("/test/typespec", 123, 0);

            // Assert
            Assert.That(result, Does.Contain("Your API spec changes are ready to generate SDK"));
            
            // Verify UpdateApiSpecStatusAsync was NOT called since work item ID is 0
            mockDevOpsService.Verify(x => x.UpdateApiSpecStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task CheckApiReadyForSDKGeneration_WhenPRIsOpenWithRequiredLabel_ShouldUpdateStatus()
        {
            // Arrange - Setup an open PR with ARM signoff label for management plane
            var labels = new List<Label> {  
                new Label(1, "", "ARMSignedOff", "color", "", "", false) // Add default parameter
            };

            var openPrWithLabel = new Octokit.PullRequest(
                123, null, null, null, null, null, null, null, 123, ItemState.Open,
                "Test PR", "Test body", DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now, DateTimeOffset.Now,
                null, null, null, null, null, null, false, // merged = false
                null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, labels, null);


            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);
            mockTypeSpecHelper.Setup(x => x.GetSpecRepoRootPath(It.IsAny<string>())).Returns("/test/repo");
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(true);
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                           .ReturnsAsync(openPrWithLabel);

            // Act
            var result = await specWorkflowTool.CheckApiReadyForSDKGeneration("/test/typespec", 123, 456);

            // Assert
            Assert.That(result, Does.Contain("Your API spec changes are ready to generate SDK"));
            
            // Verify UpdateApiSpecStatusAsync was called
            mockDevOpsService.Verify(x => x.UpdateApiSpecStatusAsync(456, "Approved"), Times.Once);
        }

        [Test]
        public async Task CheckApiReadyForSDKGeneration_WhenPRIsOpenWithoutRequiredLabel_ShouldNotUpdateStatus()
        {
            // Arrange - Setup an open PR WITHOUT required ARM signoff label for management plane
            var labels = new List<Label> {
                new Label(1, "", "SomeOtherLabel", "color", "", "", false)
            };

            var openPrWithoutLabel = new Octokit.PullRequest(
                123, null, null, null, null, null, null, null, 123, ItemState.Open,
                "Test PR", "Test body", DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now, DateTimeOffset.Now,
                null, null, null, null, null, null, false, // merged = false
                null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, labels, null);


            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);
            mockTypeSpecHelper.Setup(x => x.GetSpecRepoRootPath(It.IsAny<string>())).Returns("/test/repo");
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(true);
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                           .ReturnsAsync(openPrWithoutLabel);

            // Act
            var result = await specWorkflowTool.CheckApiReadyForSDKGeneration("/test/typespec", 123, 456);

            // Assert
            Assert.That(result, Does.Contain("does not have ARM approval"));
            
            // Verify UpdateApiSpecStatusAsync was NOT called
            mockDevOpsService.Verify(x => x.UpdateApiSpecStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task CheckApiReadyForSDKGeneration_WhenPRIsClosedWithoutMerging_ShouldFail()
        {
            // Arrange - Setup a closed PR that was NOT merged
            var closedUnmergedPr = new Octokit.PullRequest(
                123, null, null, null, null, null, null, null, 123, ItemState.Closed,
                "Test PR", "Test body", DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now, DateTimeOffset.Now,
                null, null, null, null, null, null, false, // merged = false but state = closed
                null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, null, null);


            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);
            mockTypeSpecHelper.Setup(x => x.GetSpecRepoRootPath(It.IsAny<string>())).Returns("/test/repo");
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                           .ReturnsAsync(closedUnmergedPr);

            // Act
            var result = await specWorkflowTool.CheckApiReadyForSDKGeneration("/test/typespec", 123, 456);

            // Assert
            Assert.That(result, Does.Contain("closed status without merging changes"));
            
            // Verify UpdateApiSpecStatusAsync was NOT called since PR was not ready
            mockDevOpsService.Verify(x => x.UpdateApiSpecStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        #endregion
    }
}
