using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    [TestFixture]
    internal class SpecWorkflowToolTests
    {
        private MockDevOpsService mockDevOpsService;
        private Mock<IGitHubService> mockGitHubService;
        private Mock<ITypeSpecHelper> mockTypeSpecHelper;
        private ILogger<SpecWorkflowTool> logger;
        private SpecWorkflowTool specWorkflowTool;
        private IInputSanitizer inputSanitizer;

        [SetUp]
        public void Setup()
        {
            mockDevOpsService = new MockDevOpsService();
            mockGitHubService = new Mock<IGitHubService>();
            mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            logger = new TestLogger<SpecWorkflowTool>();
            inputSanitizer = new InputSanitizer();

            mockTypeSpecHelper.Setup(x => x.IsRepoPathForPublicSpecRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                .Returns(true);

            specWorkflowTool = new SpecWorkflowTool(
                mockGitHubService.Object,
                mockDevOpsService,
                mockTypeSpecHelper.Object,
                logger,
                inputSanitizer
            );
        }

        [Test]
        public async Task GenerateSDK_WhenPackageNameEmpty()
        {
            var releasePlan = new ReleasePlanWorkItem
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

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

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
            var releasePlan = new ReleasePlanWorkItem
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

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

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
            var releasePlanWithEmptyLanguage = new ReleasePlanWorkItem
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

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlanWithEmptyLanguage;

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
            var releasePlan = new ReleasePlanWorkItem
            {
                SDKInfo = new List<SDKInfo>() // Empty list - no SDK info at all
            };

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

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
            var releasePlan = new ReleasePlanWorkItem
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

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;
            mockDevOpsService.ConfiguredRunSDKGenerationPipeline = new Build()
            {
                Id = 100,
                Status = BuildStatus.InProgress,
            };

            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);
            var labels = new List<Label>
            {
               new Label(1, "", SpecWorkflowTool.ARM_SIGN_OFF_LABEL, "", "", "", false)
            };
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                new Octokit.PullRequest(123, null, null, null, null, null, null, null, 123, ItemState.Open, null, null, DateTimeOffset.Now, DateTimeOffset.Now, DateTimeOffset.Now, null, null, null, null, null, null, false, null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, labels, null));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                pullRequestNumber: 123,
                workItemId: 456
            );

            Assert.That(result.TypeSpecProject, Is.EqualTo("specification/testcontoso/Contoso.Management"));
            Assert.That(result.ToString(), Does.Contain("Azure DevOps pipeline https://dev.azure.com/azure-sdk/internal/_build/results?buildId=100 has been initiated to generate the SDK. Build ID is 100"));
        }

        [TestCase("Java", "Java", "Released", false, "Completed")]
        [TestCase("Java", "Java", "Released", true, "Completed")]
        [TestCase("java", "java", "RELEASED", false, "Completed")]
        [TestCase("Python", "Python", "released", false, "Completed")]
        [TestCase("typescript", "JavaScript", "Released", false, "Completed")]
        [TestCase("dotnet", ".NET", "Released", false, "Completed")]
        [TestCase("csharp", ".net", "Released", false, "Completed")]
        [TestCase("Go", "Go", "Released", false, "")]
        [TestCase("Java", "Java", "Released", true, "In progress")]
        [TestCase("Java", "Java", "Released", true, "Pending")]
        public async Task GenerateSDK_WhenRequestedSdkAlreadyReleased_SkipsNewRun(
            string language, string releasePlanLanguage, string releaseStatus, bool hasSdkPullRequest, string generationStatus)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                ReleasePlanId = 1234,
                SDKInfo =
                [
                    new SDKInfo
                    {
                        Language = releasePlanLanguage,
                        PackageName = "azure-test",
                        ReleaseStatus = releaseStatus,
                        GenerationStatus = generationStatus,
                        SdkPullRequestUrl = hasSdkPullRequest ? "https://github.com/Azure/azure-sdk-for-java/pull/456" : ""
                    }
                ]
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: language,
                pullRequestNumber: hasSdkPullRequest ? 123 : 0,
                workItemId: 1234
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo("Success"));
                Assert.That(result.ExitCode, Is.Zero);
                Assert.That(result.ResponseErrors, Is.Empty);
                Assert.That(result.Language, Is.EqualTo(SdkLanguageHelpers.GetSdkLanguage(language)));
                Assert.That(result.Details, Has.Some.Contains("has already been released for release plan work item 456"));
                Assert.That(result.Details, Has.Some.Contains("A new SDK generation run was not triggered"));
            });
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            devOpsService.Verify(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Not started")]
        [TestCase("In progress")]
        [TestCase("Pending")]
        [TestCase("Failed")]
        public async Task GenerateSDK_WhenOnlyAnotherLanguageIsReleased_AllowsGeneration(string? releaseStatus)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SDKInfo =
                [
                    new SDKInfo
                    {
                        Language = "Python",
                        PackageName = "azure-test-python",
                        ReleaseStatus = "Released"
                    },
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test",
                        ReleaseStatus = releaseStatus!,
                        GenerationStatus = "Completed"
                    }
                ]
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                "main", "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase("In progress")]
        [TestCase("Pending")]
        [TestCase("PENDING")]
        public async Task GenerateSDK_WhenGenerationAlreadyInProgress_SkipsNewRun(string generationStatus)
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.ConfiguredRunSDKGenerationPipeline = new Build()
            {
                Id = 100,
                Status = BuildStatus.InProgress,
            };

            var releasePlan = new ReleasePlanWorkItem
            {
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test",
                        GenerationStatus = generationStatus,
                        GenerationPipelineUrl = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=99"
                    }
                }
            };
            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ToString(), Does.Contain("was not triggered to avoid duplicate generation"));
            Assert.That(result.ToString(), Does.Contain("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=99"));
            // A new generation run should not have been triggered.
            Assert.That(result.ToString(), Does.Not.Contain("has been initiated to generate the SDK"));
        }

        [TestCase("In progress")]
        [TestCase("Pending")]
        public async Task GenerateSDK_WhenActiveStatusHasNoPipelineUrl_RetriesGeneration(string generationStatus)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SDKInfo =
                [
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test",
                        GenerationStatus = generationStatus,
                    }
                ]
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                "main", "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task GenerateSDK_WhenAnotherActiveReleasePlanHasSdkPullRequest_BlocksGeneration()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.ConfiguredRunSDKGenerationPipeline = new Build()
            {
                Id = 100,
                Status = BuildStatus.InProgress,
            };

            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test"
                    }
                }
            };
            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

            // Another active release plan for the same TypeSpec project that already has an SDK pull request.
            mockDevOpsService.ConfiguredActiveReleasePlansForTypeSpecPath = new List<ReleasePlanWorkItem>
            {
                releasePlan,
                new ReleasePlanWorkItem
                {
                    WorkItemId = 999,
                    ReleasePlanId = 999,
                    SDKInfo = new List<SDKInfo>
                    {
                        new SDKInfo
                        {
                            Language = "Java",
                            PackageName = "azure-test",
                            SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/123",
                            ReleaseStatus = "In progress"
                        }
                    }
                }
            };

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ToString(), Does.Contain("Another active release plan"));
            Assert.That(result.ToString(), Does.Contain("999"));
            Assert.That(result.ToString(), Does.Not.Contain("has been initiated to generate the SDK"));
        }

        [Test]
        public async Task GenerateSDK_WhenAnotherReleasePlanSdkPullRequestIsReleased_AllowsGeneration()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.ConfiguredRunSDKGenerationPipeline = new Build()
            {
                Id = 100,
                Status = BuildStatus.InProgress,
            };

            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test"
                    }
                }
            };
            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

            // Another active release plan whose SDK pull request has already been released should not block generation.
            mockDevOpsService.ConfiguredActiveReleasePlansForTypeSpecPath = new List<ReleasePlanWorkItem>
            {
                releasePlan,
                new ReleasePlanWorkItem
                {
                    WorkItemId = 999,
                    ReleasePlanId = 999,
                    SDKInfo = new List<SDKInfo>
                    {
                        new SDKInfo
                        {
                            Language = "Java",
                            PackageName = "azure-test",
                            SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/123",
                            ReleaseStatus = "Released"
                        }
                    }
                }
            };

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ToString(), Does.Contain("has been initiated to generate the SDK"));
        }

        [Test]
        public async Task GenerateSDK_WhenCurrentReleasePlanHasSdkPullRequestForSameLanguage_AllowsRegeneration()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.ConfiguredRunSDKGenerationPipeline = new Build()
            {
                Id = 100,
                Status = BuildStatus.InProgress,
            };

            // Current release plan already has an SDK pull request for the requested language (regeneration scenario).
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test",
                        SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/456"
                    }
                }
            };
            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

            // Another active release plan with an unreleased SDK pull request also exists, but regeneration of the
            // current release plan should still be allowed because it already has its own SDK pull request.
            mockDevOpsService.ConfiguredActiveReleasePlansForTypeSpecPath = new List<ReleasePlanWorkItem>
            {
                releasePlan,
                new ReleasePlanWorkItem
                {
                    WorkItemId = 999,
                    ReleasePlanId = 999,
                    SDKInfo = new List<SDKInfo>
                    {
                        new SDKInfo
                        {
                            Language = "Java",
                            PackageName = "azure-test",
                            SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/123",
                            ReleaseStatus = "In progress"
                        }
                    }
                }
            };

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ToString(), Does.Contain("has been initiated to generate the SDK"));
        }

        [Test]
        public async Task GenerateSdk_Without_pr_and_workitem()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.ConfiguredRunSDKGenerationPipeline = new Build()
            {
                Id = 100,
                Status = BuildStatus.InProgress,
            };

            var releasePlan = new ReleasePlanWorkItem
            {
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test"
                    }
                }
            };
            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );
            Assert.That(result.ToString(), Does.Contain("Azure DevOps pipeline https://dev.azure.com/azure-sdk/internal/_build/results?buildId=100 has been initiated to generate the SDK. Build ID is 100"));
        }

        [TestCase("2024-01-01-preview", "2024-01-01")]
        [TestCase("", "2024-01-01-preview")]
        [TestCase("none", "2024-01-01-preview")]
        public async Task GenerateSdk_BlocksStableSdkForPreviewApiVersion(string apiVersion, string releasePlanApiVersion)
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = new ReleasePlanWorkItem
            {
                SpecAPIVersion = releasePlanApiVersion,
                SDKInfo =
                [
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test"
                    }
                ]
            };

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: apiVersion,
                sdkReleaseType: "stable",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("Stable SDK generation is not allowed from preview API version"));
        }

        [TestCase("")]
        [TestCase("none")]
        public async Task GenerateSdk_AllowsBetaSdkForPreviewApiVersion(string apiVersion)
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockDevOpsService.ConfiguredRunSDKGenerationPipeline = new Build
            {
                Id = 100,
                Status = BuildStatus.InProgress,
            };
            mockDevOpsService.ConfiguredReleasePlanForWorkItem = new ReleasePlanWorkItem
            {
                SpecAPIVersion = "2024-01-01-preview",
                SDKInfo =
                [
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test"
                    }
                ]
            };

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: apiVersion,
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ToString(), Does.Contain("Azure DevOps pipeline"));
        }

        [Test]
        public async Task GenerateSdk_With_pr_and_no_workitem()
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>()))
                .Returns(true);

            mockDevOpsService.ConfiguredRunSDKGenerationPipeline = new Build()
            {
                Id = 100,
                Status = BuildStatus.InProgress,
            };

            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                new Octokit.PullRequest(123, null, null, null, null, null, null, null, 123, ItemState.Open, null, null, DateTimeOffset.Now, DateTimeOffset.Now, DateTimeOffset.Now, null, null, null, null, null, null, false, null, null, null, null, 0, 1, 1, 1, 1, null, false, null, null, null, null, null));

            var releasePlan = new ReleasePlanWorkItem
            {
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test"
                    }
                }
            };
            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

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
        public async Task GetSDKPullRequestDetails_WithInvalidLanguage_ReturnsError()
        {
            var result = await specWorkflowTool.GetSDKPullRequestDetails("InvalidLanguage", workItemId: 123, buildId: 456);
            
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Unsupported language"));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Unknown));
        }

        [Test]
        public async Task GetSDKPullRequestDetails_WithNoBuildIdOrWorkItemId_ReturnsError()
        {
            var result = await specWorkflowTool.GetSDKPullRequestDetails("Python", workItemId: 0, buildId: 0);
            
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Either build ID or release plan work item ID is required"));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task GetSDKPullRequestDetails_WithWorkItemId_ButNoSDKInfo_ReturnsError()
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                SDKInfo = new List<SDKInfo>()
            };

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

            var result = await specWorkflowTool.GetSDKPullRequestDetails("Python", workItemId: 123, buildId: 0);
            
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("No SDK pull request details"));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task GetSDKPullRequestDetails_WithWorkItemId_ButDifferentLanguage_ReturnsError()
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                SDKInfo = new List<SDKInfo>
                {
                    new SDKInfo
                    {
                        Language = "Java",
                        SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/123"
                    }
                }
            };

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = releasePlan;

            var result = await specWorkflowTool.GetSDKPullRequestDetails("Python", workItemId: 123, buildId: 0);
            
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("No SDK pull request details found"));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task GetSDKPullRequestDetails_WithBuildId_PipelineNotFound_ReturnsError()
        {
            mockDevOpsService.ConfiguredPipelineRun = null;

            var result = await specWorkflowTool.GetSDKPullRequestDetails("Python", workItemId: 0, buildId: 456);
            
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Failed to get SDK generation pipeline run"));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task GetSDKPullRequestDetails_WithBuildId_PipelineNotCompleted_ReturnsDetails()
        {
            var build = new Build
            {
                Id = 456,
                Status = BuildStatus.InProgress
            };

            mockDevOpsService.ConfiguredPipelineRun = build;

            var result = await specWorkflowTool.GetSDKPullRequestDetails("Python", workItemId: 0, buildId: 456);
            
            Assert.IsNull(result.ResponseError);
            Assert.That(result.Details, Has.Some.Contains("SDK generation pipeline is not in completed status"));
            Assert.That(result.Details, Has.Some.Contains("InProgress"));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task GetSDKPullRequestDetails_WithBuildId_PipelineFailed_ReturnsError()
        {
            var build = new Build
            {
                Id = 456,
                Status = BuildStatus.Completed,
                Result = BuildResult.Failed
            };

            mockDevOpsService.ConfiguredPipelineRun = build;

            var result = await specWorkflowTool.GetSDKPullRequestDetails("Python", workItemId: 0, buildId: 456);
            
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("SDK generation pipeline did not succeed"));
            Assert.That(result.ResponseError, Does.Contain("Failed"));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task GetSDKPullRequestDetails_WithBuildId_PipelineSucceeded_WithPullRequest_ReturnsDetails()
        {
            var build = new Build
            {
                Id = 456,
                Status = BuildStatus.Completed,
                Result = BuildResult.Succeeded
            };

            mockDevOpsService.ConfiguredPipelineRun = build;
            mockDevOpsService.ConfiguredSDKPullRequest = "https://github.com/Azure/azure-sdk-for-python/pull/789";

            var result = await specWorkflowTool.GetSDKPullRequestDetails("Python", workItemId: 0, buildId: 456);

            Assert.IsNull(result.ResponseError);
            Assert.That(result.Details, Has.Some.Contains("SDK pull request details"));
            Assert.That(result.Details, Has.Some.Contains("https://github.com/Azure/azure-sdk-for-python/pull/789"));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task GenerateSdk_With_Invalid_TypeSpec_Path()
        {
            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "InvalidPath/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );
            Assert.That(result.TypeSpecProject, Is.EqualTo(""));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Java));
            Assert.That(result.ResponseErrors, Does.Contain("Invalid TypeSpec project root path [InvalidPath/specification/testcontoso/Contoso.Management]."));
        }

        private Mock<IDevOpsService> SetupSdkGenerationTool(ReleasePlanWorkItem releasePlan)
        {
            var devOpsService = new Mock<IDevOpsService>();
            devOpsService.Setup(x => x.ResolveReleasePlanByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(releasePlan);
            devOpsService.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());
            devOpsService.Setup(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Build { Id = 100, Status = BuildStatus.InProgress });
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");

            specWorkflowTool = new SpecWorkflowTool(
                mockGitHubService.Object,
                devOpsService.Object,
                mockTypeSpecHelper.Object,
                logger,
                inputSanitizer
            );
            return devOpsService;
        }
    }
}
