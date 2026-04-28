using Moq;

using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tools.Config;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Caches;

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
        private Mock<ITeamUserCache> _mockTeamUserCache;
        private Mock<ICheckPackageHelper> _mockCheckPackageHelper;
        private Mock<ICodeownersAuditHelper> _mockAuditHelper;

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
            _mockTeamUserCache = new Mock<ITeamUserCache>();
            _mockTeamUserCache.Setup(c => c.GetUsersForTeam(It.IsAny<string>())).Returns(new List<string>());
            _mockCheckPackageHelper = new Mock<ICheckPackageHelper>();
            _mockAuditHelper = new Mock<ICodeownersAuditHelper>();

            _tool = new CodeownersTool(
                _mockGithub,
                new TestLogger<CodeownersTool>(),
                null,
                _mockCodeownersValidator.Object,
                _mockcodeownersGenerateHelper.Object,
                _mockGitHelper.Object,
                _mockCodeownersManagement.Object,
                _mockCheckPackageHelper.Object,
                _mockDevOps.Object,
                _mockAuditHelper.Object
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

        [Test]
        public async Task Audit_ReturnsStructuredResponse()
        {
            var auditResponse = new CodeownersAuditResponse
            {
                FixRequested = true,
                ForceRequested = false,
                Repo = "Azure/azure-sdk-for-net",
            };
            auditResponse.Violations.AddRange(
            [
                new AuditViolation
                {
                    RuleId = "AUD-OWN-001",
                    Description = "Owner 'baduser' (10): not a valid code owner",
                    WorkItemId = 10,
                    Detail = "Set Invalid",
                },
                new AuditViolation
                {
                    RuleId = "AUD-STR-001",
                    Description = "Label Owner 20: has zero owners",
                    WorkItemId = 20,
                    Detail = "Type: Service Owner",
                }
            ]);
            auditResponse.FixResults.Add(new AuditFixResult
            {
                RuleId = "AUD-OWN-001",
                Description = "Set Invalid Since on Owner 'baduser' (10)",
                Success = true,
            });

            _mockAuditHelper
                .Setup(h => h.RunAudit(true, false, "Azure/azure-sdk-for-net", CancellationToken.None))
                .ReturnsAsync(auditResponse);

            var result = await _tool.Audit(true, false, "Azure/azure-sdk-for-net", CancellationToken.None);

            Assert.That(result, Is.TypeOf<CodeownersAuditResponse>());

            var response = (CodeownersAuditResponse)result;
            Assert.Multiple(() =>
            {
                Assert.That(response.FixRequested, Is.True);
                Assert.That(response.ForceRequested, Is.False);
                Assert.That(response.Repo, Is.EqualTo("Azure/azure-sdk-for-net"));
                Assert.That(response.TotalViolations, Is.EqualTo(2));
                Assert.That(response.FixesApplied, Is.EqualTo(1));
                Assert.That(response.FixesFailed, Is.EqualTo(0));
                Assert.That(response.Violations, Has.Count.EqualTo(2));
                Assert.That(response.FixResults, Has.Count.EqualTo(1));
                Assert.That(response.ToString(), Does.Contain("=== CODEOWNERS Audit Report ==="));
                Assert.That(response.ToString(), Does.Contain("--- AUD-OWN-001 (1 violations) ---"));
                Assert.That(response.ToString(), Does.Contain("[SUCCESS] Set Invalid Since on Owner 'baduser' (10)"));
            });
        }

    }
}
