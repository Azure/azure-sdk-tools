using Moq;
using Octokit;

using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Azure.Sdk.Tools.Cli.Tools.Config;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Config
{
    [TestFixture]
    public class CodeownersToolsTests
    {
        private MockGitHubService _mockGithub;
        private Mock<IGitHelper> _mockGitHelper;
        private Mock<ICodeownersValidatorHelper> _mockCodeownersValidator;
        private Mock<ICodeownersGenerateHelper> _mockcodeownersGenerateHelper;
        private Mock<ICodeownersManagementHelper> _mockCodeownersManagement;
        private Mock<IDevOpsService> _mockDevOps;

        private CodeownersTool _tool;

        [SetUp]
        public void Setup()
        {
            _mockGithub = new MockGitHubService();
            _mockCodeownersValidator = new Mock<ICodeownersValidatorHelper>();
            _mockcodeownersGenerateHelper = new Mock<ICodeownersGenerateHelper>();
            _mockGitHelper = new Mock<IGitHelper>();
            _mockCodeownersManagement = new Mock<ICodeownersManagementHelper>();
            _mockDevOps = new Mock<IDevOpsService>();

            _tool = new CodeownersTool(
                _mockGithub,
                new TestLogger<CodeownersTool>(),
                null,
                _mockCodeownersValidator.Object,
                _mockcodeownersGenerateHelper.Object,
                _mockGitHelper.Object,
                _mockCodeownersManagement.Object,
                _mockDevOps.Object
            );
        }

        // ========================
        // Individual add/remove tool tests
        // ========================

        [Test]
        public async Task AddPackageOwner_CallsAddOwnersToPackage()
        {
            _mockCodeownersManagement
                .Setup(m => m.FindOwnerByGitHubAlias("user1", default))
                .ReturnsAsync(new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "user1" });

            _mockCodeownersManagement
                .Setup(m => m.AddOwnersToPackage(It.IsAny<OwnerWorkItem[]>(), "pkg1", "Azure/azure-sdk-for-net", CancellationToken.None))
                .ReturnsAsync(new CodeownersModifyResponse { View = new CodeownersViewResponse() });

            var result = await _tool.AddPackageOwner(
                githubUsers: ["user1"], package: "pkg1", repo: "Azure/azure-sdk-for-net");

            Assert.That(result, Is.Not.Null);
            _mockCodeownersManagement.Verify(m => m.AddOwnersToPackage(It.IsAny<OwnerWorkItem[]>(), "pkg1", "Azure/azure-sdk-for-net", CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task AddPackageOwner_MultipleUsers_CallsOnce()
        {
            _mockCodeownersManagement
                .Setup(m => m.FindOwnerByGitHubAlias(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string alias, CancellationToken _) => new OwnerWorkItem { WorkItemId = alias == "user1" ? 10 : 11, GitHubAlias = alias });

            _mockCodeownersManagement
                .Setup(m => m.AddOwnersToPackage(It.IsAny<OwnerWorkItem[]>(), "pkg1", "Azure/azure-sdk-for-net", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeownersModifyResponse { View = new CodeownersViewResponse() });

            var result = await _tool.AddPackageOwner(
                githubUsers: ["user1", "user2"], package: "pkg1", repo: "Azure/azure-sdk-for-net");

            Assert.That(result, Is.Not.Null);
            _mockCodeownersManagement.Verify(m => m.AddOwnersToPackage(It.IsAny<OwnerWorkItem[]>(), "pkg1", "Azure/azure-sdk-for-net", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task RemovePackageLabel_CallsRemoveLabelsFromPackage()
        {
            _mockCodeownersManagement
                .Setup(m => m.FindLabelByName("lbl1", default))
                .ReturnsAsync(new LabelWorkItem { WorkItemId = 20, LabelName = "lbl1" });

            _mockCodeownersManagement
                .Setup(m => m.RemoveLabelsFromPackage(It.IsAny<LabelWorkItem[]>(), "pkg1", "Azure/azure-sdk-for-net", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeownersModifyResponse { View = new CodeownersViewResponse() });

            var result = await _tool.RemovePackageLabel(
                labels: ["lbl1"], package: "pkg1", repo: "Azure/azure-sdk-for-net");

            Assert.That(result, Is.Not.Null);
            _mockCodeownersManagement.Verify(m => m.RemoveLabelsFromPackage(It.IsAny<LabelWorkItem[]>(), "pkg1", "Azure/azure-sdk-for-net", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task AddPackageOwner_RepoInferredFromGit_WhenNotProvided()
        {
            var discoveredRoot = "/discovered/repo/root";
            _mockGitHelper.Setup(g => g.DiscoverRepoRootAsync(".", It.IsAny<CancellationToken>())).ReturnsAsync(discoveredRoot);
            _mockGitHelper.Setup(g => g.GetRepoFullNameAsync(discoveredRoot, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync("Azure/azure-sdk-for-net");
            _mockCodeownersManagement
                .Setup(m => m.FindOwnerByGitHubAlias("user1", default))
                .ReturnsAsync(new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "user1" });
            _mockCodeownersManagement
                .Setup(m => m.AddOwnersToPackage(It.IsAny<OwnerWorkItem[]>(), "pkg1", "Azure/azure-sdk-for-net", CancellationToken.None))
                .ReturnsAsync(new CodeownersModifyResponse { View = new CodeownersViewResponse() });

            var result = await _tool.AddPackageOwner(
                githubUsers: ["user1"], package: "pkg1", repo: null, ct: CancellationToken.None);

            _mockGitHelper.Verify(g => g.DiscoverRepoRootAsync(".", CancellationToken.None), Times.Once);
            _mockGitHelper.Verify(g => g.GetRepoFullNameAsync(discoveredRoot, It.IsAny<bool>(), CancellationToken.None), Times.Once);
            _mockCodeownersManagement.Verify(m => m.AddOwnersToPackage(It.IsAny<OwnerWorkItem[]>(), "pkg1", "Azure/azure-sdk-for-net", CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task AddPackageOwner_GitRepoInferenceFails_ReturnsError()
        {
            _mockGitHelper.Setup(g => g.DiscoverRepoRootAsync(".", It.IsAny<CancellationToken>())).ReturnsAsync("/some/root");
            _mockGitHelper.Setup(g => g.GetRepoFullNameAsync("/some/root", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);

            var result = await _tool.AddPackageOwner(
                githubUsers: ["user1"], package: "pkg1", repo: null);

            Assert.That(result.ToString(), Does.Contain("Could not infer repository"));
        }
    }
}
