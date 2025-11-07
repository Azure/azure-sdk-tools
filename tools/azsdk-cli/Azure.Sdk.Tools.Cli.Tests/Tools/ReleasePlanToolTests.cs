using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    internal class ReleasePlanToolTests
    {
        private TestLogger<ReleasePlanTool> logger;
        private IDevOpsService devOpsService;
        private IGitHubService gitHubService;
        private ITypeSpecHelper typeSpecHelper;
        private IUserHelper userHelper;
        private IEnvironmentHelper environmentHelper;
        private ReleasePlanTool releasePlanTool;

        [SetUp]
        public void Setup()
        {

            logger = new TestLogger<ReleasePlanTool>();
            devOpsService = new MockDevOpsService();
            gitHubService = new Mock<IGitHubService>().Object;

            var typeSpecHelperMock = new Mock<ITypeSpecHelper>();
            typeSpecHelperMock.Setup(x => x.IsRepoPathForPublicSpecRepo(It.IsAny<string>())).Returns(true);
            typeSpecHelper = typeSpecHelperMock.Object;

            var userHelperMock = new Mock<IUserHelper>();
            userHelperMock.Setup(x => x.GetUserEmail()).ReturnsAsync("test@example.com");
            userHelper = userHelperMock.Object;

            var environmentHelperMock = new Mock<IEnvironmentHelper>();
            environmentHelperMock.Setup(x => x.GetBooleanVariable(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);
            environmentHelper = environmentHelperMock.Object;

            releasePlanTool = new ReleasePlanTool(
                devOpsService,
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelper);
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

        [Test]
        public async Task Test_Create_releasePlan_with_valid_inputs()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";

            var releasePlanTasks = new[]{
                releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "2025-01-01", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta", isTestReleasePlan: true),
                releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "2025-01-01-preview", "https://github.com/Azure/azure-rest-api-specs/pull/35447", "stable", isTestReleasePlan: true),
                releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "2025-01-01-preview", "https://github.com/Azure/azure-rest-api-specs/pull/35448", "Preview", isTestReleasePlan: true),
                releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "2025-01-01", "https://github.com/Azure/azure-rest-api-specs/pull/35449", "GA", isTestReleasePlan: true),
            };

            var releasePlanResults = await Task.WhenAll(releasePlanTasks);

            var releasePlans = releasePlanResults
                .Select(r => r.ReleasePlanDetails as ReleasePlanDetails)
                .ToList();

            foreach (var plan in releasePlans)
            {
                Assert.IsNotNull(plan);
                Assert.IsNotNull(plan.WorkItemId);
                Assert.IsNotNull(plan.ReleasePlanId);
                Assert.IsNotNull(plan.ReleasePlanLink);
            }
        }

        [Test]
        public async Task Test_Create_releasePlan_with_AZSDKTOOLS_AGENT_TESTING_true_creates_test_release_plan()
        {
            // Arrange
            var environmentHelperMock = new Mock<IEnvironmentHelper>();
            environmentHelperMock.Setup(x => x.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false)).Returns(true);

            var testReleasePlanTool = new ReleasePlanTool(
                devOpsService,
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelperMock.Object);

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
            var releaseplanObj = releaseplan.ReleasePlanDetails as ReleasePlanDetails;
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
                typeSpecHelper,
                logger,
                userHelper,
                gitHubService,
                environmentHelperMock.Object);

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
            var releaseplanObj = releaseplan.ReleasePlanDetails as ReleasePlanDetails;
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
            Assert.That(releaseplan.ToString(), Does.Contain("Status: Success"));
            Assert.That(releaseplan.ToString(), Does.Contain("Release Plan"));
        }

        [Test]
        public async Task Test_Get_Release_Plan_For_Pull_Request_with_invalid_pr_link()
        {
            var releaseplan = await releasePlanTool.GetReleasePlanForPullRequest("invalid-pr-link");
            Assert.That(releaseplan.ToString(), Does.Contain("Failed to get release plan details"));
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
            Assert.That(releaseplan.ToString(), Does.Contain("Failed to get release plan details"));
            Assert.That(releaseplan.ToString(), Does.Contain("Invalid spec pull request URL"));
        }

        [Test]
        public async Task Test_Update_SDK_Details_In_Release_Plan()
        {
            string sdkDetails = "[{\"language\":\".NET\",\"packageName\":\"Azure.ResourceManager.Contoso\"},{\"language\":\"Python\",\"packageName\":\"azure-mgmt-contoso\"},{\"language\":\"Java\",\"packageName\":\"com.azure.resourcemanager.contoso\"},{\"language\":\"JavaScript\",\"packageName\":\"@azure/arm-contoso\"},{\"language\":\"Go\",\"packageName\":\"sdk/resourcemanager/contoso/armcontoso\"}]";
            var updateStatus = await releasePlanTool.UpdateSDKDetailsInReleasePlan(100, sdkDetails);
            Assert.That(updateStatus.Message, Does.Contain("Updated SDK details in release plan"));

            sdkDetails = "[{\"Language\":\".NET\",\"PackageName\":\"Azure.ResourceManager.Contoso\"},{\"language\":\"Python\",\"packageName\":\"azure-mgmt-contoso\"},{\"language\":\"Java\",\"packageName\":\"com.azure.resourcemanager.contoso\"},{\"language\":\"JavaScript\",\"packageName\":\"@azure/arm-contoso\"},{\"language\":\"Go\",\"packageName\":\"sdk/resourcemanager/contoso/armcontoso\"}]";
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

        [Test]
        public  async Task Test_update_language_exclusion_justification()
        {
            var updateStatus = await releasePlanTool.UpdateLanguageExclusionJustification(100, "This is a test justification for excluding certain languages.");
            Assert.That(updateStatus.Message, Does.Contain("Updated language exclusion justification in release plan"));
        }

    }
}
