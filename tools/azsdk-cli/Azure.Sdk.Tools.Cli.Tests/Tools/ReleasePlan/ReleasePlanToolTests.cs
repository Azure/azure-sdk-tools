using Moq;
using Moq.Protected;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

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

        [SetUp]
        public void Setup()
        {

            logger = new TestLogger<ReleasePlanTool>();
            devOpsService = new MockDevOpsService();
            gitHubService = new MockGitHubService();
            inputSanitizer = new InputSanitizer();
            httpClient = new Mock<HttpClient>().Object;

            var userHelperMock = new Mock<IUserHelper>();
            userHelperMock.Setup(x => x.GetUserEmail()).ReturnsAsync("test@example.com");
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

            typeSpecHelper = new TypeSpecHelper(gitHelper);

            releasePlanTool = new ReleasePlanTool(
                devOpsService,
                gitHelper,
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelper,
                inputSanitizer,
                httpClient);
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_SDK_type()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "2025-01-01", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "invalid-sdk-type", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Invalid SDK release type"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_service_tree_id()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "InvalidServiceTreeId", "12345678-1234-5678-9012-123456789012", "2025-01-01", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Service tree ID 'InvalidServiceTreeId' is not a valid GUID"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_product_tree_id()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "InvalidProductTreeId", "2025-01-01", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Product tree ID 'InvalidProductTreeId' is not a valid GUID"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_pull_request_url()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "2025-01-01", "https://github.com/Azure/invalid-repo/pull/35446", "beta", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Invalid spec pull request URL"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_api_version()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "invalid-api-version", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Invalid API version"));
        }

        [TestCase("TypeSpecTestData/specification/testcontoso/Contoso.Management", "July 2025", "2025-01-01", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta")]
        [TestCase("TypeSpecTestData/specification/testcontoso/Contoso.Management", "July 2025", "2025-01-01-preview", "https://github.com/Azure/azure-rest-api-specs/pull/35447", "stable")]
        [TestCase("TypeSpecTestData/specification/testcontoso/Contoso.Management", "July 2025", "2025-01-01-preview", "https://github.com/Azure/azure-rest-api-specs/pull/35448", "Preview")]
        [TestCase("TypeSpecTestData/specification/testcontoso/Contoso.Management", "July 2025", "2025-01-01", "https://github.com/Azure/azure-rest-api-specs/pull/35449", "GA")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management", "January 2026", "2025-03-21", "https://github.com/Azure/azure-rest-api-specs/pull/39310", "stable")]
        [Test]
        public async Task Test_Create_releasePlan_with_valid_inputs(string typeSpecPath, string targetMonth, string apiVersion, string prUrl, string sdkType)
        {
            var result = await releasePlanTool.CreateReleasePlan(
                typeSpecPath, 
                targetMonth, 
                "12345678-1234-5678-9012-123456789012", 
                "12345678-1234-5678-9012-123456789012", 
                apiVersion, 
                prUrl, 
                sdkType, 
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
                httpClient);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";

            // Act
            var releaseplan = await testReleasePlanTool.CreateReleasePlan(
                testCodeFilePath,
                "July 2025",
                "12345678-1234-5678-9012-123456789012",
                "12345678-1234-5678-9012-123456789012",
                "2025-08-19-preview",
                "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                "beta",
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
                httpClient);

            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";

            // Act
            var releaseplan = await testReleasePlanTool.CreateReleasePlan(
                testCodeFilePath,
                "July 2025",
                "12345678-1234-5678-9012-123456789012",
                "12345678-1234-5678-9012-123456789012",
                "2009-10-10",
                "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                "beta",
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
        public async Task Test_Get_Release_Plan_For_Pull_Request_with_valid_inputs()
        {
            var releaseplan = await releasePlanTool.GetReleasePlanForPullRequest("https://github.com/Azure/azure-rest-api-specs/pull/35446");
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.Details);
            Assert.That(releaseplan.Details, Has.Some.Contains("Release Plan"));
        }

        [Test]
        public async Task Test_Get_Release_Plan_For_Pull_Request_with_invalid_pr_link()
        {
            var releaseplan = await releasePlanTool.GetReleasePlanForPullRequest("invalid-pr-link");
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Failed to get release plan details"));
        }

        [Test]
        public async Task Test_Create_releasePlan_rejects_azure_rest_api_specs_pr_repo()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "2025-01-01", "https://github.com/Azure/azure-rest-api-specs-pr/pull/35446", "beta", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Invalid spec pull request URL"));
            Assert.True(releaseplan.ResponseError.Contains("azure-rest-api-specs repo"));
        }

        [Test]
        public async Task Test_Get_Release_Plan_For_Pull_Request_rejects_azure_rest_api_specs_pr_repo()
        {
            var releaseplan = await releasePlanTool.GetReleasePlanForPullRequest("https://github.com/Azure/azure-rest-api-specs-pr/pull/35446");
            Assert.IsNotNull(releaseplan);
            Assert.IsNotNull(releaseplan.ResponseError);
            Assert.True(releaseplan.ResponseError.Contains("Failed to get release plan details"));
            Assert.True(releaseplan.ResponseError.Contains("Invalid spec pull request URL"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_In_Release_Plan()
        {
            string sdkDetails = "[{\"language\":\".NET\",\"packageName\":\"Azure.ResourceManager.Contoso\"},{\"language\":\"Python\",\"packageName\":\"azure-mgmt-contoso\"},{\"language\":\"Java\",\"packageName\":\"com.azure.resourcemanager.contoso\"},{\"language\":\"JavaScript\",\"packageName\":\"@azure/arm-contoso\"},{\"language\":\"Go\",\"packageName\":\"sdk/resourcemanager/contoso/armcontoso\"}]";
            var updateStatus = await releasePlanTool.UpdateSDKDetailsInReleasePlan(100, sdkDetails);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));

            sdkDetails = "[{\"Language\":\".NET\",\"PackageName\":\"Azure.ResourceManager.Contoso\"},{\"language\":\"Python\",\"packageName\":\"azure-mgmt-contoso\"},{\"language\":\"Java\",\"packageName\":\"azure-resourcemanager-contoso\"},{\"language\":\"JavaScript\",\"packageName\":\"@azure/arm-contoso\"},{\"language\":\"Go\",\"packageName\":\"sdk/resourcemanager/contoso/armcontoso\"}]";
            updateStatus = await releasePlanTool.UpdateSDKDetailsInReleasePlan(100, sdkDetails);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_Mgmt_language_excl()
        {
            string sdkDetails = "[{\"language\":\".NET\",\"packageName\":\"Azure.ResourceManager.Contoso\"},{\"language\":\"JavaScript\",\"packageName\":\"@azure/arm-contoso\"}]";
            var updateStatus = await releasePlanTool.UpdateSDKDetailsInReleasePlan(100, sdkDetails);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));
            Assert.That(updateStatus.Message, Does.Contain("Important: The following languages were excluded in the release plan. SDK must be released for all languages."));
            Assert.True(updateStatus.NextSteps?.Contains("Prompt the user for justification for excluded languages and update it in the release plan.") ?? false);
        }

        [Test]
        public async Task Test_Update_SDK_Details_Data_language_excl()
        {
            string sdkDetails = "[{\"language\":\".NET\",\"packageName\":\"Azure.Contoso\"},{\"language\":\"JavaScript\",\"packageName\":\"@azure/contoso\"}]";
            var updateStatus = await releasePlanTool.UpdateSDKDetailsInReleasePlan(1001, sdkDetails);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));
            Assert.That(updateStatus.Message, Does.Contain("Important: The following languages were excluded in the release plan. SDK must be released for all languages."));
            Assert.That(updateStatus.NextSteps?.Contains("Prompt the user for justification for excluded languages and update it in the release plan.") ?? false);
        }

        [TestCase("Javascript", "@invalid/package/name")]
        [TestCase("Go", "invalid/package/name")]
        [Test]
        public async Task Test_Update_SDK_Details_single_invalid_package_name(string language, string package)
        {
            string sdkDetails = $"[{{\"language\":\"{language}\",\"packageName\":\"{package}\"}}]";
            var updateStatus = await releasePlanTool.UpdateSDKDetailsInReleasePlan(100, sdkDetails);
            Assert.That(updateStatus.ResponseError, Does.Contain("Unsupported package name"));
            Assert.That(updateStatus.ResponseError, Does.Contain($"{language} -> {package}"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_multiple_invalid_package_names()
        {
            string sdkDetails = "[{\"language\":\"JavaScript\",\"packageName\":\"@invalid/package\"}," +
                        "{\"language\":\"Go\",\"packageName\":\"invalid/package\"}]";
            var updateStatus = await releasePlanTool.UpdateSDKDetailsInReleasePlan(100, sdkDetails);
            Assert.That(updateStatus.ResponseError, Does.Contain("Unsupported package name"));
            Assert.That(updateStatus.ResponseError, Does.Contain("JavaScript -> @invalid/package"));
            Assert.That(updateStatus.ResponseError, Does.Contain("Go -> invalid/package"));
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
            Assert.That(response.Language, Is.Not.EqualTo(Models.SdkLanguage.Unknown), $"Language property should be set for '{language}'.");
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
                ReleasePlanLink = "https://example.com/releaseplan/200",
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", ReleaseStatus = "Released", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = ".NET", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync()).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content?.ReadAsStringAsync().Result ?? "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient);

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
                ReleasePlanLink = "https://example.com/releaseplan/201",
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Python", ReleaseStatus = "", ReleaseExclusionStatus = "Approved" },
                    new SDKInfo { Language = ".NET", ReleaseStatus = "", ReleaseExclusionStatus = "Requested" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync()).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content?.ReadAsStringAsync().Result ?? "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient);

            await tool.ListOverdueReleasePlans(notifyOwners: true, emailerUri: "https://test.com/email");

            Assert.That(capturedBody, Does.Contain("Java"));
            Assert.That(capturedBody, Does.Not.Contain("Python")); // Approved exclusion
            Assert.That(capturedBody, Does.Not.Contain(".NET")); // Requested exclusion
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
                ReleasePlanLink = "https://example.com/releaseplan/202",
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync()).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content?.ReadAsStringAsync().Result ?? "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient);

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
                ReleasePlanLink = "https://example.com/releaseplan/203",
                SDKInfo =
                [
                    new SDKInfo { Language = "Java", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" },
                    new SDKInfo { Language = "Go", ReleaseStatus = "", ReleaseExclusionStatus = "Not applicable" }
                ]
            };
            mockDevOps.Setup(x => x.ListOverdueReleasePlansAsync()).ReturnsAsync([plan]);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var capturedBody = "";
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var content = request.Content?.ReadAsStringAsync().Result ?? "";
                    var payload = JsonSerializer.Deserialize<JsonElement>(content);
                    capturedBody = payload.GetProperty("Body").GetString() ?? "";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                });

            var testHttpClient = new HttpClient(mockHttpMessageHandler.Object);
            var tool = new ReleasePlanTool(mockDevOps.Object, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer, testHttpClient);

            await tool.ListOverdueReleasePlans(notifyOwners: true, emailerUri: "https://test.com/email");

            Assert.That(capturedBody, Does.Contain("Java"));
            Assert.That(capturedBody, Does.Contain("Go")); // Included for Management Plane
            Assert.That(capturedBody, Does.Contain("Management Plane"));
        }
    }
}
