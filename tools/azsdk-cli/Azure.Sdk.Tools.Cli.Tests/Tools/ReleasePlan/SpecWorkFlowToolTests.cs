using System.CommandLine;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Moq;
using Octokit;
using PullRequest = Octokit.PullRequest;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    [TestFixture]
    internal class SpecWorkflowToolTests
    {
        private const string PinnedSpecCommit = "0123456789abcdef0123456789abcdef01234567";
        private const string DifferentSpecCommit = "fedcba9876543210fedcba9876543210fedcba98";
        private const string LinkedSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/123";
        private const string RelativeProjectPath = "specification/testcontoso/Contoso.Management";
        private const string PreviousGenerationPipelineUrl = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=99";
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
        public void Commands_ExposeGenerationAndPullRequestLookupOnly()
        {
            Assert.That(specWorkflowTool.GetCommandInstances().Select(command => command.Name),
                Is.EquivalentTo(new[] { "generate-sdk", "get-sdk-pr" }));
        }

        [Test]
        public async Task GenerateSDK_WhenPackageNameEmpty()
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
            Assert.That(mockDevOpsService.LastGenerationAutoRelease, Is.False);
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
                        GenerationPipelineUrl = PreviousGenerationPipelineUrl,
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
            VerifyNoGeneration(devOpsService);
            devOpsService.Verify(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
            devOpsService.Verify(x => x.GetPipelineRunAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test, Combinatorial]
        public async Task GenerateSDK_WhenPending_SkipsNewRunWithoutLookingUpPipeline(
            [Values("Pending", "PENDING", "pending")] string generationStatus,
            [Values(null, "", " \t", "not-a-url", PreviousGenerationPipelineUrl)] string? pipelineUrl)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline(generationStatus, pipelineUrl);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains("Pending"));
            Assert.That(result.Details, Has.Some.Contains("was not triggered to avoid duplicate generation"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            VerifyNoGeneration(devOpsService);
        }

        [Test, Combinatorial]
        public async Task GenerateSDK_WhenInProgressAndPreviousPipelineIsActive_SkipsNewRun(
            [Values("In progress", "IN PROGRESS")] string generationStatus,
            [Values(BuildStatus.NotStarted, BuildStatus.InProgress, BuildStatus.Postponed, BuildStatus.Cancelling)] BuildStatus pipelineStatus)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline(generationStatus);
            devOpsService.Setup(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Build { Id = 99, Status = pipelineStatus });

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ToString(), Does.Contain("was not triggered to avoid duplicate generation"));
            Assert.That(result.ToString(), Does.Contain(PreviousGenerationPipelineUrl));
            devOpsService.Verify(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()), Times.Once);
            VerifyNoGeneration(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test, Combinatorial]
        public async Task GenerateSDK_WhenInProgressHasNoPipelineUrl_RetriesGeneration(
            [Values("In progress", "IN PROGRESS")] string generationStatus,
            [Values(null, "", " \t")] string? pipelineUrl)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline(generationStatus, pipelineUrl);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                typespecProjectRoot: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                apiVersion: "2023-01-01",
                sdkReleaseType: "beta",
                language: "Java",
                workItemId: 456
            );

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test, Combinatorial]
        public async Task GenerateSDK_WhenInProgressAndPreviousPipelineCompleted_AllowsGeneration(
            [Values("In progress", "IN PROGRESS")] string generationStatus,
            [Values(BuildResult.Succeeded, BuildResult.PartiallySucceeded, BuildResult.Failed, BuildResult.Canceled)] BuildResult pipelineResult)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline(generationStatus);
            devOpsService.Setup(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Build { Id = 99, Status = BuildStatus.Completed, Result = pipelineResult });

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, apiVersion: "2023-01-01");

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()), Times.Once);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=99&view=results")]
        [TestCase("https://dev.azure.com/azure-sdk/internal/_build/results?view=results&buildId=99")]
        public async Task GenerateSDK_WhenPipelineLinkHasOtherQueryParameters_ChecksCorrectBuild(string pipelineUrl)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline("In progress", pipelineUrl);
            devOpsService.Setup(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Build { Id = 99, Status = BuildStatus.Completed });

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()), Times.Once);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase("not-a-url")]
        [TestCase("https://dev.azure.com/azure-sdk/internal/_build/results")]
        [TestCase("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=0")]
        [TestCase("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=-1")]
        [TestCase("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=2147483648")]
        [TestCase("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=99&buildId=100")]
        [TestCase("https://dev.azure.com/another-org/internal/_build/results?buildId=99")]
        [TestCase("https://dev.azure.com/azure-sdk/public/_build/results?buildId=99")]
        public async Task GenerateSDK_WhenInProgressPipelineLinkIsInvalid_AllowsGeneration(string pipelineUrl)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline("In progress", pipelineUrl);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase(null)]
        [TestCase(BuildStatus.None)]
        [TestCase(BuildStatus.All)]
        [TestCase((BuildStatus)1024)]
        public async Task GenerateSDK_WhenInProgressPipelineStatusIsUnknown_AllowsGeneration(BuildStatus? status)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline("In progress");
            devOpsService.Setup(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Build { Id = 99, Status = status });

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()), Times.Once);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task GenerateSDK_WhenInProgressPipelineIsMissingOrMismatched_AllowsGeneration(bool isMissing)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline("In progress");
            devOpsService.Setup(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(isMissing ? null! : new Build { Id = 98, Status = BuildStatus.InProgress });

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()), Times.Once);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase("archived")]
        [TestCase("inaccessible")]
        [TestCase("timeout")]
        public async Task GenerateSDK_WhenInProgressPipelineLookupFails_AllowsGeneration(string failureKind)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline("In progress");
            Exception failure = failureKind switch
            {
                "archived" => new HttpRequestException("Build has been archived", null, System.Net.HttpStatusCode.NotFound),
                "inaccessible" => new HttpRequestException("Build is not accessible", null, System.Net.HttpStatusCode.Forbidden),
                _ => new TaskCanceledException("Lookup timed out")
            };
            devOpsService.Setup(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()), Times.Once);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Not applicable")]
        [TestCase("not applicable")]
        [TestCase("Completed")]
        [TestCase("Failed")]
        public async Task GenerateSDK_WhenStatusIsNotPendingOrInProgress_DoesNotCheckOldPipeline(string? generationStatus)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline(generationStatus!);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task GenerateSDK_WhenPipelineLookupIsCanceled_DoesNotQueue()
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline("In progress");
            using var cancellation = new CancellationTokenSource();
            var lookup = new TaskCompletionSource<Build>(TaskCreationOptions.RunContinuationsAsynchronously);
            devOpsService.Setup(x => x.GetPipelineRunAsync(99, cancellation.Token)).Returns(lookup.Task);

            var generation = specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, ct: cancellation.Token);
            cancellation.Cancel();

            try
            {
                Assert.CatchAsync<OperationCanceledException>(async () => await generation.WaitAsync(TimeSpan.FromSeconds(5)));
                devOpsService.Verify(x => x.GetPipelineRunAsync(99, cancellation.Token), Times.Once);
                VerifyNoGeneration(devOpsService);
            }
            finally
            {
                lookup.TrySetResult(new Build { Id = 99, Status = BuildStatus.Completed });
                await generation.ContinueWith(_ => { });
            }
        }

        [Test]
        public async Task GenerateSDK_WhenAllLanguagesAreNotApplicable_AllowsEachLanguage()
        {
            string[] languages = [".NET", "Java", "JavaScript", "Go", "Python"];
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SDKInfo = languages.Select(language => new SDKInfo
                {
                    Language = language,
                    PackageName = "azure-test",
                    GenerationStatus = "Not applicable"
                }).ToList()
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan);

            foreach (var language in languages)
            {
                var result = await specWorkflowTool.RunGenerateSdkAsync(
                    "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", language,
                    workItemId: 456, apiVersion: "2023-01-01");

                Assert.That(result.Status, Is.EqualTo("Success"));
                Assert.That(result.Details, Has.Some.Contains("has been initiated to generate the SDK"));
                devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                    PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2023-01-01", "beta",
                    language, 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task GenerateSDK_WhenPreviousPipelineIsFinishedOrMissing_StillChecksConflictingReleasePlans(bool lookupFails)
        {
            var devOpsService = SetupSdkGenerationToolWithPipeline("In progress");
            if (lookupFails)
            {
                devOpsService.Setup(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("Build has been archived", null, System.Net.HttpStatusCode.NotFound));
            }
            else
            {
                devOpsService.Setup(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Build { Id = 99, Status = BuildStatus.Completed });
            }
            devOpsService.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new ReleasePlanWorkItem
                    {
                        WorkItemId = 999,
                        SDKInfo =
                        [
                            new SDKInfo
                            {
                                Language = "Java",
                                SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/123"
                            }
                        ]
                    }
                ]);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ToString(), Does.Contain("Another active release plan"));
            devOpsService.Verify(x => x.GetPipelineRunAsync(99, It.IsAny<CancellationToken>()), Times.Once);
            VerifyNoGeneration(devOpsService);
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
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
            releasePlan.SpecCommitSHA = PinnedSpecCommit;
            releasePlan.SpecAPIVersion = "2023-01-01";
            releasePlan.ActiveSpecPullRequest = LinkedSpecPullRequest;
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
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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

        [TestCase("2024-01-01-preview", "2024-01-01", false)]
        [TestCase("", "2024-01-01-preview", false)]
        [TestCase("none", "2024-01-01-preview", false)]
        [TestCase("2024-01-01-preview", "2024-01-01", true)]
        [TestCase("", "2024-01-01-preview", true)]
        [TestCase("none", "2024-01-01-preview", true)]
        public async Task GenerateSdk_BlocksStableSdkForPreviewApiVersion(string apiVersion, string releasePlanApiVersion, bool requireMergedSpec)
        {
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns("specification/testcontoso/Contoso.Management");

            mockDevOpsService.ConfiguredReleasePlanForWorkItem = new ReleasePlanWorkItem
            {
                SpecAPIVersion = releasePlanApiVersion,
                SpecCommitSHA = PinnedSpecCommit,
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
                workItemId: 456,
                requireMergedSpec: requireMergedSpec
            );

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("Stable SDK generation is not allowed from preview API version"));
            Assert.That(mockDevOpsService.LastGenerationSpecCommitSha, Is.Null);
            mockGitHubService.VerifyNoOtherCalls();
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
                SpecCommitSHA = PinnedSpecCommit,
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
                SpecCommitSHA = PinnedSpecCommit,
                SpecAPIVersion = "2023-01-01",
                ActiveSpecPullRequest = LinkedSpecPullRequest,
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
            var plan = CreatePinnedReleasePlan();
            plan.SpecAPIVersion = "2023-01-01";
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);

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
            VerifyNoGeneration(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task GenerateSdk_WithLegacyUnpinnedLinkedMergedSpec_RequiresConfirmation(bool requireMergedSpec)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                ActiveSpecPullRequest = LinkedSpecPullRequest,
                SpecAPIVersion = "2026-01-01-preview",
                SDKInfo = [new SDKInfo { Language = "Java", PackageName = "azure-test" }]
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMergedSpecPullRequest(PinnedSpecCommit));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: requireMergedSpec);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("update-spec-pr"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("confirm the release target"));
            Assert.That(releasePlan.SpecCommitSHA, Is.Empty);
            Assert.That(releasePlan.SpecAPIVersion, Is.EqualTo("2026-01-01-preview"));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(0)]
        [TestCase(123)]
        public async Task GenerateSdk_WithoutLinkedSpec_DoesNotFallBackToMain(int pullRequestNumber)
        {
            var releasePlan = CreatePinnedReleasePlan();
            releasePlan.ActiveSpecPullRequest = "";
            var devOpsService = SetupSdkGenerationTool(releasePlan, pinSpec: false);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                pullRequestNumber: pullRequestNumber, workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseError, Does.Contain("Link a valid Azure/azure-rest-api-specs pull request"));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test]
        public async Task GenerateSdk_WhenMergedSpecRequiredAndSpecPrIsUnavailable_DoesNotQueue()
        {
            var devOpsService = SetupSdkGenerationTool(CreatePinnedReleasePlan(), pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PullRequest)null!);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: true);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseError, Does.Contain("could not be found"));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase("open")]
        [TestCase("closed")]
        public async Task GenerateSdk_WhenMergedSpecRequiredAndSpecPrIsUnmerged_DoesNotQueue(string state)
        {
            var devOpsService = SetupSdkGenerationTool(CreatePinnedReleasePlan(), pinSpec: false);
            // Even a matching synthetic merge SHA is not proof that a PR has merged.
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateUnmergedSpecPullRequest(PinnedSpecCommit, state));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: true);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("Auto-release generation requires the confirmed target to use the linked PR's merge commit"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("Pre-merge draft SDK review remains available"));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test]
        public async Task GenerateSdk_WhenMergedSpecRequiredAndMergeCommitDiffers_DoesNotRetargetOrQueue()
        {
            var plan = CreatePinnedReleasePlan();
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMergedSpecPullRequest(DifferentSpecCommit));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: true);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("Update the release target after merge"));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(PinnedSpecCommit, "2026-01-01-preview", "beta")]
        [TestCase("0123456789ABCDEF0123456789ABCDEF01234567", "2026-01-01-preview", "beta")]
        [TestCase(PinnedSpecCommit, "2026-01-01", "stable")]
        public async Task GenerateSdk_WhenMergedSpecRequiredAndMergeCommitMatches_UsesStoredTarget(
            string mergeCommitSha, string apiVersion, string sdkReleaseType)
        {
            var plan = CreatePinnedReleasePlan();
            plan.SpecAPIVersion = apiVersion;
            plan.SDKReleaseType = sdkReleaseType;
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            using var cancellation = new CancellationTokenSource();
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, cancellation.Token))
                .ReturnsAsync(CreateMergedSpecPullRequest(mergeCommitSha));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", sdkReleaseType, "Java",
                workItemId: 456, requireMergedSpec: true, specCommitSha: PinnedSpecCommit, ct: cancellation.Token);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains(PinnedSpecCommit));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            Assert.That(plan.SpecAPIVersion, Is.EqualTo(apiVersion));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", apiVersion, sdkReleaseType,
                "Java", 456, "", true, cancellation.Token), Times.Once);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, cancellation.Token), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task GenerateSdk_Command_RequiresMergedSpecOnlyWhenRequested(bool requireMergedSpec)
        {
            var devOpsService = SetupSdkGenerationTool(CreatePinnedReleasePlan(), pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateUnmergedSpecPullRequest());
            var command = specWorkflowTool.GetCommandInstances().Single(c => c.Name == "generate-sdk");
            var arguments = new List<string>
            {
                "--typespec-project", "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                "--release-type", "beta", "--language", "Java", "--workitem-id", "456"
            };
            if (requireMergedSpec)
            {
                arguments.Add("--require-merged-spec");
            }
            var parseResult = command.Parse(arguments.ToArray());
            Assert.That(parseResult.Errors, Is.Empty);
            using var cancellation = new CancellationTokenSource();

            var result = (ReleaseWorkflowResponse)await specWorkflowTool.HandleCommand(parseResult, cancellation.Token);

            if (requireMergedSpec)
            {
                Assert.That(result.Status, Is.EqualTo("Failed"));
                Assert.That(result.ResponseErrors, Has.Some.Contains("Auto-release generation requires"));
                VerifyNoGeneration(devOpsService);
                mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, cancellation.Token), Times.Once);
            }
            else
            {
                Assert.That(result.Status, Is.EqualTo("Success"));
                Assert.That(result.ResponseErrors, Is.Empty);
                devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                    PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2026-01-01-preview", "beta",
                    "Java", 456, "", false, cancellation.Token), Times.Once);
            }
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task GenerateSdk_WithDifferentExpectedSpecCommit_DoesNotQueueOrWrite(bool requireMergedSpec)
        {
            var plan = CreatePinnedReleasePlan();
            plan.SDKInfo[0].SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/789";
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            using var cancellation = new CancellationTokenSource();
            var ct = cancellation.Token;

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: requireMergedSpec, specCommitSha: DifferentSpecCommit, ct: ct);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("spec commit changed"));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            Assert.That(plan.SpecAPIVersion, Is.EqualTo("2026-01-01-preview"));
            Assert.That(plan.SDKReleaseType, Is.EqualTo("beta"));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            devOpsService.Verify(x => x.ResolveReleasePlanByIdAsync(456, ct), Times.Exactly(2));
            devOpsService.VerifyNoOtherCalls();
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(PinnedSpecCommit, true)]
        [TestCase("0123456789ABCDEF0123456789ABCDEF01234567", true)]
        [TestCase(DifferentSpecCommit, false)]
        public async Task GenerateSdk_Command_ValidatesExpectedSpecCommitBeforeRegeneration(string expectedCommit, bool matchesStoredPin)
        {
            var plan = CreatePinnedReleasePlan();
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            var command = specWorkflowTool.GetCommandInstances().Single(c => c.Name == "generate-sdk");
            var parseResult = command.Parse(new[]
            {
                "--typespec-project", RelativeProjectPath,
                "--release-type", "beta", "--language", "Java", "--workitem-id", "456",
                "--spec-commit-sha", expectedCommit
            });
            Assert.That(parseResult.Errors, Is.Empty);
            using var cancellation = new CancellationTokenSource();
            var ct = cancellation.Token;

            var result = (ReleaseWorkflowResponse)await specWorkflowTool.HandleCommand(parseResult, ct: ct);

            Assert.That(result.Status, Is.EqualTo(matchesStoredPin ? "Success" : "Failed"));
            if (matchesStoredPin)
            {
                Assert.That(result.ResponseErrors, Is.Empty);
                Assert.That(result.Details, Has.Some.Contains(PinnedSpecCommit));
                devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                    PinnedSpecCommit, RelativeProjectPath, "2026-01-01-preview", "beta", "Java", 456, "", false, ct), Times.Once);
                devOpsService.Verify(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    RelativeProjectPath, ApiReleaseType.Unknown, ct), Times.Once);
            }
            else
            {
                Assert.That(result.ResponseErrors, Has.Some.Contains("spec commit changed"));
                VerifyNoGeneration(devOpsService);
            }
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            Assert.That(plan.SpecAPIVersion, Is.EqualTo("2026-01-01-preview"));
            Assert.That(plan.SDKReleaseType, Is.EqualTo("beta"));
            VerifyNoSpecTargetUpdate(devOpsService);
            devOpsService.Verify(x => x.ResolveReleasePlanByIdAsync(456, ct), Times.Exactly(2));
            devOpsService.VerifyNoOtherCalls();
            mockGitHubService.VerifyNoOtherCalls();
            mockTypeSpecHelper.VerifyNoOtherCalls();
        }

        [TestCase("beta", "stable", false)]
        [TestCase("beta", "stable", true)]
        [TestCase("stable", "beta", false)]
        [TestCase("stable", "beta", true)]
        public async Task GenerateSdk_WithDifferentSdkReleaseType_DoesNotQueueOrWrite(
            string storedReleaseType, string requestedReleaseType, bool requireMergedSpec)
        {
            var plan = CreatePinnedReleasePlan();
            plan.SpecAPIVersion = "2026-01-01";
            plan.SDKReleaseType = storedReleaseType;
            plan.SDKInfo[0].SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/789";
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            using var cancellation = new CancellationTokenSource();
            var ct = cancellation.Token;

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", requestedReleaseType, "Java",
                workItemId: 456, requireMergedSpec: requireMergedSpec, specCommitSha: PinnedSpecCommit, ct: ct);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("SDK release type does not match the release plan's confirmed target"));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            Assert.That(plan.SpecAPIVersion, Is.EqualTo("2026-01-01"));
            Assert.That(plan.SDKReleaseType, Is.EqualTo(storedReleaseType));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            devOpsService.Verify(x => x.ResolveReleasePlanByIdAsync(456, ct), Times.Exactly(2));
            devOpsService.VerifyNoOtherCalls();
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task GenerateSdk_WithDifferentApiVersion_DoesNotRetargetReleasePlan(bool requireMergedSpec)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SpecAPIVersion = "2026-01-01-preview",
                SDKInfo = [new SDKInfo { Language = "Java", PackageName = "azure-test" }]
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, apiVersion: "2026-07-01", requireMergedSpec: requireMergedSpec);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("API version"));
            Assert.That(releasePlan.SpecAPIVersion, Is.EqualTo("2026-01-01-preview"));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(".NET")]
        [TestCase("Java")]
        [TestCase("JavaScript")]
        [TestCase("Python")]
        [TestCase("Go")]
        public async Task GenerateSdk_WithStoredPin_DoesNotRefreshFromGitHubOrMain(string language)
        {
            var plan = CreatePinnedReleasePlan(language);
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Spec repository has changed or is unavailable"));

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var result = await specWorkflowTool.RunGenerateSdkAsync(
                    "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", language,
                    workItemId: 456, specCommitSha: PinnedSpecCommit);
                Assert.That(result.Status, Is.EqualTo("Success"));
                Assert.That(result.Details, Has.Some.Contains(PinnedSpecCommit));
            }

            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2026-01-01-preview", "beta",
                language, 456, "", false, It.IsAny<CancellationToken>()), Times.Exactly(2));
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test]
        public async Task GenerateSdk_WithStoredOpenSpecPrHead_AllowsManualDraftWithoutFetchingSpecPr()
        {
            var specPullRequest = CreateUnmergedSpecPullRequest();
            var plan = CreatePinnedReleasePlan();
            plan.SpecCommitSHA = specPullRequest.Head.Sha;
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(specPullRequest);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            Assert.That(specPullRequest.Merged, Is.False);
            Assert.That(specPullRequest.MergeCommitSha, Is.Not.EqualTo(plan.SpecCommitSHA));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2026-01-01-preview", "beta",
                "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test]
        public async Task GenerateSdk_WithExistingSdkBranch_RegeneratesAtStoredPin()
        {
            var specPullRequest = CreateUnmergedSpecPullRequest();
            var plan = CreatePinnedReleasePlan();
            plan.SpecCommitSHA = specPullRequest.Head.Sha;
            plan.SDKInfo[0].SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/789";
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(specPullRequest);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-sdk-for-java", 789, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(
                    """{"number":789,"state":"open","head":{"ref":"feature/existing-sdk"}}"""));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, specCommitSha: PinnedSpecCommit);

            Assert.That(result.Status, Is.EqualTo("Success"));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2026-01-01-preview", "beta",
                "Java", 456, "feature/existing-sdk", false, It.IsAny<CancellationToken>()), Times.Once);
            devOpsService.Verify(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-sdk-for-java", 789, It.IsAny<CancellationToken>()), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test, Combinatorial]
        public async Task GenerateSdk_WithMissingOrInvalidStoredPin_RequiresConfirmation(
            [Values(null, "", " \t", "main", "abc123", "gggggggggggggggggggggggggggggggggggggggg", "0123456789abcdef0123456789abcdef012345670")] string? commitSha,
            [Values(false, true)] bool requireMergedSpec)
        {
            var plan = CreatePinnedReleasePlan();
            plan.SpecCommitSHA = commitSha!;
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: requireMergedSpec);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("invalid spec commit SHA"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("update-spec-pr"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("confirm the release target"));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(commitSha));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test, Combinatorial]
        public async Task GenerateSdk_WithValidPinButMissingStoredApiVersion_RequiresConfirmation(
            [Values(null, "", " \t")] string? storedApiVersion,
            [Values("", "none", "2026-01-01-preview")] string requestedApiVersion,
            [Values(false, true)] bool requireMergedSpec)
        {
            var plan = CreatePinnedReleasePlan();
            plan.SpecAPIVersion = storedApiVersion!;
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, apiVersion: requestedApiVersion, requireMergedSpec: requireMergedSpec);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("missing or invalid spec commit SHA or API version"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("update-spec-pr"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("confirm the release target"));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            Assert.That(plan.SpecAPIVersion, Is.EqualTo(storedApiVersion));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase("")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr/pull/123")]
        [TestCase("https://github.com/another-org/azure-rest-api-specs/pull/123")]
        [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/123")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/pull/9999999999999999")]
        public async Task GenerateSdk_WithInvalidOrPrivateLinkedSpec_DoesNotQueue(string specPullRequest)
        {
            var plan = CreatePinnedReleasePlan();
            plan.ActiveSpecPullRequest = specPullRequest;
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task GenerateSdk_WithPrivatePreviewPlan_ReturnsBeforeTargetValidation(bool requireMergedSpec)
        {
            var plan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                ApiReleaseType = ApiReleaseType.PrivatePreview,
                ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs-pr/pull/123"
            };
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "invalid-path", "invalid-release-type", "invalid-language",
                workItemId: 456, requireMergedSpec: requireMergedSpec);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.Details, Has.Some.Contains("Private Preview"));
            Assert.That(result.Details, Has.Some.Contains("generate the SDK locally only"));
            Assert.That(plan.SpecCommitSHA, Is.Empty);
            Assert.That(plan.SpecAPIVersion, Is.Empty);
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            devOpsService.Verify(x => x.ResolveReleasePlanByIdAsync(456, It.IsAny<CancellationToken>()), Times.Once);
            devOpsService.VerifyNoOtherCalls();
            mockGitHubService.VerifyNoOtherCalls();
            mockTypeSpecHelper.VerifyNoOtherCalls();
        }

        [Test]
        public async Task GenerateSdk_WithDifferentSpecPr_DoesNotOverridePin()
        {
            var devOpsService = SetupSdkGenerationTool(CreatePinnedReleasePlan());

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", pullRequestNumber: 999, workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("does not match the release plan's linked PR"));
            VerifyNoGeneration(devOpsService);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase("unavailable")]
        [TestCase("timeout")]
        public async Task GenerateSdk_WhenMergedSpecLookupFails_DoesNotQueue(string failureKind)
        {
            var plan = CreatePinnedReleasePlan();
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            Exception failure = failureKind == "timeout"
                ? new TaskCanceledException("Spec lookup timed out")
                : new HttpRequestException("Spec lookup failed");
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: true);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.ResponseError, Does.Contain(failure.Message));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase("")]
        [TestCase("main")]
        [TestCase("abc123")]
        public async Task GenerateSdk_WhenMergedSpecRequiredAndMergedPrHasInvalidCommit_DoesNotQueue(string commitSha)
        {
            var plan = CreatePinnedReleasePlan();
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMergedSpecPullRequest(commitSha));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: true);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseError, Does.Contain("does not have a valid merge commit SHA"));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test]
        public async Task GenerateSdk_WhenMergedSpecLookupIsCanceled_DoesNotQueue()
        {
            var devOpsService = SetupSdkGenerationTool(CreatePinnedReleasePlan(), pinSpec: false);
            using var cancellation = new CancellationTokenSource();
            var lookup = new TaskCompletionSource<PullRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, cancellation.Token)).Returns(lookup.Task);

            var generation = specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: true, ct: cancellation.Token);
            cancellation.Cancel();
            try
            {
                Assert.CatchAsync<OperationCanceledException>(async () => await generation.WaitAsync(TimeSpan.FromSeconds(5)));
                VerifyNoGeneration(devOpsService);
                VerifyNoSpecTargetUpdate(devOpsService);
                mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, cancellation.Token), Times.Once);
                mockGitHubService.VerifyNoOtherCalls();
            }
            finally
            {
                lookup.TrySetResult(CreateMergedSpecPullRequest(PinnedSpecCommit));
                await generation.ContinueWith(_ => { });
            }
        }

        [Test]
        public void GenerateSdk_WhenCanceledAfterMergedSpecLookup_DoesNotQueue()
        {
            var devOpsService = SetupSdkGenerationTool(CreatePinnedReleasePlan(), pinSpec: false);
            using var cancellation = new CancellationTokenSource();
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, cancellation.Token))
                .Callback(() => cancellation.Cancel())
                .ReturnsAsync(CreateMergedSpecPullRequest(PinnedSpecCommit));

            Assert.CatchAsync<OperationCanceledException>(async () => await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                workItemId: 456, requireMergedSpec: true, ct: cancellation.Token));

            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, cancellation.Token), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase(RelativeProjectPath, RelativeProjectPath)]
        [TestCase(RelativeProjectPath + "/", RelativeProjectPath)]
        [TestCase("specification\\testcontoso\\Contoso.Management\\", RelativeProjectPath)]
        [TestCase(RelativeProjectPath, RelativeProjectPath + "/")]
        [TestCase(RelativeProjectPath, "specification\\testcontoso\\Contoso.Management\\")]
        public async Task GenerateSdk_WithConfirmedRelativeProject_DoesNotRequireLocalCheckout(string requestedPath, string storedPath)
        {
            var plan = CreatePinnedReleasePlan();
            plan.APISpecProjectPath = storedPath;
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            using var cancellation = new CancellationTokenSource();
            var ct = cancellation.Token;
            Assert.That(Path.IsPathRooted(requestedPath), Is.False);
            Assert.That(Directory.Exists(requestedPath), Is.False, "This test must exercise generation without a local spec checkout.");

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                requestedPath, "beta", "Java", workItemId: 456, specCommitSha: PinnedSpecCommit, ct: ct);

            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.ResponseErrors, Is.Empty);
            Assert.That(result.TypeSpecProject, Is.EqualTo(RelativeProjectPath));
            Assert.That(plan.APISpecProjectPath, Is.EqualTo(storedPath));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, RelativeProjectPath, "2026-01-01-preview", "beta", "Java", 456, "", false, ct), Times.Once);
            devOpsService.Verify(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                RelativeProjectPath, ApiReleaseType.Unknown, ct), Times.Once);
            VerifyNoSpecTargetUpdate(devOpsService);
            devOpsService.Verify(x => x.ResolveReleasePlanByIdAsync(456, ct), Times.Exactly(2));
            devOpsService.VerifyNoOtherCalls();
            mockTypeSpecHelper.VerifyNoOtherCalls();
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase("specification/other/Other.Management")]
        [TestCase("specification\\other\\Other.Management\\")]
        public async Task GenerateSdk_WithDifferentRelativeProject_DoesNotQueueOrWrite(string requestedPath)
        {
            var plan = CreatePinnedReleasePlan();
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            using var cancellation = new CancellationTokenSource();
            var ct = cancellation.Token;
            Assert.That(Directory.Exists(requestedPath), Is.False);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                requestedPath, "beta", "Java", workItemId: 456, specCommitSha: PinnedSpecCommit, ct: ct);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("does not match the release plan's project"));
            Assert.That(plan.APISpecProjectPath, Is.EqualTo(RelativeProjectPath));
            Assert.That(plan.SpecCommitSHA, Is.EqualTo(PinnedSpecCommit));
            VerifyNoGeneration(devOpsService);
            VerifyNoSpecTargetUpdate(devOpsService);
            devOpsService.Verify(x => x.ResolveReleasePlanByIdAsync(456, ct), Times.Once);
            devOpsService.VerifyNoOtherCalls();
            mockTypeSpecHelper.VerifyNoOtherCalls();
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase("specification/testcontoso/Contoso.Management/", "specification/testcontoso/Contoso.Management", true)]
        [TestCase("specification\\testcontoso\\Contoso.Management\\", "specification/testcontoso/Contoso.Management", true)]
        [TestCase("specification/testcontoso/Contoso.Management", "specification/testcontoso/Contoso.Management/", true)]
        [TestCase("specification/other/Other.Management", "specification/testcontoso/Contoso.Management", false)]
        public async Task GenerateSdk_ProjectComparisonNormalizesSeparators(string requestedPath, string storedPath, bool matches)
        {
            var plan = CreatePinnedReleasePlan();
            plan.APISpecProjectPath = storedPath;
            var devops = SetupSdkGenerationTool(plan, pinSpec: false);
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>())).Returns(requestedPath);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management/", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo(matches ? "Success" : "Failed"));
            if (matches)
            {
                devops.Verify(x => x.RunSDKGenerationPipelineAsync(PinnedSpecCommit, "specification/testcontoso/Contoso.Management",
                    "2026-01-01-preview", "beta", "Java", 456, "", false, It.IsAny<CancellationToken>()), Times.Once);
            }
            else
            {
                Assert.That(result.ResponseErrors, Has.Some.Contains("does not match the release plan's project"));
                VerifyNoGeneration(devops);
            }
            VerifyNoSpecTargetUpdate(devops);
            mockTypeSpecHelper.Verify(x => x.GetTypeSpecProjectRelativePath(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management/"), Times.Once);
            mockTypeSpecHelper.VerifyNoOtherCalls();
            mockGitHubService.VerifyNoOtherCalls();
        }

        private static ReleasePlanWorkItem CreatePinnedReleasePlan(string language = "Java") => new()
        {
            WorkItemId = 456,
            ActiveSpecPullRequest = LinkedSpecPullRequest,
            SpecCommitSHA = PinnedSpecCommit,
            SpecAPIVersion = "2026-01-01-preview",
            APISpecProjectPath = RelativeProjectPath,
            SDKReleaseType = "beta",
            SDKInfo = [new SDKInfo { Language = language, PackageName = "azure-test" }]
        };

        private static PullRequest CreateMergedSpecPullRequest(string commitSha) =>
            new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(System.Text.Json.JsonSerializer.Serialize(new
            {
                number = 123,
                state = "closed",
                merged_at = "2026-09-01T00:00:00Z",
                merge_commit_sha = commitSha,
                @base = new { @ref = "main" }
            }));

        private static PullRequest CreateUnmergedSpecPullRequest(string mergeCommitSha = DifferentSpecCommit, string state = "open") =>
            new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(System.Text.Json.JsonSerializer.Serialize(new
            {
                number = 123,
                state,
                merge_commit_sha = mergeCommitSha,
                head = new { @ref = "feature/spec-review", sha = PinnedSpecCommit },
                @base = new { @ref = "main" }
            }));

        private static void VerifyNoSpecTargetUpdate(Mock<IDevOpsService> devOpsService)
        {
            devOpsService.Verify(x => x.UpdateSpecPullRequestAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
            devOpsService.Verify(x => x.UpdateApiSpecVersionAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static void VerifyNoGeneration(Mock<IDevOpsService> devOpsService)
        {
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Never);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Never);
        }

        private Mock<IDevOpsService> SetupSdkGenerationToolWithPipeline(string generationStatus, string? pipelineUrl = PreviousGenerationPipelineUrl)
        {
            return SetupSdkGenerationTool(new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SDKInfo =
                [
                    new SDKInfo
                    {
                        Language = "Java",
                        PackageName = "azure-test",
                        GenerationStatus = generationStatus,
                        GenerationPipelineUrl = pipelineUrl!
                    }
                ]
            });
        }

        private Mock<IDevOpsService> SetupSdkGenerationTool(ReleasePlanWorkItem releasePlan, bool pinSpec = true)
        {
            if (pinSpec)
            {
                // Default existing fixtures to a confirmed target; pinSpec: false preserves negative-test inputs.
                releasePlan.SpecCommitSHA = PinnedSpecCommit;
                releasePlan.ActiveSpecPullRequest = LinkedSpecPullRequest;
                if (string.IsNullOrWhiteSpace(releasePlan.SpecAPIVersion))
                {
                    releasePlan.SpecAPIVersion = "2023-01-01";
                }
            }
            var devOpsService = new Mock<IDevOpsService>();
            devOpsService.Setup(x => x.ResolveReleasePlanByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(releasePlan);
            devOpsService.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());
            devOpsService.Setup(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
