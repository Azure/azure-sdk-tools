using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Graph.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    internal class ReleasePlanToolTests
    {
        private TestLogger<ReleasePlanTool> logger;
        private IDevOpsService devOpsService;
        private IGitHubService gitHubService;
        private ITypeSpecHelper typeSpecHelper;
        private IUserHelper userHelper;
        private IOutputHelper outputService;
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

            var outputServiceMock = new Mock<IOutputHelper>();
            outputServiceMock.Setup(x => x.Format(It.IsAny<object>())).Returns<object>(obj => obj?.ToString() ?? "");
            outputService = outputServiceMock.Object;

            var environmentHelperMock = new Mock<IEnvironmentHelper>();
            environmentHelperMock.Setup(x => x.GetBooleanVariable(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);
            environmentHelper = environmentHelperMock.Object;

            releasePlanTool = new ReleasePlanTool(
                devOpsService,
                typeSpecHelper,
                logger,
                outputService,
                userHelper,
                gitHubService,
                environmentHelper);
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_SDK_type()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "Test version", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "Preview", isTestReleasePlan: true);
            Assert.True(releaseplan.Contains("Invalid SDK release type"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_service_tree_id()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "InvalidServiceTreeId", "12345678-1234-5678-9012-123456789012", "Test version", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta", isTestReleasePlan: true);
            Assert.True(releaseplan.Contains("Service tree ID 'InvalidServiceTreeId' is not a valid GUID"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_product_tree_id()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "InvalidProductTreeId", "Test version", "https://github.com/Azure/azure-rest-api-specs-pr/pull/35446", "beta", isTestReleasePlan: true);
            Assert.True(releaseplan.Contains("Product tree ID 'InvalidProductTreeId' is not a valid GUID"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_invalid_pull_request_url()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "Test version", "https://github.com/Azure/invalid-repo/pull/35446", "beta", isTestReleasePlan: true);
            Assert.True(releaseplan.Contains("Invalid spec pull request URL"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_valid_inputs()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "Test version", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.True(releaseplan.Contains("Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem"));
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
                outputService,
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
                "Test version",
                "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                "beta",
                isTestReleasePlan: false); // This should be overridden to true by environment variable

            // Assert
            Assert.IsNotNull(releaseplan);
            Assert.True(releaseplan.Contains("Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem"));

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
                outputService,
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
                "Test version",
                "https://github.com/Azure/azure-rest-api-specs/pull/35446",
                "beta",
                isTestReleasePlan: false);

            // Assert
            Assert.IsNotNull(releaseplan);
            Assert.True(releaseplan.Contains("Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem"));

            // Verify the environment helper was called
            environmentHelperMock.Verify(x => x.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false), Times.Once);
        }
    }
}
