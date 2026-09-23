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
using PullRequest = Octokit.PullRequest;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    [TestFixture]
    internal class SpecWorkflowToolTests
    {
        private const string PinnedSpecCommit = "0123456789abcdef0123456789abcdef01234567";
        private const string LinkedSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/123";
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
                SpecCommitSha = PinnedSpecCommit,
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
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
                "Java", 456, "", It.IsAny<CancellationToken>()), Times.Once);
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
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
                "Java", 456, "", It.IsAny<CancellationToken>()), Times.Once);
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
                "Java", 456, "", It.IsAny<CancellationToken>()), Times.Once);
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                "Java", 456, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                "Java", 456, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                "Java", 456, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                "Java", 456, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                "Java", 456, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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
                devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
                    language, 456, "", It.IsAny<CancellationToken>()), Times.Once);
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
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
            releasePlan.SpecCommitSha = PinnedSpecCommit;
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
                SpecCommitSha = PinnedSpecCommit,
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
                SpecCommitSha = PinnedSpecCommit,
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
                SpecCommitSha = PinnedSpecCommit,
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
                SpecCommitSha = PinnedSpecCommit,
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

        [Test]
        public async Task GenerateSdk_WithLinkedMergedSpec_UsesImmutableCommit()
        {
            const string mergeCommit = "0123456789abcdef0123456789abcdef01234567";
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/123",
                SpecAPIVersion = "2026-01-01-preview",
                SDKInfo = [new SDKInfo { Language = "Java", PackageName = "azure-test" }]
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(
                    $$$"""{"number":123,"state":"closed","merged_at":"2026-09-01T00:00:00Z","merge_commit_sha":"{{{mergeCommit}}}","base":{"ref":"main"}}"""));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            devOpsService.Verify(x => x.UpdateSpecCommitShaAsync(456, LinkedSpecPullRequest, mergeCommit, It.IsAny<CancellationToken>()), Times.Once);
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                mergeCommit, "specification/testcontoso/Contoso.Management", "2026-01-01-preview", "beta",
                "Java", 456, "", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase(0)]
        [TestCase(123)]
        public async Task GenerateSdk_WithoutLinkedSpec_DoesNotFallBackToMain(int pullRequestNumber)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                SDKInfo = [new SDKInfo { Language = "Java", PackageName = "azure-test" }]
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan, pinSpec: false);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java",
                pullRequestNumber: pullRequestNumber, workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task GenerateSdk_WithUnavailableOrUnmergedSpec_DoesNotQueue(bool missing)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 456,
                ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/123",
                SDKInfo = [new SDKInfo { Language = "Java", PackageName = "azure-test" }]
            };
            var devOpsService = SetupSdkGenerationTool(releasePlan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(missing ? null! : new PullRequest());

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task GenerateSdk_WithDifferentApiVersion_DoesNotRetargetReleasePlan()
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
                workItemId: 456, apiVersion: "2026-07-01");

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("API version"));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
                    "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", language, workItemId: 456);
                Assert.That(result.Status, Is.EqualTo("Success"));
                Assert.That(result.Details, Has.Some.Contains(PinnedSpecCommit));
            }

            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2026-01-01-preview", "beta",
                language, 456, "", It.IsAny<CancellationToken>()), Times.Exactly(2));
            devOpsService.Verify(x => x.UpdateSpecCommitShaAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [Test]
        public async Task GenerateSdk_WithExistingSdkBranch_RegeneratesAtStoredPin()
        {
            var plan = CreatePinnedReleasePlan();
            plan.SDKInfo[0].SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/789";
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-sdk-for-java", 789, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(
                    """{"number":789,"state":"open","head":{"ref":"feature/existing-sdk"}}"""));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Success"));
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                PinnedSpecCommit, "specification/testcontoso/Contoso.Management", "2026-01-01-preview", "beta",
                "Java", 456, "feature/existing-sdk", It.IsAny<CancellationToken>()), Times.Once);
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-sdk-for-java", 789, It.IsAny<CancellationToken>()), Times.Once);
            mockGitHubService.VerifyNoOtherCalls();
        }

        [TestCase("main")]
        [TestCase("abc123")]
        [TestCase("gggggggggggggggggggggggggggggggggggggggg")]
        [TestCase("0123456789abcdef0123456789abcdef012345670")]
        public async Task GenerateSdk_WithInvalidStoredPin_DoesNotFallBack(string commitSha)
        {
            var plan = CreatePinnedReleasePlan();
            plan.SpecCommitSha = commitSha;
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            Assert.That(result.ResponseErrors, Has.Some.Contains("invalid spec commit SHA"));
            VerifyNoGeneration(devOpsService);
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
            mockGitHubService.VerifyNoOtherCalls();
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

        [TestCase(false)]
        [TestCase(true)]
        public async Task GenerateSdk_WhenSavingLegacyPinFails_DoesNotQueue(bool throws)
        {
            var plan = CreatePinnedReleasePlan();
            plan.SpecCommitSha = "";
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMergedSpecPullRequest(PinnedSpecCommit));
            var save = devOpsService.Setup(x => x.UpdateSpecCommitShaAsync(456, LinkedSpecPullRequest, PinnedSpecCommit, It.IsAny<CancellationToken>()));
            if (throws)
            {
                save.ThrowsAsync(new InvalidOperationException("Pin storage failed or revision changed"));
            }
            else
            {
                save.ReturnsAsync(false);
            }

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            VerifyNoGeneration(devOpsService);
        }

        [TestCase("")]
        [TestCase("main")]
        [TestCase("abc123")]
        public async Task GenerateSdk_WhenMergedPrHasNoValidCommit_DoesNotSaveOrQueue(string commitSha)
        {
            var plan = CreatePinnedReleasePlan();
            plan.SpecCommitSha = "";
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMergedSpecPullRequest(commitSha));

            var result = await specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456);

            Assert.That(result.Status, Is.EqualTo("Failed"));
            VerifyNoGeneration(devOpsService);
            devOpsService.Verify(x => x.UpdateSpecCommitShaAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task GenerateSdk_WhenLegacySpecLookupIsCanceled_DoesNotSaveOrQueue()
        {
            var plan = CreatePinnedReleasePlan();
            plan.SpecCommitSha = "";
            var devOpsService = SetupSdkGenerationTool(plan, pinSpec: false);
            using var cancellation = new CancellationTokenSource();
            var lookup = new TaskCompletionSource<PullRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, cancellation.Token)).Returns(lookup.Task);

            var generation = specWorkflowTool.RunGenerateSdkAsync(
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", "beta", "Java", workItemId: 456, ct: cancellation.Token);
            cancellation.Cancel();
            try
            {
                Assert.CatchAsync<OperationCanceledException>(async () => await generation.WaitAsync(TimeSpan.FromSeconds(5)));
                VerifyNoGeneration(devOpsService);
                devOpsService.Verify(x => x.UpdateSpecCommitShaAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                lookup.TrySetResult(CreateMergedSpecPullRequest(PinnedSpecCommit));
                await generation.ContinueWith(_ => { });
            }
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
                    "2026-01-01-preview", "beta", "Java", 456, "", It.IsAny<CancellationToken>()), Times.Once);
            }
            else
            {
                VerifyNoGeneration(devops);
            }
        }

        private static ReleasePlanWorkItem CreatePinnedReleasePlan(string language = "Java") => new()
        {
            WorkItemId = 456,
            ActiveSpecPullRequest = LinkedSpecPullRequest,
            SpecCommitSha = PinnedSpecCommit,
            SpecAPIVersion = "2026-01-01-preview",
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

        private static void VerifyNoGeneration(Mock<IDevOpsService> devOpsService) =>
            devOpsService.Verify(x => x.RunSDKGenerationPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

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
                releasePlan.SpecCommitSha = PinnedSpecCommit;
                releasePlan.ActiveSpecPullRequest = LinkedSpecPullRequest;
            }
            var devOpsService = new Mock<IDevOpsService>();
            devOpsService.Setup(x => x.ResolveReleasePlanByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(releasePlan);
            devOpsService.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReleasePlanWorkItem>());
            devOpsService.Setup(x => x.UpdateSpecCommitShaAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
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
