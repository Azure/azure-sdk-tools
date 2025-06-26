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
            typeSpecHelper = new Mock<ITypeSpecHelper>().Object;
            userHelper = new Mock<IUserHelper>().Object;
            outputService = new Mock<IOutputService>().Object;
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
            var releaseplan = await releasePlanTool.CreateReleasePlan(testCodeFilePath, "July 2025", "TBD", "TBD", "Test version", "https://github.com/Azure/azure-rest-api-specs/pull/35446", "Preview", isTestReleasePlan: true);
            Assert.True(releaseplan.Contains("Invalid SDK release type"));
        }
    }
}
