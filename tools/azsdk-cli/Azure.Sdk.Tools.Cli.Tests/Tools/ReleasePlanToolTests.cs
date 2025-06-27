using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.MockServices;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    internal class ReleasePlanToolTests
    {
        private TestLogger<ReleasePlanTool> logger;
        private IDevOpsService devOpsService;
        private IGitHubService gitHubService;
        private ITypeSpecHelper typeSpecHelper;
        private IUserHelper userHelper;
        private IOutputService outputService;
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

            var outputServiceMock = new Mock<IOutputService>();
            outputServiceMock.Setup(x => x.Format(It.IsAny<object>())).Returns<object>(obj  => obj?.ToString() ?? "");
            outputService = outputServiceMock.Object;

            releasePlanTool = new ReleasePlanTool(
                devOpsService,
                typeSpecHelper,
                logger,
                outputService,
                userHelper,
                gitHubService);
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
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "InvalidProductTreeId", "Test version", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta", isTestReleasePlan: true);
            Assert.True(releaseplan.Contains("Product tree ID 'InvalidProductTreeId' is not a valid GUID"));
        }

        [Test]
        public async Task Test_Create_releasePlan_with_valid_inputs()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "12345678-1234-5678-9012-123456789012", "12345678-1234-5678-9012-123456789012", "Test version", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "beta", isTestReleasePlan: true);
            Assert.IsNotNull(releaseplan);
            Assert.True(releaseplan.Contains("Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem"));
        }
    }
}
