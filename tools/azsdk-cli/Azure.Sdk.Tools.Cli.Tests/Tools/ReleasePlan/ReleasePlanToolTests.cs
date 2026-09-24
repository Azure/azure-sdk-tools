using System.Globalization;
using Moq;
using Moq.Protected;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    internal class ReleasePlanToolTests
    {
        private TestLogger<ReleasePlanTool> logger;
        private IDevOpsService devOpsService;
        private IGitHelper gitHelper;
        private IGitHubService gitHubService;
        private ITypeSpecHelper typeSpecHelper;
        private IUserHelper userHelper;
        private IEnvironmentHelper environmentHelper;
        private ReleasePlanTool releasePlanTool;
        private IInputSanitizer inputSanitizer;
        private HttpClient httpClient;
        // Keep existing historical target-month fixtures independent of the wall clock.
        private readonly TimeProvider _timeProvider = new FixedTimeProvider(new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero));

        [SetUp]
        public void Setup()
        {

            logger = new TestLogger<ReleasePlanTool>();
            devOpsService = new MockDevOpsService();
            gitHubService = new MockGitHubService();
            inputSanitizer = new InputSanitizer();
            httpClient = new Mock<HttpClient>().Object;

            var userHelperMock = new Mock<IUserHelper>();
            userHelperMock.Setup(x => x.GetUserEmail(It.IsAny<CancellationToken>())).ReturnsAsync("test@example.com");
            userHelper = userHelperMock.Object;

            var environmentHelperMock = new Mock<IEnvironmentHelper>();
            environmentHelperMock.Setup(x => x.GetBooleanVariable(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);
            environmentHelper = environmentHelperMock.Object;

            var gitHelperMock = new Mock<IGitHelper>();
            gitHelperMock.Setup(x => x.GetBranchNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("testBranch");
            gitHelperMock.Setup(x => x.GetRepoRemoteUriAsync(It.Is<string>(p => !string.IsNullOrEmpty(p) && !Uri.IsWellFormedUriString(p, UriKind.Absolute)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Uri("https://github.com/Azure/azure-rest-api-specs.git"));
            gitHelperMock.Setup(x => x.DiscoverRepoRootAsync(It.Is<string>(p => !string.IsNullOrEmpty(p) && !Uri.IsWellFormedUriString(p, UriKind.Absolute)), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string path, CancellationToken _) => path.Contains("specification") ? path.Substring(0, path.IndexOf("specification")) : path);
            gitHelper = gitHelperMock.Object;

            var processHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), Mock.Of<IRawOutputHelper>());
            var realTypeSpecHelper = new TypeSpecHelper(gitHelper, processHelper);

            // Wrap the real TypeSpecHelper in a mock so ParseTypeSpecProjectAsync returns dummy metadata
            // without requiring the npx emitter to be installed and runnable in CI/test environments.
            var typeSpecHelperMock = new Mock<ITypeSpecHelper>();
            typeSpecHelperMock.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns((string p) => realTypeSpecHelper.IsValidTypeSpecProjectPath(p));
            typeSpecHelperMock.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns((string p) => realTypeSpecHelper.IsTypeSpecProjectForMgmtPlane(p));
            typeSpecHelperMock.Setup(x => x.IsUrl(It.IsAny<string>())).Returns((string p) => realTypeSpecHelper.IsUrl(p));
            typeSpecHelperMock.Setup(x => x.IsValidTypeSpecProjectUrl(It.IsAny<string>())).Returns((string p) => realTypeSpecHelper.IsValidTypeSpecProjectUrl(p));
            typeSpecHelperMock.Setup(x => x.IsTypeSpecUrlForMgmtPlane(It.IsAny<string>())).Returns((string p) => realTypeSpecHelper.IsTypeSpecUrlForMgmtPlane(p));
            typeSpecHelperMock.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>())).Returns((string p) => realTypeSpecHelper.GetTypeSpecProjectRelativePath(p));
            typeSpecHelperMock.Setup(x => x.GetTypeSpecProjectRelativePathFromUrl(It.IsAny<string>())).Returns((string p) => realTypeSpecHelper.GetTypeSpecProjectRelativePathFromUrl(p));
            typeSpecHelperMock.Setup(x => x.GetSpecRepoRootPath(It.IsAny<string>())).Returns((string p) => realTypeSpecHelper.GetSpecRepoRootPath(p));
            typeSpecHelperMock.Setup(x => x.IsRepoPathForPublicSpecRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string p, CancellationToken ct) => realTypeSpecHelper.IsRepoPathForPublicSpecRepoAsync(p, ct));
            typeSpecHelperMock.Setup(x => x.IsRepoPathForSpecRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string p, CancellationToken ct) => realTypeSpecHelper.IsRepoPathForSpecRepoAsync(p, ct));
            typeSpecHelperMock.Setup(x => x.ParseTypeSpecProjectAsync(It.IsAny<string>(), It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateDummyTypeSpecProject());
            typeSpecHelper = typeSpecHelperMock.Object;

            releasePlanTool = new ReleasePlanTool(
                devOpsService,
                gitHelper,
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelper,
                inputSanitizer,
                httpClient,
                Mock.Of<INpxHelper>(),
                Mock.Of<IRawOutputHelper>(),
                Mock.Of<INotificationService>(),
                _timeProvider);
        }

        [Test]
        public async Task Test_Create_releasePlan_for_existing_product()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);

            //Verify service ID and product ID in response. It should have values from previous release plans.
            Assert.That(releaseplan.ReleasePlanDetails?.ServiceTreeId, Is.EqualTo("87654321-4321-8765-1234-210987654321"));
            Assert.That(releaseplan.ReleasePlanDetails?.ProductTreeId, Is.EqualTo("12345678-1234-5678-9012-123456789012"));
        }

        [Test]
        public async Task Test_Create_releasePlan_warns_about_project_schedule_risks()
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            var mockDevOpsService = (MockDevOpsService)devOpsService;
            mockDevOpsService.ConfiguredActiveReleasePlansForTypeSpecPath =
            [
                new ReleasePlanWorkItem
                {
                    WorkItemId = 801,
                    ReleasePlanId = 81,
                    APISpecProjectPath = typeSpecProjectPath,
                    SDKReleaseMonth = "August 2026"
                }
            ];
            var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero));
            var tool = new ReleasePlanTool(devOpsService, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), timeProvider);

            var response = await tool.CreateReleasePlan(
                null,
                "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                "October 2026",
                "GA",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                isTestReleasePlan: true);

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.ReleasePlanDetails, Is.Not.Null);
            Assert.That(response.Warnings, Has.Some.Contains("Release plan 81").And.Contains("past due"));
            Assert.That(response.NextSteps, Has.Some.Contains("azsdk_update_release_plan_target"));
        }

        [Test]
        public async Task Test_Create_releasePlan_copies_product_details_from_previous_release_plan()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");

            // Product name, type and lifecycle should be copied from the previous release plan's product info.
            Assert.That(releaseplan.ReleasePlanDetails?.ProductName, Is.EqualTo("Contoso Management Product Name"));
            Assert.That(releaseplan.ReleasePlanDetails?.ProductType, Is.EqualTo("Offering"));
            Assert.That(releaseplan.ReleasePlanDetails?.ProductLifecycle, Is.EqualTo("GA"));
        }

        [Test]
        public async Task Test_Create_releasePlan_reports_progress()
        {
            var reported = new List<ProgressNotificationValue>();
            var progressMock = new Mock<IProgress<ProgressNotificationValue>>();
            progressMock.Setup(p => p.Report(It.IsAny<ProgressNotificationValue>()))
                .Callback<ProgressNotificationValue>(v => reported.Add(v));

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(progressMock.Object, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);

            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");

            // The release plan creation should report progress via the MCP progress channel.
            Assert.That(reported, Is.Not.Empty, "Expected at least one progress notification to be reported.");
            Assert.That(reported.Any(r => r.Message != null && r.Message.Contains("Creating a release plan")), Is.True,
                "Expected a 'Creating a release plan' progress notification.");
            Assert.That(reported[0].Total, Is.EqualTo(2));
            Assert.That(reported[0].Progress, Is.EqualTo(1));
        }

        [Test]
        public async Task Test_Create_releasePlan_notification_excludes_owners_and_support_for_test_release_plan()
        {
            var notificationMock = new Mock<INotificationService>();
            EmailPayload? captured = null;
            notificationMock
                .Setup(n => n.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()))
                .Callback<EmailPayload, CancellationToken>((p, _) => captured = p)
                .Returns(Task.CompletedTask);

            var tool = CreateReleasePlanToolWithNotificationService(notificationMock.Object);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            await tool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);

            Assert.IsNotNull(captured);
            Assert.That(captured!.CC, Is.Empty, "Test release plans must not CC sdkowners or azsdk support.");
            Assert.That(captured.EmailTo, Is.EqualTo(new List<string> { "test@example.com" }));
        }

        [Test]
        public async Task Test_Create_releasePlan_notification_includes_owners_and_support_for_management_release_plan()
        {
            var notificationMock = new Mock<INotificationService>();
            EmailPayload? captured = null;
            notificationMock
                .Setup(n => n.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()))
                .Callback<EmailPayload, CancellationToken>((p, _) => captured = p)
                .Returns(Task.CompletedTask);

            var tool = CreateReleasePlanToolWithNotificationService(notificationMock.Object);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            await tool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: false);

            Assert.IsNotNull(captured);
            Assert.That(captured!.CC, Does.Contain("sdkowners@microsoft.com"));
            Assert.That(captured.CC, Does.Contain("azsdkexp@microsoft.com"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_api_release_type()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "invalid-type", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Invalid API release type"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_service_tree_id()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", serviceTreeId: "InvalidServiceTreeId", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Service tree ID 'InvalidServiceTreeId' is not a valid GUID"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_product_tree_id()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", productTreeId: "InvalidProductTreeId", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Product tree ID 'InvalidProductTreeId' is not a valid GUID"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_pull_request_url()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/invalid-repo/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Invalid spec pull request URL"));
        }

        [Test]
        public async Task Test_Create_releasePlan_from_language_repo_returns_guidance()
        {
            // Simulate running from a language SDK repository (or any non-spec repo): the git remote
            // is not an azure-rest-api-specs(-pr) repo, so the tool should guide the user to provide an
            // absolute path or run from the specs repo instead of reporting a private-repo error.
            var languageRepoGitHelperMock = new Mock<IGitHelper>();
            languageRepoGitHelperMock.Setup(x => x.GetBranchNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("testBranch");
            languageRepoGitHelperMock.Setup(x => x.GetRepoRemoteUriAsync(It.Is<string>(p => !string.IsNullOrEmpty(p) && !Uri.IsWellFormedUriString(p, UriKind.Absolute)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Uri("https://github.com/Azure/azure-sdk-for-net.git"));
            languageRepoGitHelperMock.Setup(x => x.DiscoverRepoRootAsync(It.Is<string>(p => !string.IsNullOrEmpty(p) && !Uri.IsWellFormedUriString(p, UriKind.Absolute)), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string path, CancellationToken _) => path.Contains("specification") ? path.Substring(0, path.IndexOf("specification")) : path);

            var processHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), Mock.Of<IRawOutputHelper>());
            var languageRepoTypeSpecHelper = new TypeSpecHelper(languageRepoGitHelperMock.Object, processHelper);

            var languageRepoReleasePlanTool = new ReleasePlanTool(
                devOpsService,
                languageRepoGitHelperMock.Object,
                languageRepoTypeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelper,
                inputSanitizer,
                httpClient,
                Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), _timeProvider);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await languageRepoReleasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);

            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Could not locate the Azure REST API specs repository"), $"Unexpected error: {releaseplan.ResponseError}");
            Assert.True(releaseplan.ResponseError.Contains("absolute path"), $"Unexpected error: {releaseplan.ResponseError}");
        }

        [TestCase("TypeSpecTestData/specification/testcontoso/Contoso.Management", "July 2025", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "GA", "", "")]
        [TestCase("TypeSpecTestData/specification/testcontoso/Contoso.Management", "July 2025", "https://github.com/Azure/azure-rest-api-specs/pull/35447", "Public Preview", "", "")]
        [TestCase("TypeSpecTestData/specification/testcontoso/Contoso.Management", "July 2025", "https://github.com/Azure/azure-rest-api-specs-pr/pull/35448", "Private Preview", "", "")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management", "January 2026", "https://github.com/Azure/azure-rest-api-specs/pull/39310", "GA", "12345678-1234-5678-9012-123456789012", "87654321-4321-8765-1234-210987654321")]
        [Test]
        public async Task Test_Create_releasePlan_with_valid_inputs(string typeSpecPath, string targetMonth, string prUrl, string apiType, string serviceId, string productId)
        {
            var result = await releasePlanTool.CreateReleasePlan(null, 
                typeSpecPath, 
                targetMonth,
                apiType,
                specPullRequestUrl: prUrl,
                serviceTreeId: serviceId,
                productTreeId: productId,
                isTestReleasePlan: true);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.IsNotNull(result.ReleasePlanDetails);
            Assert.IsNotNull(result.ReleasePlanDetails.WorkItemId);
            Assert.IsNotNull(result.ReleasePlanDetails.ReleasePlanId);
            Assert.IsNotNull(result.ReleasePlanDetails.ReleasePlanLink);
        }

        [Test]
        public async Task Test_Create_releasePlan_with_AZSDKTOOLS_AGENT_TESTING_true_creates_test_release_plan()
        {
            // Arrange
            var environmentHelperMock = new Mock<IEnvironmentHelper>();
            environmentHelperMock.Setup(x => x.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false)).Returns(true);

            var testReleasePlanTool = new ReleasePlanTool(
                devOpsService,
                gitHelper,
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelperMock.Object,
                inputSanitizer,
                httpClient,
                Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), _timeProvider);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";

            // Act
            var releaseplan = await testReleasePlanTool.CreateReleasePlan(null, 
                testCodeFilePath,
                "July 2025",
                "GA",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                isTestReleasePlan: false); // This should be overridden to true by environment variable

            // Assert
            var releaseplanObj = releaseplan.ReleasePlanDetails as ReleasePlanWorkItem;
            Assert.IsNotNull(releaseplanObj);
            Assert.IsNotNull(releaseplanObj.WorkItemId);
            Assert.IsNotNull(releaseplanObj.ReleasePlanId);
            Assert.IsNotNull(releaseplanObj.ReleasePlanLink);

            // Verify the environment helper was called
            environmentHelperMock.Verify(x => x.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false), Times.Once);
        }

        [Test]
        public async Task Test_Create_releasePlan_with_AZSDKTOOLS_AGENT_TESTING_false_respects_parameter()
        {
            // Arrange
            var environmentHelperMock = new Mock<IEnvironmentHelper>();
            environmentHelperMock.Setup(x => x.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false)).Returns(false);

            var testReleasePlanTool = new ReleasePlanTool(
                devOpsService,
                gitHelper,
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelperMock.Object,
                inputSanitizer,
                httpClient,
                Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), _timeProvider);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";

            // Act
            var releaseplan = await testReleasePlanTool.CreateReleasePlan(null, 
                testCodeFilePath,
                "July 2025",
                "GA",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                isTestReleasePlan: false);

            // Assert
            var releaseplanObj = releaseplan.ReleasePlanDetails as ReleasePlanWorkItem;
            Assert.IsNotNull(releaseplanObj);
            Assert.IsNotNull(releaseplanObj.WorkItemId);
            Assert.IsNotNull(releaseplanObj.ReleasePlanId);
            Assert.IsNotNull(releaseplanObj.ReleasePlanLink);

            // Verify the environment helper was called
            environmentHelperMock.Verify(x => x.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false), Times.Once);
        }
        
        [Test]
        public async Task Test_Create_releasePlan_private_preview_accepts_azure_rest_api_specs_pr_repo()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "Private Preview", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs-pr/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            Assert.IsNotNull(releaseplan.ReleasePlanDetails);
        }

        [Test]
        public async Task Test_Create_releasePlan_private_preview_marks_finished_when_spec_pr_merged()
        {
            var mockGitHubService = (MockGitHubService)gitHubService;
            mockGitHubService.ConfiguredPullRequestMerged = true;

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "Private Preview", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs-pr/pull/35446", isTestReleasePlan: true);

            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            var details = releaseplan.ReleasePlanDetails as ReleasePlanWorkItem;
            Assert.IsNotNull(details);
            Assert.That(details.Status, Is.EqualTo("Finished"));
        }

        [Test]
        public async Task Test_Create_releasePlan_private_preview_warns_when_merge_check_fails_and_still_sends_notification()
        {
            var mockGitHubService = (MockGitHubService)gitHubService;
            mockGitHubService.ThrowOnGetPullRequest = true;

            var notificationMock = new Mock<INotificationService>();
            var notified = false;
            notificationMock
                .Setup(n => n.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()))
                .Callback(() => notified = true)
                .Returns(Task.CompletedTask);

            var tool = CreateReleasePlanToolWithNotificationService(notificationMock.Object);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await tool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "Private Preview", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs-pr/pull/35446", isTestReleasePlan: true);

            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            Assert.IsNotNull(releaseplan.Warnings);
            Assert.That(releaseplan.Warnings, Has.Some.Contains("spec pull request is merged"));
            Assert.IsTrue(notified, "Notification should still be sent even if the merge check fails.");
        }

        [Test]
        public async Task Test_Create_releasePlan_private_preview_rejects_public_spec_pr()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "Private Preview", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("cannot be linked to a Private Preview release plan"));
        }

        [Test]
        public async Task Test_Create_releasePlan_public_preview_rejects_private_spec_pr()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "Public Preview", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs-pr/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("cannot be linked to a Public Preview or GA release plan"));
        }

        [Test]
        public async Task Test_Create_releasePlan_ga_rejects_private_spec_pr()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs-pr/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("cannot be linked to a Public Preview or GA release plan"));
        }

        [Test]
        public async Task Test_Create_releasePlan_spec_pr_validation_is_case_insensitive()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            // Use uppercase URL - should still work
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "Private Preview", specPullRequestUrl: "https://github.com/Azure/Azure-Rest-Api-Specs-PR/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
        }

        [Test]
        public async Task Test_Create_releasePlan_defaults_sdk_type_to_beta_for_preview()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "Public Preview", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            var details = releaseplan.ReleasePlanDetails as ReleasePlanWorkItem;
            Assert.IsNotNull(details);
            Assert.That(details.SDKReleaseType, Is.EqualTo("beta"));
        }

        [Test]
        public async Task Test_Create_releasePlan_defaults_sdk_type_to_stable_for_ga()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, testCodeFilePath, "July 2025", "GA", specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            var details = releaseplan.ReleasePlanDetails as ReleasePlanWorkItem;
            Assert.IsNotNull(details);
            Assert.That(details.SDKReleaseType, Is.EqualTo("stable"));
        }

        [Test]
        public async Task Test_Create_releasePlan_without_spec_pr_with_service_and_product_ids()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, 
                testCodeFilePath,
                "July 2025",
                "GA",
                serviceTreeId: "87654321-4321-8765-1234-210987654321",
                productTreeId: "12345678-1234-5678-9012-123456789012",
                isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            Assert.IsNotNull(releaseplan.ReleasePlanDetails);
            Assert.Greater(releaseplan.ReleasePlanDetails.WorkItemId, 0);
        }

        [Test]
        public async Task Test_Create_releasePlan_without_spec_pr_and_without_ids_succeeds_when_not_derivable()
        {
            // Use a URL TypeSpec path that has no previous release plans in mock
            var testCodeFilePath = "https://github.com/Azure/azure-rest-api-specs/blob/main/specification/unknownservice/Unknown.Service";
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, 
                testCodeFilePath,
                "July 2025",
                "GA",
                isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            var releasePlanDetails = releaseplan.ReleasePlanDetails as ReleasePlanWorkItem;
            Assert.IsNotNull(releasePlanDetails);
            Assert.Greater(releasePlanDetails.WorkItemId, 0);
            Assert.That(releasePlanDetails.ServiceTreeId, Is.Empty);
            Assert.That(releasePlanDetails.ProductTreeId, Is.Empty);
        }

        [Test]
        public async Task Test_Create_releasePlan_returns_existing_plan_with_same_api_version()
        {
            // Arrange: configure the mock to return an existing release plan with matching API version
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var existingReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 999,
                ReleasePlanId = 50001,
                Title = "Existing Release Plan",
                ProductName = "Contoso Management Product Name",
                ProductType = "Offering",
                ProductLifecycle = "GA",
                ServiceTreeId = "87654321-4321-8765-1234-210987654321",
                ProductTreeId = "12345678-1234-5678-9012-123456789012",
                APISpecProjectPath = "specification/testcontoso/Contoso.Management",
                SpecAPIVersion = "2024-01-01",
                ApiReleaseType = ApiReleaseType.GA,
                Status = "In Progress",
                SDKReleaseMonth = "August 2026"
            };

            // The API version must match what CreateDummyTypeSpecProject returns ("2026-05-02-preview")
            existingReleasePlan.SpecAPIVersion = "2026-05-02-preview";
            ((MockDevOpsService)devOpsService).ConfiguredReleasePlanForTypeSpecPathAndApiVersion = existingReleasePlan;
            ((MockDevOpsService)devOpsService).ConfiguredReleasePlanForTypeSpecPathAndApiVersionKey = "specification/testcontoso/Contoso.Management";
            ((MockDevOpsService)devOpsService).ConfiguredApiVersionForTypeSpecPathAndApiVersion = "2026-05-02-preview";
            ((MockDevOpsService)devOpsService).ConfiguredActiveReleasePlansForTypeSpecPath = [existingReleasePlan];
            var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero));
            var tool = new ReleasePlanTool(devOpsService, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), timeProvider);

            // Act
            var releaseplan = await tool.CreateReleasePlan(null,
                testCodeFilePath, 
                "October 2026",
                "GA", 
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446", 
                isTestReleasePlan: true);

            // Assert
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            var releasePlanDetails = releaseplan.ReleasePlanDetails as ReleasePlanWorkItem;
            Assert.IsNotNull(releasePlanDetails);
            Assert.That(releasePlanDetails.WorkItemId, Is.EqualTo(999), "Should return existing release plan");
            Assert.That(releasePlanDetails.ReleasePlanId, Is.EqualTo(50001));
            Assert.That(releasePlanDetails.SpecAPIVersion, Is.EqualTo("2026-05-02-preview"));
            Assert.IsNotNull(releaseplan.Message, "Response should contain a message about existing plan");
            Assert.That(releaseplan.Message, Does.Contain("existing release plan"), "Message should indicate plan already exists");
            Assert.That(releaseplan.Warnings, Has.Some.Contains("Release plan 50001").And.Contains("past due"));
            Assert.That(((MockDevOpsService)devOpsService).LastApiReleaseTypeForTypeSpecPathAndApiVersion, Is.EqualTo(ApiReleaseType.GA));
        }

        [Test]
        public async Task Test_Create_releasePlan_creates_new_plan_when_project_plan_has_different_api_version()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var existingReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 999,
                ReleasePlanId = 50001,
                APISpecProjectPath = "specification/testcontoso/Contoso.Management",
                SpecAPIVersion = "2025-01-01",
                ApiReleaseType = ApiReleaseType.GA,
                Status = "In Progress"
            };
            var mockDevOpsService = (MockDevOpsService)devOpsService;
            mockDevOpsService.ConfiguredReleasePlanForTypeSpecPath = existingReleasePlan;
            mockDevOpsService.ConfiguredReleasePlanForTypeSpecPathKey = existingReleasePlan.APISpecProjectPath;

            var releaseplan = await releasePlanTool.CreateReleasePlan(
                null,
                testCodeFilePath,
                "July 2025",
                "GA",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                isTestReleasePlan: true);

            Assert.That(releaseplan.ResponseError, Is.Null);
            Assert.That(releaseplan.ReleasePlanDetails, Is.Not.Null);
            Assert.That(releaseplan.ReleasePlanDetails?.WorkItemId, Is.EqualTo(1), "A new release plan should be created.");
            Assert.That(releaseplan.ReleasePlanDetails?.SpecAPIVersion, Is.EqualTo("2026-05-02-preview"));
        }

        [Test]
        public async Task Test_Create_releasePlan_without_spec_pr_sets_empty_spec_pull_requests()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            ((MockDevOpsService)devOpsService).ConfiguredActiveReleasePlansForTypeSpecPath =
            [
                new ReleasePlanWorkItem { WorkItemId = 801, ReleasePlanId = 81, SDKReleaseMonth = "January 2020" }
            ];
            var releaseplan = await releasePlanTool.CreateReleasePlan(null, 
                testCodeFilePath,
                "July 2025",
                "GA",
                serviceTreeId: "87654321-4321-8765-1234-210987654321",
                productTreeId: "12345678-1234-5678-9012-123456789012",
                isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            var releasePlanDetails = releaseplan.ReleasePlanDetails as ReleasePlanWorkItem;
            Assert.IsNotNull(releasePlanDetails);
            Assert.That(releasePlanDetails.SpecPullRequests, Is.Empty);
            Assert.That(releaseplan.Warnings, Is.Null);
        }

        [Test]
        public async Task Test_Create_releasePlan_allows_different_release_type_for_same_spec_pr()
        {
            // Arrange: configure the mock to return an existing GA release plan for the spec PR
            var mockDevOpsService = new MockDevOpsService
            {
                ConfiguredReleasePlanForSpecPrUrl = new ReleasePlanWorkItem
                {
                    WorkItemId = 42,
                    ReleasePlanId = 10,
                    Title = "Existing GA Release Plan",
                    ReleasePlanType = "GA" // ApiReleaseType.GA
                }
            };

            var tool = new ReleasePlanTool(
                mockDevOpsService,
                gitHelper,
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelper,
                inputSanitizer,
                httpClient,
                Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), _timeProvider);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var specPrUrl = "https://github.com/Azure/azure-rest-api-specs/pull/35446";

            // Act: create a Public Preview plan using the same spec PR (should succeed because release type differs)
            var releaseplan = await tool.CreateReleasePlan(null, 
                testCodeFilePath,
                "July 2025",
                "Public Preview",
                specPullRequestUrl: specPrUrl,
                isTestReleasePlan: true);

            // Assert: creation succeeds
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError, $"Unexpected error: {releaseplan.ResponseError}");
            Assert.IsNotNull(releaseplan.ReleasePlanDetails);
            Assert.Greater(releaseplan.ReleasePlanDetails.WorkItemId, 0);
        }

        [Test]
        public async Task Test_Create_releasePlan_blocks_same_release_type_for_same_spec_pr()
        {
            // Arrange: configure the mock to return an existing GA release plan for the spec PR
            var mockDevOpsService = new MockDevOpsService
            {
                ConfiguredReleasePlanForSpecPrUrl = new ReleasePlanWorkItem
                {
                    WorkItemId = 42,
                    ReleasePlanId = 10,
                    Title = "Existing GA Release Plan",
                    ReleasePlanType = "GA" // ApiReleaseType.GA
                }
            };

            var tool = new ReleasePlanTool(
                mockDevOpsService,
                gitHelper,
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelper,
                inputSanitizer,
                httpClient,
                Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), _timeProvider);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var specPrUrl = "https://github.com/Azure/azure-rest-api-specs/pull/35446";

            // Act: attempt to create another GA plan for the same spec PR (should be blocked)
            var releaseplan = await tool.CreateReleasePlan(null, 
                testCodeFilePath,
                "July 2025",
                "GA",
                specPullRequestUrl: specPrUrl,
                isTestReleasePlan: true);

            // Assert: returns existing plan with a message instead of creating a new one
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError);
            Assert.That(releaseplan.Message, Does.Contain("release plan already exists"));
            Assert.That(releaseplan.ReleasePlanDetails?.WorkItemId, Is.EqualTo(42));
        }

        
        [Test]
        public async Task Test_Get_Release_Plan_by_spec_pull_request_url()
        {
            var releaseplan = await releasePlanTool.GetReleasePlan(specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446");
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError);
            Assert.IsNotNull(releaseplan.ReleasePlanDetails);
            Assert.That(releaseplan.Message, Does.Contain("Successfully retrieved release plan"));
        }

        [TestCase("work-item")]
        [TestCase("release-plan")]
        [TestCase("spec-pr")]
        [TestCase("typespec")]
        [TestCase("api-version")]
        public async Task Test_Get_Release_Plan_omits_sdk_pr_status_without_fetching_github_for_each_identifier(string identifier)
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            const string specPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/35446";
            const string apiVersion = "2026-07-03-preview";
            (string Language, string Repository, string StoredStatus)[] sdks =
            [
                (".NET", "azure-sdk-for-net", "Merged"),
                ("JavaScript", "azure-sdk-for-js", "Ready for review"),
                ("Python", "azure-sdk-for-python", "Failed to generate SDK"),
                ("Java", "azure-sdk-for-java", "Closed"),
                ("Go", "azure-sdk-for-go", "")
            ];
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 777,
                ReleasePlanId = 77,
                // The missing project path produces existing spec guidance that must be retained.
                ActiveSpecPullRequest = specPullRequestUrl,
                SDKInfo = sdks.Select(sdk => new SDKInfo
                {
                    Language = sdk.Language,
                    SdkPullRequestUrl = $"https://github.com/Azure/{sdk.Repository}/pull/1",
                    PullRequestStatus = sdk.StoredStatus,
                    GenerationStatus = "Completed",
                    ReleaseStatus = "Pending"
                }).ToList()
            };
            var mockDevOps = new Mock<IDevOpsService>(MockBehavior.Strict);
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(777, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanAsync(specPullRequestUrl, ApiReleaseType.Unknown, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAsync(typeSpecProjectPath, It.IsAny<bool>(), ApiReleaseType.Unknown, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecProjectPath, apiVersion, ApiReleaseType.PublicPreview, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            var mockGitHub = new Mock<IGitHubService>(MockBehavior.Strict);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, mockGitHub.Object, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var response = identifier switch
            {
                "work-item" => await tool.GetReleasePlan(workItemId: 777),
                "release-plan" => await tool.GetReleasePlan(releasePlanId: 77),
                "spec-pr" => await tool.GetReleasePlan(specPullRequestUrl: specPullRequestUrl),
                "typespec" => await tool.GetReleasePlan(typeSpecProjectPath: typeSpecProjectPath),
                _ => await tool.GetReleasePlan(typeSpecProjectPath: typeSpecProjectPath, apiReleaseType: "Public Preview", apiVersion: apiVersion)
            };

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.Warnings, Is.Null);
            Assert.That(response.NextSteps, Has.Some.Contains("set the TypeSpec project path"));
            Assert.That(response.NextSteps, Has.Some.Contains("release plan dashboard").And.Contains("not current PR status"));
            Assert.That(new OutputHelper(OutputHelper.OutputModes.Plain).Format(response), Does.Contain("release plan dashboard").And.Contain(response.ReleasePlanLink));

            using var document = JsonDocument.Parse(new OutputHelper(OutputHelper.OutputModes.Json).Format(response));
            var sdkDetails = document.RootElement.GetProperty("release_plan_details").GetProperty("SDKInfo");
            Assert.That(sdkDetails.GetArrayLength(), Is.EqualTo(sdks.Length));
            for (var index = 0; index < sdks.Length; index++)
            {
                Assert.That(sdkDetails[index].TryGetProperty("PullRequestStatus", out _), Is.False);
                Assert.That(sdkDetails[index].GetProperty("SdkPullRequestUrl").GetString(), Is.EqualTo(releasePlan.SDKInfo[index].SdkPullRequestUrl));
                Assert.That(sdkDetails[index].GetProperty("GenerationStatus").GetString(), Is.EqualTo("Completed"));
                Assert.That(sdkDetails[index].GetProperty("ReleaseStatus").GetString(), Is.EqualTo("Pending"));
                Assert.That(response.ReleasePlanDetails!.SDKInfo[index].PullRequestStatus, Is.EqualTo(sdks[index].StoredStatus), "Stored PR status remains available to internal release-plan selection.");
            }
            Assert.That(mockDevOps.Invocations, Has.Count.EqualTo(1), "Reading the plan must not write SDK PR status back to Azure DevOps.");
            mockGitHub.VerifyNoOtherCalls();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public async Task Test_Get_Release_Plan_omits_stored_pr_status_even_without_pr_url(string? prUrl)
        {
            var sdk = new SDKInfo
            {
                Language = "Python",
                SdkPullRequestUrl = prUrl!,
                PullRequestStatus = "Failed to generate SDK",
                GenerationStatus = "Completed"
            };
            var mockGitHub = new Mock<IGitHubService>(MockBehavior.Strict);
            var mockDevOps = new Mock<IDevOpsService>(MockBehavior.Strict);
            mockDevOps.Setup(x => x.GetReleasePlanAsync(77, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReleasePlanWorkItem { ReleasePlanId = 77, SDKInfo = [sdk] });
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, mockGitHub.Object, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var response = await tool.GetReleasePlan(releasePlanId: 77);

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.Warnings, Is.Null);
            Assert.That(response.NextSteps, Is.Null);
            Assert.That(response.ReleasePlanDetails!.SDKInfo.Single().PullRequestStatus, Is.EqualTo("Failed to generate SDK"));
            Assert.That(response.ReleasePlanDetails.SDKInfo.Single().GenerationStatus, Is.EqualTo("Completed"));
            using var document = JsonDocument.Parse(new OutputHelper(OutputHelper.OutputModes.Mcp).Format(response));
            Assert.That(document.RootElement.GetProperty("release_plan_details").GetProperty("SDKInfo")[0].TryGetProperty("PullRequestStatus", out _), Is.False);
            mockGitHub.VerifyNoOtherCalls();
            mockDevOps.Verify(x => x.GetReleasePlanAsync(77, It.IsAny<CancellationToken>()), Times.Once);
            mockDevOps.VerifyNoOtherCalls();
        }

        [Test]
        public async Task Test_Get_Release_Plan_by_typespec_project_path()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var expectedReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 777,
                ReleasePlanId = 77,
                IsDataPlane = true
            };
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAsync("specification/testcontoso/Contoso.Management", It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedReleasePlan);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var releaseplan = await tool.GetReleasePlan(typeSpecProjectPath: "specification/testcontoso/Contoso.Management");
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError);
            Assert.IsNotNull(releaseplan.ReleasePlanDetails);
            Assert.That(releaseplan.ReleasePlanDetails.WorkItemId, Is.EqualTo(777));
        }

        [Test]
        public async Task Test_Get_Release_Plan_by_typespec_project_path_and_api_version()
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            const string apiVersion = "2024-01-01";
            var mockDevOps = new Mock<IDevOpsService>();
            var expectedReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 778,
                ReleasePlanId = 78,
                SpecAPIVersion = apiVersion
            };
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecProjectPath, apiVersion, ApiReleaseType.GA, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedReleasePlan);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var response = await tool.GetReleasePlan(typeSpecProjectPath: typeSpecProjectPath, apiReleaseType: "GA", apiVersion: apiVersion);

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.ReleasePlanDetails?.WorkItemId, Is.EqualTo(778));
            mockDevOps.Verify(x => x.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecProjectPath, apiVersion, ApiReleaseType.GA, It.IsAny<CancellationToken>()), Times.Once);
            mockDevOps.Verify(x => x.GetReleasePlanByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Test_Get_Release_Plan_by_spec_pull_request_and_api_version_uses_typespec_version_lookup()
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            const string specPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/35446";
            const string apiVersion = "2024-01-01-preview";
            var mockDevOps = new Mock<IDevOpsService>();
            var expectedReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 779,
                ReleasePlanId = 79,
                SpecAPIVersion = apiVersion
            };
            string? capturedTypeSpecProjectPath = null;
            string? capturedApiVersion = null;
            ApiReleaseType capturedApiReleaseType = ApiReleaseType.Unknown;
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .Callback((string path, string version, ApiReleaseType releaseType, CancellationToken _) =>
                {
                    capturedTypeSpecProjectPath = path;
                    capturedApiVersion = version;
                    capturedApiReleaseType = releaseType;
                })
                .ReturnsAsync(expectedReleasePlan);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var response = await tool.GetReleasePlan(specPullRequestUrl: specPullRequestUrl, typeSpecProjectPath: typeSpecProjectPath, apiReleaseType: "Public Preview", apiVersion: apiVersion);

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.ReleasePlanDetails?.WorkItemId, Is.EqualTo(779));
            Assert.That(capturedTypeSpecProjectPath, Is.EqualTo(typeSpecProjectPath));
            Assert.That(capturedApiVersion, Is.EqualTo(apiVersion));
            Assert.That(capturedApiReleaseType, Is.EqualTo(ApiReleaseType.PublicPreview));
            mockDevOps.Verify(x => x.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecProjectPath, apiVersion, ApiReleaseType.PublicPreview, It.IsAny<CancellationToken>()), Times.Once);
            mockDevOps.Verify(x => x.GetReleasePlanAsync(It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Test_Get_Release_Plan_with_api_version_requires_typespec_project_path()
        {
            var response = await releasePlanTool.GetReleasePlan(
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                apiVersion: "2024-01-01");

            Assert.That(response.ResponseError, Does.Contain("TypeSpec project path is required"));
        }

        [Test]
        public async Task Test_Get_Release_Plan_with_api_version_requires_api_release_type()
        {
            var response = await releasePlanTool.GetReleasePlan(
                typeSpecProjectPath: "specification/testcontoso/Contoso.Management",
                apiVersion: "2024-01-01");

            Assert.That(response.ResponseError, Does.Contain("API release type is required"));
        }

        [Test]
        public async Task Test_Get_Release_Plan_with_api_version_rejects_invalid_api_release_type()
        {
            var response = await releasePlanTool.GetReleasePlan(
                typeSpecProjectPath: "specification/testcontoso/Contoso.Management",
                apiReleaseType: "invalid",
                apiVersion: "2024-01-01");

            Assert.That(response.ResponseError, Does.Contain("Invalid API release type"));
        }

        [Test]
        public async Task Test_Get_Release_Plan_warns_about_project_schedule_risks()
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            var mockDevOps = new Mock<IDevOpsService>();
            var expectedReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 777,
                ReleasePlanId = 77,
                APISpecProjectPath = typeSpecProjectPath,
                SDKReleaseMonth = "October 2026"
            };
            var pastDuePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 801,
                ReleasePlanId = 81,
                APISpecProjectPath = typeSpecProjectPath,
                SDKReleaseMonth = "August 2026",
                SDKInfo =
                [
                    new SDKInfo
                    {
                        Language = "Python",
                        SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/1",
                        PullRequestStatus = "Open"
                    }
                ]
            };
            var dueSoonPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 802,
                ReleasePlanId = 82,
                APISpecProjectPath = typeSpecProjectPath,
                SDKReleaseMonth = "September 2026"
            };
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAsync(typeSpecProjectPath, It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedReleasePlan);
            mockDevOps.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(typeSpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>()))
                .ReturnsAsync([pastDuePlan, dueSoonPlan, expectedReleasePlan]);
            var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero));
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), timeProvider);

            var response = await tool.GetReleasePlan(typeSpecProjectPath: typeSpecProjectPath);

            Assert.That(response.Warnings, Has.Some.Contains("Release plan 81").And.Contains("past due"));
            Assert.That(response.Warnings, Has.Some.Contains("Release plan 82").And.Contains("October 1, 2026"));
            Assert.That(response.Warnings, Has.None.Contains("Release plan 77"));
            Assert.That(response.NextSteps, Has.Some.Contains("azsdk_update_release_plan_target"));
            Assert.That(response.NextSteps, Has.Some.Contains("abandon"));
        }

        [TestCase("work-item")]
        [TestCase("release-plan")]
        [TestCase("spec-pr")]
        public async Task Test_Get_Release_Plan_warns_for_each_identifier(string identifier)
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            const string specPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/35446";
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 777,
                ReleasePlanId = 77,
                APISpecProjectPath = typeSpecProjectPath,
                SDKReleaseMonth = "August 2026"
            };
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(releasePlan.WorkItemId, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanAsync(releasePlan.ReleasePlanId, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanAsync(specPullRequestUrl, ApiReleaseType.Unknown, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(typeSpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>())).ReturnsAsync([releasePlan]);
            var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero));
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), timeProvider);

            var response = identifier switch
            {
                "work-item" => await tool.GetReleasePlan(workItemId: releasePlan.WorkItemId),
                "release-plan" => await tool.GetReleasePlan(releasePlanId: releasePlan.ReleasePlanId),
                _ => await tool.GetReleasePlan(specPullRequestUrl: specPullRequestUrl)
            };

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.Warnings, Has.Some.Contains("Release plan 77").And.Contains("past due"));
        }

        [TestCase(23, false)]
        [TestCase(24, true)]
        public async Task Test_Get_Release_Plan_due_soon_window_is_inclusive(int dayOfMonth, bool expectedWarning)
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            var mockDevOps = new Mock<IDevOpsService>();
            var requestedPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 777,
                ReleasePlanId = 77,
                APISpecProjectPath = typeSpecProjectPath,
                SDKReleaseMonth = "October 2026"
            };
            var septemberPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 802,
                ReleasePlanId = 82,
                APISpecProjectPath = typeSpecProjectPath,
                SDKReleaseMonth = "September 2026"
            };
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAsync(typeSpecProjectPath, It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>())).ReturnsAsync(requestedPlan);
            mockDevOps.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(typeSpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>())).ReturnsAsync([requestedPlan, septemberPlan]);
            var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, dayOfMonth, 0, 0, 0, TimeSpan.Zero));
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), timeProvider);

            var response = await tool.GetReleasePlan(typeSpecProjectPath: typeSpecProjectPath);

            Assert.That(response.Warnings?.Any(warning => warning.Contains("Release plan 82", StringComparison.Ordinal)) ?? false, Is.EqualTo(expectedWarning));
        }

        [Test]
        public async Task Test_Get_Release_Plan_reports_schedule_scan_failure_as_warning()
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem { WorkItemId = 777, APISpecProjectPath = typeSpecProjectPath };
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAsync(typeSpecProjectPath, It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(typeSpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("query failed"));
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var response = await tool.GetReleasePlan(typeSpecProjectPath: typeSpecProjectPath);

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.Warnings, Has.Some.Contains("Unable to check other release plans"));
        }

        [Test]
        public void Test_Get_Release_Plan_propagates_schedule_scan_cancellation()
        {
            const string typeSpecProjectPath = "specification/testcontoso/Contoso.Management";
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem { WorkItemId = 777, APISpecProjectPath = typeSpecProjectPath };
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAsync(typeSpecProjectPath, It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetActiveReleasePlansByTypeSpecProjectPathAsync(typeSpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            Assert.ThrowsAsync<OperationCanceledException>(async () => await tool.GetReleasePlan(typeSpecProjectPath: typeSpecProjectPath));
        }

        [Test]
        public async Task Test_Get_Release_Plan_by_absolute_typespec_project_path()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var expectedReleasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 777,
                ReleasePlanId = 77,
                IsDataPlane = true
            };
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAsync("specification/testcontoso/Contoso.Management", It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedReleasePlan);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var absolutePath = Path.GetFullPath("TypeSpecTestData/specification/testcontoso/Contoso.Management");
            var releaseplan = await tool.GetReleasePlan(typeSpecProjectPath: absolutePath);
            Assert.IsNotNull(releaseplan);
            Assert.IsNull(releaseplan.ResponseError);
            Assert.IsNotNull(releaseplan.ReleasePlanDetails);
            Assert.That(releaseplan.ReleasePlanDetails.WorkItemId, Is.EqualTo(777));
        }

        [Test]
        public async Task Test_Get_Release_Plan_with_no_params_returns_error()
        {
            var releaseplan = await releasePlanTool.GetReleasePlan();
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.That(releaseplan.ResponseError, Does.Contain("At least one of the following options must be provided"));
        }

        [Test]
        public async Task Test_GetReleasePlan_by_typespec_path_with_no_matching_plan_returns_error()
        {
            var result = await releasePlanTool.GetReleasePlan(typeSpecProjectPath: "specification/nonexistent/Service");
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Failed to get release plan details"));
        }

        private sealed class FixedTimeProvider(DateTimeOffset now, TimeZoneInfo? localTimeZone = null) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;

            public override TimeZoneInfo LocalTimeZone => localTimeZone ?? TimeZoneInfo.Utc;
        }

        [Test]
        public async Task Test_Update_SDK_Details_In_Release_Plan()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "Azure.ResourceManager.Contoso", Language = SdkLanguage.DotNet },
                new() { PackageName = "azure-mgmt-contoso", Language = SdkLanguage.Python },
                new() { PackageName = "com.azure.resourcemanager.contoso", Language = SdkLanguage.Java },
                new() { PackageName = "@azure/arm-contoso", Language = SdkLanguage.JavaScript },
                new() { PackageName = "sdk/resourcemanager/contoso/armcontoso", Language = SdkLanguage.Go }
            };
            var tool = CreateReleasePlanToolWithMockedTypeSpec(testCodeFilePath, project);
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_accepts_azure_rest_JavaScript_package()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages =
            [
                new PackageInfo { PackageName = "@azure-rest/ai-content-safety", Language = SdkLanguage.JavaScript }
            ];

            var tool = CreateReleasePlanToolWithMockedTypeSpec(testCodeFilePath, project);
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);

            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain("Language: JavaScript, Package name: @azure-rest/ai-content-safety"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_Mgmt_language_excl()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "Azure.ResourceManager.Contoso", Language = SdkLanguage.DotNet },
                new() { PackageName = "@azure/arm-contoso", Language = SdkLanguage.JavaScript }
            };
            var tool = CreateReleasePlanToolWithMockedTypeSpec(testCodeFilePath, project);
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));
            Assert.That(updateStatus.Message, Does.Contain("Important: The following languages have missing emitter configuration in the TypeSpec project:"));
            Assert.True(updateStatus.NextSteps?.Contains("Configure the TypeSpec emitter for missing languages in tspconfig.yaml, or provide a justification for language exclusion.") ?? false);
        }

        [Test]
        public async Task Test_Update_SDK_Details_Data_language_excl()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "Azure.Contoso", Language = SdkLanguage.DotNet },
                new() { PackageName = "@azure/contoso", Language = SdkLanguage.JavaScript }
            };
            var tool = CreateReleasePlanToolWithMockedTypeSpec(testCodeFilePath, project);
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(1001, testCodeFilePath, CancellationToken.None);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));
            Assert.That(updateStatus.Message, Does.Contain("Important: The following languages have missing emitter configuration in the TypeSpec project:"));
            Assert.That(updateStatus.NextSteps?.Contains("Configure the TypeSpec emitter for missing languages in tspconfig.yaml, or provide a justification for language exclusion.") ?? false);
        }

        [Test]
        public async Task Test_Update_SDK_Details_sets_MissingEmitterConfig_status_for_missing_languages()
        {
            // When a required language has no emitter configuration in the TypeSpec project,
            // the tool must set ReleaseExclusionStatus to "MissingEmitterConfig" (not "Requested")
            // so that the dashboard can display a distinct "Missing emitter configuration" label.
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "Azure.ResourceManager.Contoso", Language = SdkLanguage.DotNet },
                new() { PackageName = "@azure/arm-contoso", Language = SdkLanguage.JavaScript }
            };

            Dictionary<string, string>? capturedFields = null;
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 100,
                ReleasePlanId = 1,
                IsManagementPlane = true
            };
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((_, fields, _) => capturedFields = fields)
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 100 });

            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(testCodeFilePath)).Returns(true);
            mockTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(testCodeFilePath, It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(project);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, mockTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);

            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain("missing emitter configuration"));
            // The work item must be updated with "MissingEmitterConfig" (not "Requested") for languages
            // that do not have an emitter entry in the TypeSpec project.
            Assert.IsNotNull(capturedFields, "UpdateWorkItemAsync should have been called");
            Assert.That(capturedFields!.Values, Has.All.EqualTo("MissingEmitterConfig"));
            Assert.That(capturedFields.Values, Does.Not.Contain("Requested"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_preserves_requested_and_approved_exclusion_status_for_missing_languages()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "Azure.ResourceManager.Contoso", Language = SdkLanguage.DotNet },
                new() { PackageName = "@azure/arm-contoso", Language = SdkLanguage.JavaScript }
            };

            Dictionary<string, string>? capturedFields = null;
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 100,
                ReleasePlanId = 1,
                IsManagementPlane = true,
                SDKInfo =
                [
                    new SDKInfo { Language = "Python", ReleaseExclusionStatus = "Requested" },
                    new SDKInfo { Language = "Go", ReleaseExclusionStatus = "Approved" }
                ]
            };
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((_, fields, _) => capturedFields = fields)
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 100 });

            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(testCodeFilePath)).Returns(true);
            mockTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(testCodeFilePath, It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(project);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, mockTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);

            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain("[Java]"));
            Assert.IsNotNull(capturedFields, "UpdateWorkItemAsync should have been called");
            Assert.That(capturedFields, Has.Count.EqualTo(1));
            Assert.That(capturedFields, Contains.Key("Custom.ReleaseExclusionStatusForJava"));
            Assert.That(capturedFields!["Custom.ReleaseExclusionStatusForJava"], Is.EqualTo("MissingEmitterConfig"));
            Assert.That(capturedFields.ContainsKey("Custom.ReleaseExclusionStatusForPython"), Is.False);
            Assert.That(capturedFields.ContainsKey("Custom.ReleaseExclusionStatusForGo"), Is.False);
        }

        [Test]
        public async Task Test_Update_SDK_Details_marks_empty_package_name_as_missing_emitter_config()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "Azure.ResourceManager.Contoso", Language = SdkLanguage.DotNet },
                new() { PackageName = "", Language = SdkLanguage.Java },
                new() { PackageName = "azure-mgmt-contoso", Language = SdkLanguage.Python },
                new() { PackageName = "@azure/arm-contoso", Language = SdkLanguage.JavaScript },
                new() { PackageName = "sdk/resourcemanager/contoso/armcontoso", Language = SdkLanguage.Go }
            };

            Dictionary<string, string>? capturedFields = null;
            List<SDKInfo>? capturedSdkInfos = null;
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 100,
                ReleasePlanId = 1,
                IsManagementPlane = true
            };
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>()))
                .Callback<int, List<SDKInfo>, CancellationToken>((_, sdkInfos, _) => capturedSdkInfos = sdkInfos)
                .ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((_, fields, _) => capturedFields = fields)
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 100 });

            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(testCodeFilePath)).Returns(true);
            mockTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(testCodeFilePath, It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(project);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, mockTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);

            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain("[Java]"));
            Assert.That(capturedSdkInfos, Has.Count.EqualTo(4));
            Assert.That(capturedSdkInfos!.Select(x => x.Language), Does.Not.Contain("Java"));
            Assert.IsNotNull(capturedFields, "UpdateWorkItemAsync should have been called");
            Assert.That(capturedFields, Has.Count.EqualTo(1));
            Assert.That(capturedFields, Contains.Key("Custom.ReleaseExclusionStatusForJava"));
            Assert.That(capturedFields!["Custom.ReleaseExclusionStatusForJava"], Is.EqualTo("MissingEmitterConfig"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_marks_all_required_languages_missing_emitter_config_when_no_package_names_are_resolved()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "", Language = SdkLanguage.DotNet },
                new() { PackageName = "", Language = SdkLanguage.Java },
                new() { PackageName = "", Language = SdkLanguage.Python },
                new() { PackageName = "", Language = SdkLanguage.JavaScript },
                new() { PackageName = "", Language = SdkLanguage.Go }
            };

            Dictionary<string, string>? capturedFields = null;
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 100,
                ReleasePlanId = 1,
                IsManagementPlane = true,
                SDKInfo = []
            };
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((_, fields, _) => capturedFields = fields)
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 100 });

            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(testCodeFilePath)).Returns(true);
            mockTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(testCodeFilePath, It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(project);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, mockTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);

            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain("missing emitter configuration"));
            Assert.IsNotNull(capturedFields, "UpdateWorkItemAsync should have been called");
            Assert.That(capturedFields, Has.Count.EqualTo(5));
            Assert.That(capturedFields!.Values, Has.All.EqualTo("MissingEmitterConfig"));
            mockDevOps.Verify(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Test_Update_SDK_Details_Data_optional_go()
        {
            // Data plane only requires .NET, Java, Python and JavaScript, but Go is optional.
            // When a data plane TypeSpec project also emits Go, the tool must not fail with an
            // "Unsupported SDK language" error and should still update the Go package name.
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "Azure.Contoso", Language = SdkLanguage.DotNet },
                new() { PackageName = "azure-contoso", Language = SdkLanguage.Python },
                new() { PackageName = "com.azure.contoso", Language = SdkLanguage.Java },
                new() { PackageName = "@azure/contoso", Language = SdkLanguage.JavaScript },
                new() { PackageName = "sdk/contoso/azcontoso", Language = SdkLanguage.Go }
            };
            var tool = CreateReleasePlanToolWithMockedTypeSpec(testCodeFilePath, project);
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(1001, testCodeFilePath, CancellationToken.None);
            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));
            Assert.That(updateStatus.Message, Does.Contain("Language: Go, Package name: sdk/contoso/azcontoso"));
            // Go is optional for data plane, so it must not be reported as an excluded language.
            Assert.That(updateStatus.Message, Does.Not.Contain("excluded"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_Data_skips_untracked_language()
        {
            // A data plane TypeSpec project may also emit packages for languages the release plan
            // does not track (e.g. Rust). The tool must not fail; it should update the supported
            // languages and skip the untracked ones, reporting them in the message.
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = "Azure.Contoso", Language = SdkLanguage.DotNet },
                new() { PackageName = "azure-contoso", Language = SdkLanguage.Python },
                new() { PackageName = "com.azure.contoso", Language = SdkLanguage.Java },
                new() { PackageName = "@azure/contoso", Language = SdkLanguage.JavaScript },
                new() { PackageName = "sdk/contoso/azcontoso", Language = SdkLanguage.Go },
                new() { PackageName = "azure_contoso", Language = SdkLanguage.Rust }
            };
            var tool = CreateReleasePlanToolWithMockedTypeSpec(testCodeFilePath, project);
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(1001, testCodeFilePath, CancellationToken.None);
            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));
            Assert.That(updateStatus.Message, Does.Contain("Language: Go, Package name: sdk/contoso/azcontoso"));
            // Rust is not tracked by the data plane release plan and must be skipped, not fail.
            Assert.That(updateStatus.Message, Does.Contain("skipped: Rust"));
            Assert.That(updateStatus.Message, Does.Not.Contain("Language: Rust"));
        }

        [TestCase("Javascript", "@contoso/ai-content-safety", "JavaScript")]
        [TestCase("Go", "contoso/ai-content-safety", "Go")]
        [Test]
        public async Task Test_Update_SDK_Details_uses_TypeSpec_package_name_without_prefix_validation(string language, string package, string expectedLanguage)
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            var sdkLanguage = SdkLanguageHelpers.GetSdkLanguage(language);
            project.Packages = new List<PackageInfo>
            {
                new() { PackageName = package, Language = sdkLanguage }
            };
            var tool = CreateReleasePlanToolWithMockedTypeSpec(testCodeFilePath, project);
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);
            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain($"Language: {expectedLanguage}, Package name: {package}"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_invalid_typespec_path()
        {
            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(false);
            var tool = new ReleasePlanTool(devOpsService, gitHelper, mockTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, "/nonexistent/path", CancellationToken.None);
            Assert.That(updateStatus.ResponseError, Does.Contain("invalid"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_parse_failure()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);
            mockTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(It.IsAny<string>(), It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TypeSpecProject?)null);
            var tool = new ReleasePlanTool(devOpsService, gitHelper, mockTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);
            Assert.That(updateStatus.ResponseError, Does.Contain("Failed to parse TypeSpec project"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_skips_update_when_no_packages_resolved()
        {
            // When the TypeSpec project resolves zero packages, the tool must skip the update entirely
            // to avoid incorrectly marking languages as having missing emitter configuration. It should
            // return an informational message and never touch the release plan work item.
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var project = TypeSpecProject.ParseTypeSpecConfig(testCodeFilePath);
            project.Packages = new List<PackageInfo>();

            var mockDevOps = new Mock<IDevOpsService>();
            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(testCodeFilePath)).Returns(true);
            mockTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(testCodeFilePath, It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(project);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, mockTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
            var updateStatus = await tool.UpdateSDKDetailsInReleasePlan(100, testCodeFilePath, CancellationToken.None);

            Assert.That(updateStatus.ResponseError, Is.Null);
            Assert.That(updateStatus.Message, Does.Contain("No package details were identified"));
            // No release plan reads or writes should happen when there are no packages to update.
            mockDevOps.Verify(x => x.ResolveReleasePlanByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            mockDevOps.Verify(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>()), Times.Never);
            mockDevOps.Verify(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        
        [Test]
        public async Task Test_update_language_exclusion_justification()
        {
            var updateStatus = await releasePlanTool.UpdateLanguageExclusionJustification(100, "This is a test justification for excluding certain languages.");
            Assert.That(updateStatus.Message, Does.Contain("Updated language exclusion justification in release plan"));
        }

        [TestCase("Python", "https://github.com/Azure/azure-sdk-for-python/pull/12345")]
        [TestCase(".NET", "https://github.com/Azure/azure-sdk-for-net/pull/12345")]
        [TestCase("dotnet", "https://github.com/Azure/azure-sdk-for-net/pull/12345")]
        [TestCase("Dotnet", "https://github.com/Azure/azure-sdk-for-net/pull/12345")]
        [TestCase("DotNet", "https://github.com/Azure/azure-sdk-for-net/pull/12345")]
        [TestCase("csharp", "https://github.com/Azure/azure-sdk-for-net/pull/12345")]
        [TestCase("Javascript", "https://github.com/Azure/azure-sdk-for-js/pull/12345")]
        [TestCase("typescript", "https://github.com/Azure/azure-sdk-for-js/pull/12345")]
        [TestCase("Java", "https://github.com/Azure/azure-sdk-for-java/pull/12345")]
        [TestCase("Go", "https://github.com/Azure/azure-sdk-for-go/pull/12345")]
        [Test]
        public async Task Test_link_sdk_pull_request_to_release_plan(string language, string pullRequestUrl)
        {
            var response = await releasePlanTool.LinkSdkPullRequestToReleasePlan(language, pullRequestUrl, 1, 1);
            Assert.That(response.Details, Has.Some.Contains("Successfully linked pull request to release plan"), $"Assertion failed for language '{language}' and PR '{pullRequestUrl}'.");
            Assert.That(response.Language, Is.Not.EqualTo(SdkLanguage.Unknown), $"Language property should be set for '{language}'.");
        }

        [Test]
        public async Task Test_link_sdk_pull_request_with_missing_work_item_and_release_plan()
        {
            var response = await releasePlanTool.LinkSdkPullRequestToReleasePlan("Python", "https://github.com/Azure/azure-sdk-for-python/pull/12345", 0, 0);
            Assert.IsNotNull(response.ResponseError);
            Assert.That(response.ResponseError, Does.Contain("Either work item ID or release plan ID is required"));
            Assert.That(response.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task Test_link_sdk_pull_request_with_invalid_language()
        {
            var response = await releasePlanTool.LinkSdkPullRequestToReleasePlan("InvalidLanguage", "https://github.com/Azure/azure-sdk-for-python/pull/12345", 1, 0);
            Assert.IsNotNull(response.ResponseError);
            Assert.That(response.ResponseError, Does.Contain("Unsupported language"));
            Assert.That(response.Language, Is.EqualTo(SdkLanguage.Unknown));
        }

        [Test]
        public async Task Test_link_sdk_pull_request_with_empty_pull_request_url()
        {
            var response = await releasePlanTool.LinkSdkPullRequestToReleasePlan("Python", "", 1, 0);
            Assert.IsNotNull(response.ResponseError);
            Assert.That(response.ResponseError, Does.Contain("SDK pull request URL is required"));
            Assert.That(response.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task Test_link_sdk_pull_request_with_mismatched_language_and_repo()
        {
            // Trying to link a Java repo PR with Python language
            var response = await releasePlanTool.LinkSdkPullRequestToReleasePlan("Python", "https://github.com/Azure/azure-sdk-for-java/pull/12345", 1, 0);
            Assert.IsNotNull(response.ResponseError);
            Assert.That(response.ResponseError, Does.Contain("Invalid pull request link"));
            Assert.That(response.ResponseError, Does.Contain("azure-sdk-for-python"));
            Assert.That(response.Language, Is.EqualTo(SdkLanguage.Python));
        }

        [Test]
        public async Task Test_update_spec_pull_request_with_work_item_id()
        {
            var response = await releasePlanTool.UpdateSpecPullRequestInReleasePlan("https://github.com/Azure/azure-rest-api-specs/pull/12345", workItemId: 100);
            Assert.IsNotNull(response);
            Assert.That(response.Status, Is.EqualTo("Success"));
            Assert.That(response.Details, Has.Some.Contains("Successfully updated spec pull request URL"));
            Assert.That(response.NextSteps, Has.Some.Contains("SDK generation should be triggered"));
        }

        [Test]
        public async Task Test_update_spec_pull_request_with_release_plan_id()
        {
            var response = await releasePlanTool.UpdateSpecPullRequestInReleasePlan("https://github.com/Azure/azure-rest-api-specs/pull/12345", releasePlanId: 1);
            Assert.IsNotNull(response);
            Assert.That(response.Status, Is.EqualTo("Success"));
            Assert.That(response.Details, Has.Some.Contains("Successfully updated spec pull request URL"));
            Assert.That(response.NextSteps, Has.Some.Contains("SDK generation should be triggered"));
        }

        [Test]
        public async Task Test_update_spec_pull_request_with_no_identifiers()
        {
            var response = await releasePlanTool.UpdateSpecPullRequestInReleasePlan("https://github.com/Azure/azure-rest-api-specs/pull/12345");
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.ResponseError);
            Assert.That(response.ResponseError, Does.Contain("Either work item ID or release plan ID must be provided"));
        }

        [Test]
        public async Task Test_update_spec_pull_request_with_invalid_url()
        {
            var response = await releasePlanTool.UpdateSpecPullRequestInReleasePlan("invalid-url", workItemId: 100);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.ResponseError);
            Assert.That(response.ResponseError, Does.Contain("Invalid spec pull request URL"));
        }

        [Test]
        public async Task Test_update_spec_pull_request_with_non_specs_repo()
        {
            var response = await releasePlanTool.UpdateSpecPullRequestInReleasePlan("https://github.com/Azure/azure-sdk-for-python/pull/12345", workItemId: 100);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.ResponseError);
            Assert.That(response.ResponseError, Does.Contain("Invalid spec pull request URL"));
            Assert.That(response.ResponseError, Does.Contain("azure-rest-api-specs"));
        }

        [Test]
        public async Task Test_list_overdue_release_plans_notify_without_emailer_uri()
        {
            var response = await releasePlanTool.ListOverdueReleasePlans(notifyOwners: true, emailerUri: "");
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.ResponseError);
            Assert.That(response.ResponseError, Does.Contain("Emailer URI is required"));
        }

        [Test]
        public async Task Test_notification_includes_correct_missing_sdks()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var plan = new ReleasePlanWorkItem
            {
                WorkItemId = 200,
                Owner = "Test Owner",
                ReleasePlanSubmittedByEmail = "valid@example.com",
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKReleaseMonth = "January 2026",
                ReleasePlanId = 200,
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = ".NET", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync(It.IsAny<CancellationToken>())).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content?.ReadAsStringAsync(token).Result ?? "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            await tool.ListOverdueReleasePlans(notifyOwners: true, emailerUri: "https://test.com/email");

            Assert.That(capturedBody, Does.Contain("Java"));
            Assert.That(capturedBody, Does.Contain(".NET"));
            Assert.That(capturedBody, Does.Not.Contain("Python")); // Released, should not be in missing list
        }

        [Test]
        public async Task Test_notification_excludes_approved_and_requested_languages()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var plan = new ReleasePlanWorkItem
            {
                WorkItemId = 201,
                Owner = "Test Owner",
                ReleasePlanSubmittedByEmail = "valid@example.com",
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKReleaseMonth = "January 2026",
                ReleasePlanId = 201,
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", ReleaseStatus = "", ReleaseExclusionStatus = "Approved" },
                    new SDKInfo { Language = ".NET", ReleaseStatus = "", ReleaseExclusionStatus = "Requested" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync(It.IsAny<CancellationToken>())).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content?.ReadAsStringAsync(token).Result ?? "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            await tool.ListOverdueReleasePlans(notifyOwners: true, emailerUri: "https://test.com/email");

            Assert.That(capturedBody, Does.Contain("Java"));
            Assert.That(capturedBody, Does.Not.Contain("Python")); // Approved exclusion
            Assert.That(capturedBody, Does.Not.Contain(".NET")); // Requested exclusion
        }

        [Test]
        public async Task Test_notification_excludes_missing_emitter_config_languages()
        {
            // Languages marked "MissingEmitterConfig" by the auto-update tool must not appear
            // in the overdue SDK notification, since the emitter configuration has not been set up.
            var mockDevOps = new Mock<IDevOpsService>();
            var plan = new ReleasePlanWorkItem
            {
                WorkItemId = 203,
                Owner = "Test Owner",
                ReleasePlanSubmittedByEmail = "valid@example.com",
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKReleaseMonth = "January 2026",
                ReleasePlanId = 203,
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", ReleaseStatus = "", ReleaseExclusionStatus = "MissingEmitterConfig" },
                    new SDKInfo { Language = ".NET", ReleaseStatus = "", ReleaseExclusionStatus = "MissingEmitterConfig" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync(It.IsAny<CancellationToken>())).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content is not null
                        ? await request.Content.ReadAsStringAsync(token).ConfigureAwait(false)
                        : "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            await tool.ListOverdueReleasePlans(notifyOwners: true, emailerUri: "https://test.com/email");

            Assert.That(capturedBody, Does.Contain("Java"));
            Assert.That(capturedBody, Does.Not.Contain("Python")); // MissingEmitterConfig
            Assert.That(capturedBody, Does.Not.Contain(".NET")); // MissingEmitterConfig
        }

        [Test]
        public async Task Test_notification_excludes_go_for_dataplane()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var plan = new ReleasePlanWorkItem
            {
                WorkItemId = 202,
                Owner = "Test Owner",
                ReleasePlanSubmittedByEmail = "valid@example.com",
                IsDataPlane = true,
                IsManagementPlane = false,
                SDKReleaseMonth = "January 2026",
                ReleasePlanId = 202,
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync(It.IsAny<CancellationToken>())).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content?.ReadAsStringAsync(token).Result ?? "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            await tool.ListOverdueReleasePlans(notifyOwners: true, emailerUri: "https://test.com/email");

            Assert.That(capturedBody, Does.Contain("Java"));
            Assert.That(capturedBody, Does.Not.Contain("Go")); // Filtered for Data Plane
            Assert.That(capturedBody, Does.Contain("Data Plane"));
        }

        [Test]
        public async Task Test_notification_includes_go_for_management_plane()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var plan = new ReleasePlanWorkItem
            {
                WorkItemId = 203,
                Owner = "Test Owner",
                ReleasePlanSubmittedByEmail = "valid@example.com",
                IsManagementPlane = true,
                IsDataPlane = false,
                SDKReleaseMonth = "January 2026",
                ReleasePlanId = 203,
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync(It.IsAny<CancellationToken>())).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content?.ReadAsStringAsync(token).Result ?? "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            await tool.ListOverdueReleasePlans(notifyOwners: true, emailerUri: "https://test.com/email");

            Assert.That(capturedBody, Does.Contain("Java"));
            Assert.That(capturedBody, Does.Contain("Go")); // Included for Management Plane
            Assert.That(capturedBody, Does.Contain("Management Plane"));
        }

        [Test]
        public async Task Test_FindProduct_with_valid_typespec_path()
        {
            // Arrange
            var typeSpecProjectPath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";

            // Act
            var result = await releasePlanTool.GetProductByTypeSpecPath(typeSpecProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ProductInfo);
            Assert.That(result.ProductInfo.ProductServiceTreeId, Is.EqualTo("12345678-1234-5678-9012-123456789012"));
            Assert.That(result.ProductInfo.ServiceId, Is.EqualTo("87654321-4321-8765-1234-210987654321"));
            Assert.IsNull(result.ResponseError);
        }

        [Test]
        public async Task Test_FindProduct_with_nonexistent_typespec_path()
        {
            // Arrange
            var typeSpecProjectPath = "specification/nonexistent/Service";

            // Act
            var result = await releasePlanTool.GetProductByTypeSpecPath(typeSpecProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ProductInfo);
            Assert.That(result.ResponseError, Does.Contain("Invalid TypeSpec project path. tspconfig.yaml is not found in the path specification/nonexistent/Service."));
        }

        [Test]
        public async Task Test_FindProduct_with_empty_typespec_path()
        {
            // Arrange
            var typeSpecProjectPath = "";

            // Act
            var result = await releasePlanTool.GetProductByTypeSpecPath(typeSpecProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ProductInfo);
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("TypeSpec project path cannot be empty"));
        }

        [Test]
        public async Task Test_Abandon_ReleasePlan_With_WorkItemId_Success()
        {
            // Act
            var result = await releasePlanTool.AbandonReleasePlan(workItemId: 100, releasePlanId: 0);

            // Assert
            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.IsNotNull(result.Details);
            Assert.That(result.Details.Count, Is.GreaterThan(0));
            Assert.That(result.Details[0], Does.Contain("abandoned"));
        }

        [Test]
        public async Task Test_Abandon_ReleasePlan_With_ReleasePlanId_Success()
        {
            // Act
            var result = await releasePlanTool.AbandonReleasePlan(workItemId: 0, releasePlanId: 123);

            // Assert
            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.IsNotNull(result.Details);
            Assert.That(result.Details.Count, Is.GreaterThan(0));
            Assert.That(result.Details[0], Does.Contain("abandoned"));
        }

        [Test]
        public async Task Test_Abandon_ReleasePlan_Without_Ids_ReturnsError()
        {
            // Act
            var result = await releasePlanTool.AbandonReleasePlan(workItemId: 0, releasePlanId: 0);

            // Assert
            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Either work item ID or release plan ID must be provided"));
        }

        [Test]
        public async Task Test_Abandon_ReleasePlan_With_Both_Ids_Success()
        {
            // Act - when both are provided, workItemId takes precedence
            var result = await releasePlanTool.AbandonReleasePlan(workItemId: 100, releasePlanId: 123);

            // Assert
            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.IsNotNull(result.Details);
            Assert.That(result.Details.Count, Is.GreaterThan(0));
            Assert.That(result.Details[0], Does.Contain("abandoned"));
        }

        // ======================== UpdateReleasePlan Tests ========================

        [Test]
        public async Task Test_UpdateReleasePlan_with_invalid_SDK_type()
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "invalid-type",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 100);

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Invalid SDK release type"));
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_invalid_pull_request_url()
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/invalid-repo/pull/35446",
                workItemId: 100);

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Invalid spec pull request URL"));
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_empty_typespec_path()
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 100);

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("TypeSpec project path is required"));
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_tspconfig_path_success()
        {
            var relativeConfigPath = "TypeSpecTestData/specification/testcontoso/Contoso.Management/tspconfig.yaml";
            var absoluteConfigPath = Path.GetFullPath(relativeConfigPath);

            var relativeResult = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: relativeConfigPath,
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 100);

            var absoluteResult = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: absoluteConfigPath,
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 100);

            Assert.Multiple(() =>
            {
                Assert.IsNull(relativeResult.ResponseError, $"Unexpected error for relative tspconfig path: {relativeResult.ResponseError}");
                Assert.IsNull(absoluteResult.ResponseError, $"Unexpected error for absolute tspconfig path: {absoluteResult.ResponseError}");
                Assert.That(relativeResult.TypeSpecProject, Is.EqualTo("specification/testcontoso/Contoso.Management"));
                Assert.That(absoluteResult.TypeSpecProject, Is.EqualTo("specification/testcontoso/Contoso.Management"));
            });
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_invalid_service_tree_id()
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 100,
                serviceTreeId: "not-a-guid");

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Service tree ID 'not-a-guid' is not a valid GUID"));
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_invalid_product_tree_id()
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 100,
                productTreeId: "not-a-guid");

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Product tree ID 'not-a-guid' is not a valid GUID"));
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_work_item_id_success()
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 100);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated release plan"));
            Assert.IsNotNull(result.ReleasePlanDetails);
            Assert.That(result.TypeSpecProject, Does.Contain("specification/testcontoso/Contoso.Management"));
        }

        [Test]
        public async Task Test_UpdateReleasePlan_without_spec_pr()
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                workItemId: 100);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated release plan"));
            Assert.IsNotNull(result.ReleasePlanDetails);
            Assert.That(result.TypeSpecProject, Does.Contain("specification/testcontoso/Contoso.Management"));
        }

        [TestCase("GA", "stable")]
        [TestCase("Preview", "beta")]
        [TestCase("beta", "beta")]
        [TestCase("stable", "stable")]
        [Test]
        public async Task Test_UpdateReleasePlan_maps_sdk_release_type(string inputType, string expectedMapped)
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: inputType,
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 100);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated release plan"));
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_optional_service_and_product_ids()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 200,
                ReleasePlanId = 10,
                IsManagementPlane = true,
                IsDataPlane = false
            };
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(200, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(200, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((id, fields, _) =>
                {
                    // Verify the service and product IDs are included
                    Assert.That(fields.ContainsKey("Custom.ServiceTreeID"));
                    Assert.That(fields["Custom.ServiceTreeID"], Is.EqualTo("11111111-1111-1111-1111-111111111111"));
                    Assert.That(fields.ContainsKey("Custom.ProductServiceTreeID"));
                    Assert.That(fields["Custom.ProductServiceTreeID"], Is.EqualTo("22222222-2222-2222-2222-222222222222"));
                    Assert.That(fields.ContainsKey("Custom.ApiSpecProjectPath"));
                    // Verify product details copied from the triage work item
                    Assert.That(fields["Custom.ProductName"], Is.EqualTo("Contoso Product"));
                    Assert.That(fields["Custom.ProductType"], Is.EqualTo("Offering"));
                    Assert.That(fields["Custom.ProductLifecycle"], Is.EqualTo("GA"));
                })
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 200 });
            mockDevOps.Setup(x => x.UpdateSpecPullRequestAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateApiSpecVersionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.GetProductInfoFromTriageWorkItemAsync("22222222-2222-2222-2222-222222222222", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProductInfo { ProductServiceTreeId = "22222222-2222-2222-2222-222222222222", ProductName = "Contoso Product", ProductType = "Offering", ProductLifecycle = "GA" });

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var result = await tool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 200,
                serviceTreeId: "11111111-1111-1111-1111-111111111111",
                productTreeId: "22222222-2222-2222-2222-222222222222");

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated release plan"));

            mockDevOps.Verify(x => x.UpdateWorkItemAsync(200, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
            mockDevOps.Verify(x => x.UpdateSpecPullRequestAsync(200, "https://github.com/Azure/azure-rest-api-specs/pull/35446", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_product_id_and_no_triage_asks_for_product_type()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 210,
                ReleasePlanId = 11,
                IsManagementPlane = true,
                IsDataPlane = false
            };
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(210, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(210, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            // No triage work item found for the product ID
            mockDevOps.Setup(x => x.GetProductInfoFromTriageWorkItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductInfo?)null);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var result = await tool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                workItemId: 210,
                productTreeId: "22222222-2222-2222-2222-222222222222");

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("Product type could not be determined"));
            // The release plan should not be updated until the product type is provided
            mockDevOps.Verify(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_explicit_product_type_proceeds()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 220,
                ReleasePlanId = 12,
                IsManagementPlane = true,
                IsDataPlane = false
            };
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(220, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(220, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetProductInfoFromTriageWorkItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductInfo?)null);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((id, fields, _) =>
                {
                    Assert.That(fields["Custom.ProductServiceTreeID"], Is.EqualTo("22222222-2222-2222-2222-222222222222"));
                    Assert.That(fields["Custom.ProductType"], Is.EqualTo("Feature"));
                })
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 220 });
            mockDevOps.Setup(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var result = await tool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                workItemId: 220,
                productTreeId: "22222222-2222-2222-2222-222222222222",
                productType: ProductType.Feature);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated release plan"));
            mockDevOps.Verify(x => x.UpdateWorkItemAsync(220, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Test_UpdateReleasePlan_without_service_and_product_ids_does_not_include_them()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 300,
                ReleasePlanId = 10,
                IsDataPlane = true
            };
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(300, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(300, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((id, fields, _) =>
                {
                    Assert.That(fields.ContainsKey("Custom.ServiceTreeID"), Is.False);
                    Assert.That(fields.ContainsKey("Custom.ProductServiceTreeID"), Is.False);
                })
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 300 });
            mockDevOps.Setup(x => x.UpdateSpecPullRequestAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateApiSpecVersionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var result = await tool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "stable",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 300);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            mockDevOps.Verify(x => x.UpdateWorkItemAsync(300, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Test_UpdateReleasePlan_finds_by_pr_url_skipping_path_lookup()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 500,
                ReleasePlanId = 50,
                IsManagementPlane = true
            };
            // Work item ID not provided (0), TypeSpec path lookup returns null, PR URL lookup returns the plan
            mockDevOps.Setup(x => x.GetReleasePlanByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>())).ReturnsAsync((ReleasePlanWorkItem?)null);
            mockDevOps.Setup(x => x.GetReleasePlanAsync("https://github.com/Azure/azure-rest-api-specs/pull/99999", It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 500 });
            mockDevOps.Setup(x => x.UpdateSpecPullRequestAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateApiSpecVersionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(500, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);

            var mockNpxHelper = new Mock<INpxHelper>();
            mockNpxHelper.Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0 });

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, mockNpxHelper.Object, Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var result = await tool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/99999",
                workItemId: 0);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated release plan 500"));
            Assert.That(result.PackageType, Is.EqualTo(SdkType.Management));

            mockDevOps.Verify(x => x.GetReleasePlanByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);
            mockDevOps.Verify(x => x.GetReleasePlanAsync("https://github.com/Azure/azure-rest-api-specs/pull/99999", It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Test_UpdateReleasePlan_with_url_based_typespec_path()
        {
            var result = await releasePlanTool.UpdateReleasePlan(
                typeSpecProjectPath: "https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management",
                sdkReleaseType: "stable",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/39310",
                workItemId: 100);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated release plan"));
            Assert.IsNotNull(result.TypeSpecProject);
        }

        // ======================== Target Month Validation Tests ========================

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t ")]
        [TestCase("2026-10")]
        [TestCase("10/2026")]
        [TestCase("September")]
        [TestCase("September 26")]
        [TestCase("September 0000")]
        [TestCase("September 10000")]
        [TestCase("NotAMonth 2026")]
        [TestCase("Sep 2026")]
        [TestCase("September 17, 2026")]
        [TestCase("2026-09-17")]
        [TestCase("September 2026 extra")]
        public async Task Test_ReleasePlanTools_reject_malformed_target_month_before_side_effects(string? targetMonth)
        {
            var now = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
            var expectedError = string.IsNullOrWhiteSpace(targetMonth) ? "SDK release target month is required" : "Month YYYY";

            await AssertTargetReleaseMonthRejected(targetMonth, now, expectedError);
        }

        [TestCase("2026-09-17T12:00:00Z", "September 2025")]
        [TestCase("2026-09-17T12:00:00Z", "August 2026")]
        [TestCase("2026-09-01T00:00:00Z", "August 2026")]
        [TestCase("2027-01-01T00:00:00Z", "December 2026")]
        [TestCase("2026-12-31T23:59:59Z", "January 2026")]
        [TestCase("2028-02-29T23:59:59Z", "January 2028")]
        [SetCulture("fr-FR")]
        public async Task Test_ReleasePlanTools_reject_past_target_month_before_side_effects(string utcNow, string targetMonth)
        {
            var now = DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture);
            var expectedError = $"{now.ToString("MMMM yyyy", CultureInfo.InvariantCulture)} or later";

            await AssertTargetReleaseMonthRejected(targetMonth, now, expectedError);
        }

        private async Task AssertTargetReleaseMonthRejected(string? targetMonth, DateTimeOffset now, string expectedError)
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var mockTypeSpec = new Mock<ITypeSpecHelper>();
            var mockNotification = new Mock<INotificationService>();
            var mockProgress = new Mock<IProgress<ProgressNotificationValue>>();
            var localTimeZone = TimeZoneInfo.CreateCustomTimeZone("UTC-08", TimeSpan.FromHours(-8), "UTC-08", "UTC-08");
            var clock = new FixedTimeProvider(now, localTimeZone);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, mockTypeSpec.Object, logger, userHelper, gitHubService,
                environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), mockNotification.Object, clock);

            var createResponse = await tool.CreateReleasePlan(mockProgress.Object,
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", targetMonth!, "GA");
            var testCreateResponse = await tool.CreateReleasePlan(mockProgress.Object,
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", targetMonth!, "GA", isTestReleasePlan: true);
            var updateResponse = await tool.UpdateReleasePlanTarget(36557, targetMonth!);

            Assert.Multiple(() =>
            {
                Assert.That(createResponse.ResponseError, Does.Contain(expectedError), "Create must reject the target month.");
                Assert.That(testCreateResponse.ResponseError, Does.Contain(expectedError), "Test plans must use the same validation.");
                Assert.That(updateResponse.ResponseError, Does.Contain(expectedError), "Update must reject the target month.");
                Assert.That(createResponse.ReleasePlanDetails, Is.Null);
                Assert.That(testCreateResponse.ReleasePlanDetails, Is.Null);
                Assert.That(updateResponse.ReleasePlanDetails, Is.Null);
                Assert.That(mockDevOps.Invocations, Is.Empty, "Invalid targets must not trigger work-item reads or writes.");
                Assert.That(mockTypeSpec.Invocations, Is.Empty, "Invalid targets must not trigger TypeSpec processing.");
                Assert.That(mockNotification.Invocations, Is.Empty, "Invalid targets must not send notifications.");
                Assert.That(mockProgress.Invocations, Is.Empty, "Invalid targets must not report creation progress.");
                Assert.That(Mock.Get(userHelper).Invocations, Is.Empty, "Invalid targets must not require authentication.");
            });
        }

        [TestCase("2026-09-01T00:00:00Z", "September 2026")]
        [TestCase("2026-09-17T12:00:00Z", "October 2026")]
        [TestCase("2026-09-30T23:59:59Z", "September 2026")]
        [TestCase("2026-09-30T23:59:59Z", "October 2026")]
        [TestCase("2026-12-31T23:59:59Z", "December 2026")]
        [TestCase("2026-12-31T23:59:59Z", "January 2027")]
        [TestCase("2027-01-01T00:00:00Z", "January 2027")]
        [TestCase("2027-01-01T00:00:00Z", "December 2027")]
        [TestCase("2028-02-29T23:59:59Z", "February 2028")]
        [TestCase("2028-02-29T23:59:59Z", "March 2028")]
        [SetCulture("fr-FR")]
        public async Task Test_ReleasePlanTools_accept_current_or_future_target_month(string utcNow, string targetMonth)
        {
            var now = DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture);
            var localTimeZone = TimeZoneInfo.CreateCustomTimeZone("UTC+14", TimeSpan.FromHours(14), "UTC+14", "UTC+14");
            var clock = new FixedTimeProvider(now, localTimeZone);
            var createTool = new ReleasePlanTool(devOpsService, gitHelper, typeSpecHelper, logger, userHelper, gitHubService,
                environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), clock);

            var createResponse = await createTool.CreateReleasePlan(null,
                "TypeSpecTestData/specification/testcontoso/Contoso.Management", targetMonth, "GA", isTestReleasePlan: true);

            Assert.That(createResponse.ResponseError, Is.Null);
            Assert.That(createResponse.ReleasePlanDetails?.SDKReleaseMonth, Is.EqualTo(targetMonth), "Creation must preserve the requested target.");

            var mockDevOps = new Mock<IDevOpsService>();
            var existingPlan = new ReleasePlanWorkItem { WorkItemId = 36557, ReleasePlanId = 57, SDKReleaseMonth = "September 2025" };
            mockDevOps.Setup(s => s.ResolveReleasePlanByIdAsync(57, It.IsAny<CancellationToken>())).ReturnsAsync(existingPlan);
            mockDevOps.Setup(s => s.UpdateWorkItemAsync(36557, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, CancellationToken>((_, fields, _) => existingPlan.SDKReleaseMonth = fields["Custom.SDKReleasemonth"])
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 36557 });
            mockDevOps.Setup(s => s.GetReleasePlanForWorkItemAsync(36557, It.IsAny<CancellationToken>())).ReturnsAsync(existingPlan);
            var updateTool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService,
                environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), clock);

            var updateResponse = await updateTool.UpdateReleasePlanTarget(57, targetMonth);

            Assert.That(updateResponse.ResponseError, Is.Null);
            Assert.That(updateResponse.ReleasePlanDetails?.SDKReleaseMonth, Is.EqualTo(targetMonth), "An existing past-due plan must be repairable.");
            mockDevOps.Verify(s => s.UpdateWorkItemAsync(36557,
                It.Is<Dictionary<string, string>>(fields => fields.Count == 1 && fields["Custom.SDKReleasemonth"] == targetMonth),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ======================== UpdateReleasePlanTarget Tests ========================

        [Test]
        public async Task Test_UpdateReleasePlanTarget_with_valid_inputs()
        {
            var result = await releasePlanTool.UpdateReleasePlanTarget(workItemId: 100, targetReleaseMonthYear: "January 2026");

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated SDK release target month to January 2026"));
        }

        [Test]
        public async Task Test_UpdateReleasePlanTarget_with_invalid_work_item_id()
        {
            var result = await releasePlanTool.UpdateReleasePlanTarget(workItemId: 0, targetReleaseMonthYear: "January 2026");

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("valid work item ID"));
        }

        [Test]
        public async Task Test_UpdateReleasePlanTarget_with_empty_target_month()
        {
            var result = await releasePlanTool.UpdateReleasePlanTarget(workItemId: 100, targetReleaseMonthYear: "");

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("target month is required"));
        }

        [Test]
        public async Task Test_UpdateReleasePlanTarget_when_release_plan_not_found()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            mockDevOps
                .Setup(s => s.GetReleasePlanForWorkItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReleasePlanWorkItem?)null!);

            var tool = new ReleasePlanTool(
                mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper,
                gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), _timeProvider);

            var result = await tool.UpdateReleasePlanTarget(workItemId: 999, targetReleaseMonthYear: "January 2026");

            Assert.IsNotNull(result.ResponseError);
            Assert.That(result.ResponseError, Does.Contain("No release plan found"));
            mockDevOps.Verify(s => s.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ======================== RunTypeSpecMetadataEmitterAsync Tests ========================

        [Test]
        public async Task Test_UpdateReleasePlan_url_path_skips_emitter()
        {
            // When TypeSpec path is a URL, the emitter should be skipped (returns null)
            // but the update should still succeed
            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 400,
                ReleasePlanId = 40,
                IsManagementPlane = true
            };
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(400, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(400, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 400 });
            mockDevOps.Setup(x => x.UpdateSpecPullRequestAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateApiSpecVersionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var result = await tool.UpdateReleasePlan(
                typeSpecProjectPath: "https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contoso/Contoso.Management",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 400);

            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            // Emitter not called, so UpdateReleasePlanSDKDetailsAsync should not be called
            mockDevOps.Verify(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Test_UpdateReleasePlan_emitter_failure_still_succeeds()
        {
            // When the emitter fails (ParseTypeSpecProjectAsync returns null), the update should still
            // succeed but UpdateReleasePlanSDKDetailsAsync must not be called (no packages to update).
            var mockNpxHelper = new Mock<INpxHelper>();
            mockNpxHelper.Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1 });

            // Override the Setup() mock: return null to simulate emitter failure for this test only.
            var failingTypeSpecHelper = new Mock<ITypeSpecHelper>();
            failingTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);
            failingTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);
            failingTypeSpecHelper.Setup(x => x.IsUrl(It.IsAny<string>())).Returns(false);
            failingTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectUrl(It.IsAny<string>())).Returns(false);
            failingTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(It.IsAny<string>()))
                .Returns((string p) => p.Contains("specification") ? p.Substring(p.IndexOf("specification")) : p);
            failingTypeSpecHelper.Setup(x => x.IsRepoPathForSpecRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            failingTypeSpecHelper.Setup(x => x.GetSpecRepoRootPath(It.IsAny<string>())).Returns(string.Empty);
            failingTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(It.IsAny<string>(), It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TypeSpecProject?)null);

            var mockDevOps = new Mock<IDevOpsService>();
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 600,
                ReleasePlanId = 60,
                IsDataPlane = true
            };
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(600, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(600, It.IsAny<CancellationToken>())).ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 600 });
            mockDevOps.Setup(x => x.UpdateSpecPullRequestAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockDevOps.Setup(x => x.UpdateApiSpecVersionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, failingTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, mockNpxHelper.Object, Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var result = await tool.UpdateReleasePlan(
                typeSpecProjectPath: "TypeSpecTestData/specification/testcontoso/Contoso.Management",
                sdkReleaseType: "beta",
                specPullRequestUrl: "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                workItemId: 600);

            // Update should still succeed even though emitter failed
            Assert.IsNull(result.ResponseError, $"Unexpected error: {result.ResponseError}");
            Assert.That(result.Message, Does.Contain("Successfully updated release plan"));
            mockDevOps.Verify(x => x.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Test_UpdateReleasePlan_filters_unsupported_emitter_languages_without_validating_package_names(bool isManagementPlane)
        {
            var typeSpecPath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 700,
                ReleasePlanId = 70,
                IsManagementPlane = isManagementPlane,
                IsDataPlane = !isManagementPlane
            };
            var project = TypeSpecProject.ParseTypeSpecConfig(typeSpecPath);
            project.Packages =
            [
                new PackageInfo { Language = SdkLanguage.Python, PackageName = "azure-contoso" },
                new PackageInfo { Language = SdkLanguage.Go, PackageName = "sdk/contoso" },
                new PackageInfo { Language = SdkLanguage.JavaScript, PackageName = "@contoso/ai-content-safety" },
                new PackageInfo { Language = SdkLanguage.Rust, PackageName = "azure_contoso" },
                new PackageInfo { Language = SdkLanguage.Cpp, PackageName = "azure-contoso-cpp" }
            ];

            var mockDevOps = new Mock<IDevOpsService>();
            mockDevOps.Setup(x => x.ResolveReleasePlanByIdAsync(700, It.IsAny<CancellationToken>()))
                .ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.GetReleasePlanForWorkItemAsync(700, It.IsAny<CancellationToken>()))
                .ReturnsAsync(releasePlan);
            mockDevOps.Setup(x => x.UpdateWorkItemAsync(700, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem { Id = 700 });
            mockDevOps.Setup(x => x.UpdateReleasePlanSDKDetailsAsync(
                    700,
                    It.Is<List<SDKInfo>>(sdkInfos =>
                        sdkInfos.Count == 3 &&
                        sdkInfos.Any(sdk => sdk.Language == "Python" && sdk.PackageName == "azure-contoso") &&
                        sdkInfos.Any(sdk => sdk.Language == "Go" && sdk.PackageName == "sdk/contoso") &&
                        sdkInfos.Any(sdk => sdk.Language == "JavaScript" && sdk.PackageName == "@contoso/ai-content-safety") &&
                        sdkInfos.All(sdk => sdk.Language != "Rust" && sdk.Language != "C++")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsUrl(typeSpecPath)).Returns(false);
            mockTypeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(typeSpecPath))
                .Returns("specification/testcontoso/Contoso.Management");
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(typeSpecPath))
                .Returns(isManagementPlane);
            mockTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(
                    typeSpecPath,
                    It.IsAny<INpxHelper>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(project);

            var tool = new ReleasePlanTool(
                mockDevOps.Object,
                gitHelper,
                mockTypeSpecHelper.Object,
                logger,
                userHelper,
                gitHubService,
                environmentHelper,
                inputSanitizer,
                httpClient,
                Mock.Of<INpxHelper>(),
                Mock.Of<IRawOutputHelper>(),
                Mock.Of<INotificationService>());

            var result = await tool.UpdateReleasePlan(
                typeSpecProjectPath: typeSpecPath,
                sdkReleaseType: "beta",
                workItemId: 700);

            Assert.That(result.ResponseError, Is.Null);
            mockDevOps.Verify(x => x.UpdateReleasePlanSDKDetailsAsync(
                700,
                It.IsAny<List<SDKInfo>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ==================== KPI Attestation Tests ====================

        [Test]
        public async Task Test_GetKPIAttestationStatus_NoInputs_ReturnsError()
        {
            var result = await releasePlanTool.GetKPIAttestationStatus("", "", "");
            Assert.That(result.ResponseError, Does.Contain("Either provide both product ID and release plan type"));

            var badLifecycle = await releasePlanTool.GetKPIAttestationStatus("product-123", "InvalidLifecycle");
            Assert.That(badLifecycle.ResponseError, Does.Contain("Invalid release plan type"));
        }

        [Test]
        public async Task Test_GetKPIAttestationStatus_WithProductAndLifecycle_ReturnsNoError()
        {
            var result = await releasePlanTool.GetKPIAttestationStatus("product-123", "Private Preview");
            Assert.IsNull(result.ResponseError);
            Assert.That(result.Message, Does.Contain("No release plans found"));
        }

        [Test]
        public async Task Test_GetKPIAttestationStatus_WithTypeSpecPath_ResolvesProductInfo()
        {
            var result = await releasePlanTool.GetKPIAttestationStatus(typeSpecProjectPath: "specification/testcontoso/Contoso.Management");
            Assert.IsNull(result.ResponseError);
            Assert.That(result.Message, Does.Contain("12345678-1234-5678-9012-123456789012"));
            Assert.That(result.Message, Does.Contain("GA"));
        }
      
        private ReleasePlanTool CreateReleasePlanToolWithMockedTypeSpec(string typeSpecPath, TypeSpecProject typeSpecProject)
        {
            var mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockTypeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(typeSpecPath)).Returns(true);
            mockTypeSpecHelper.Setup(x => x.ParseTypeSpecProjectAsync(typeSpecPath, It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(typeSpecProject);
            return new ReleasePlanTool(devOpsService, gitHelper, mockTypeSpecHelper.Object, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
        }

        private ReleasePlanTool CreateReleasePlanToolWithNotificationService(INotificationService notificationService)
        {
            return new ReleasePlanTool(devOpsService, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, httpClient, Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), notificationService, _timeProvider);
        }

        /// <summary>
        /// Creates a dummy TypeSpecProject with widget service metadata to stand in for the real
        /// npx emitter output in tests that do not need accurate per-package data.
        /// </summary>
        private static TypeSpecProject CreateDummyTypeSpecProject()
        {
            var project = TypeSpecProject.ParseTypeSpecConfig("TypeSpecTestData/specification/testcontoso/Contoso.Management");
            project.Packages =
            [
                new PackageInfo { PackageName = "azure-mgmt-widgetservice", Language = SdkLanguage.Python, ApiVersion = "2026-05-02-preview", TypeSpecSdkType = "preview" },
                new PackageInfo { PackageName = "com.azure.resourcemanager:azure-resourcemanager-widgetservice", Language = SdkLanguage.Java, ApiVersion = "2026-05-02-preview", TypeSpecSdkType = "preview" },
                new PackageInfo { PackageName = "sdk/resourcemanager/widgetservice/armwidgetservice", Language = SdkLanguage.Go, ApiVersion = "2026-05-02-preview", TypeSpecSdkType = "preview" },
                new PackageInfo { PackageName = "@azure/arm-widgetservice", Language = SdkLanguage.JavaScript, ApiVersion = "2026-05-02-preview", TypeSpecSdkType = "preview" },
                new PackageInfo { PackageName = "Azure.ResourceManager.WidgetService", Language = SdkLanguage.DotNet, ApiVersion = "2026-05-02-preview", TypeSpecSdkType = "preview" },
            ];
            return project;
        }
    }
}
