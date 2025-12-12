using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Helpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    internal class ReleasePlanManualTests
    {
        private IAzureService azureService;
        private IDevOpsService devOpsService;
        private ReleasePlanTool releasePlan;
        private TestLogger<ReleasePlanTool> logger;
        private IGitHubService gitHubService;
        private ITypeSpecHelper typeSpecHelper;
        private IUserHelper userHelper;
        private IEnvironmentHelper environmentHelper;
        private readonly IGitHelper gitHelper;
        private IInputSanitizer inputSanitizer;

        public ReleasePlanManualTests()
        {
            azureService = new AzureService();
            var devopsLogger = new TestLogger<DevOpsService>();
            var devopsConnection = new DevOpsConnection(azureService);
            devOpsService = new DevOpsService(devopsLogger, devopsConnection);

            logger = new TestLogger<ReleasePlanTool>();
            gitHubService = new Mock<IGitHubService>().Object;
            inputSanitizer = new InputSanitizer();

            var typeSpecHelperMock = new Mock<ITypeSpecHelper>();
            typeSpecHelperMock.Setup(x => x.IsRepoPathForPublicSpecRepo(It.IsAny<string>())).Returns(true);
            typeSpecHelper = typeSpecHelperMock.Object;

            var userHelperMock = new Mock<IUserHelper>();
            userHelperMock.Setup(x => x.GetUserEmail()).ReturnsAsync("test@example.com");
            userHelper = userHelperMock.Object;

            var environmentHelperMock = new Mock<IEnvironmentHelper>();
            environmentHelperMock.Setup(x => x.GetBooleanVariable(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);
            environmentHelper = environmentHelperMock.Object;

            var gitHelperMock = new Mock<IGitHelper>();
            gitHelperMock.Setup(x => x.GetBranchName(It.IsAny<string>())).Returns("testBranch");
            gitHelper = gitHelperMock.Object;

            releasePlan = new ReleasePlanTool(devOpsService, gitHelper, typeSpecHelper, logger, userHelper, gitHubService, environmentHelper, inputSanitizer);
        }

        [Test] // disabled by default because it makes real API calls
        [Ignore("Manual test - requires real API calls")]
        public async Task Test_UpdateExclusionJustification()
        {
            int releasePlanWorkItemId = 28940; // replace with a real release plan ID
            string exclusionJustification = "Updated justification for exclusion.";
            var updateStatus = await this.releasePlan.UpdateLanguageExclusionJustification(releasePlanWorkItemId, exclusionJustification);
            Assert.IsNotNull(updateStatus);
            Assert.That(updateStatus.Message, Does.Contain("Updated language exclusion"));

            var releasePlanInfo = await this.devOpsService.GetReleasePlanForWorkItemAsync(releasePlanWorkItemId);
            Assert.That(exclusionJustification, Is.EqualTo(releasePlanInfo.LanguageExclusionRequesterNote));
        }

        [Test] // disabled by default because it makes real API calls
        [Ignore("Manual test - requires real API calls")]
        public async Task Test_UpdateExclusionJustificationWithLanguage()
        {
            int releasePlanWorkItemId = 28940; // replace with a real release plan ID
            string exclusionJustification = "Updated justification for exclusion.";
            var updateStatus = await this.releasePlan.UpdateLanguageExclusionJustification(releasePlanWorkItemId, exclusionJustification, "Java");
            Assert.IsNotNull(updateStatus);
            Assert.That(updateStatus.Message, Does.Contain("Updated language exclusion"));

            var releasePlanInfo = await this.devOpsService.GetReleasePlanForWorkItemAsync(releasePlanWorkItemId);
            Assert.That(exclusionJustification, Is.EqualTo(releasePlanInfo.LanguageExclusionRequesterNote));
        }


        [Test] // disabled by default because it makes real API calls
        [Ignore("Manual test - requires real API calls")]
        public async Task Test_Get_ReleaseExclusionStatus()
        {
            int releasePlan = 28940; // replace with a real release plan ID
            var releasePlanInfo = await this.devOpsService.GetReleasePlanForWorkItemAsync(releasePlan);
            Assert.IsNotNull(releasePlanInfo);
            
            var pythonSdk = releasePlanInfo.SDKInfo.FirstOrDefault(sdk => sdk.Language.Equals("Python", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(pythonSdk);
            Assert.That(pythonSdk.ReleaseExclusionStatus, Is.EqualTo("Requested"));
        }
    }
}
