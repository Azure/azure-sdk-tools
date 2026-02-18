using Moq;
using Octokit;

using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Azure.Sdk.Tools.Cli.Tools.Config;
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

        private CodeownersTool _tool;

        [SetUp]
        public void Setup()
        {
            _mockGithub = new MockGitHubService();
            _mockCodeownersValidator = new Mock<ICodeownersValidatorHelper>();
            _mockcodeownersGenerateHelper = new Mock<ICodeownersGenerateHelper>();
            _mockGitHelper = new Mock<IGitHelper>();

            _tool = new CodeownersTool(
                _mockGithub,
                new TestLogger<CodeownersTool>(),
                null,
                _mockCodeownersValidator.Object,
                _mockcodeownersGenerateHelper.Object,
                _mockGitHelper.Object
            );
        }

        [Test]
        public async Task Update_InvalidInputs_Throws()
        {
            // both serviceLabel and path empty -> method throws
            var result = await _tool.UpdateCodeowners("repo", false, "", "", [], [], false);
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.ResponseError);
            Assert.That(result.ToString(), Does.Contain("Service label:  and Path:  are both invalid. At least one must be valid"));
        }

        [Test]
        public async Task Update_LabelMissing_NotInReview_NoPath_Throws()
        {
            var gh = new Mock<IGitHubService>();
            // labels file exists but does not contain the label
            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string?>()))
                .ReturnsAsync(new RepositoryContent("labels.md", "labels.md", "sha", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# Labels\n")), null, null));
            // no label PRs
            gh.Setup(s => s.SearchPullRequestsByTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ItemState?>()))
                .ReturnsAsync(new List<PullRequest?>().AsReadOnly());
            // provide an (empty) CODEOWNERS file so UpdateCodeowners can proceed
            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, "repo", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
                .ReturnsAsync(new RepositoryContent("CODEOWNERS", ".github/CODEOWNERS", "shaCode", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("")), null, null));

            var tool = new CodeownersTool(
                gh.Object,
                new TestLogger<CodeownersTool>(),
                null,
                _mockCodeownersValidator.Object,
                _mockcodeownersGenerateHelper.Object,
                _mockGitHelper.Object
            );

            var result = await tool.UpdateCodeowners("repo", false, "", "NonExistService", [], [], true);

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.ResponseError);
            Assert.That(result.ToString(), Does.Contain("When creating a new entry, both a Service Label and Path are required"));
        }

        [Test]
        public async Task Update_AddOwnersToExistingEntry_Success()
        {
            Assert.Ignore("This test needs to be updated to not make an http request via codeowners utils TeamUserCache");

            var gh = new Mock<IGitHubService>();
            // Simulate CODEOWNERS file with an entry for /sdk/myservice/
            string codeownersContent = "/sdk/myservice/ @oldowner\n";
            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
                .ReturnsAsync(new RepositoryContent("CODEOWNERS", ".github/CODEOWNERS", "shaCode", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(codeownersContent)), null, null));
            gh.Setup(s => s.SearchPullRequestsByTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ItemState?>()))
                .ReturnsAsync(new List<PullRequest?>().AsReadOnly());
            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string?>()))
                .ReturnsAsync(new RepositoryContent("labels.md", "labels.md", "sha", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# Labels\n")), null, null));
            gh.Setup(s => s.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            gh.Setup(s => s.CreateBranchAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), It.IsAny<string>(), "main")).ReturnsAsync(CreateBranchStatus.Created);
            gh.Setup(s => s.UpdateFileAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            gh.Setup(s => s.CreatePullRequestAsync(It.IsAny<string>(), Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
                .ReturnsAsync(new PullRequestResult { Url = "https://pr", Messages = ["Created"] });

            var validator = new Mock<ICodeownersValidatorHelper>();
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new CodeownersValidationResult { Username = "user", IsValidCodeOwner = true });

            var tool = new CodeownersTool(
                gh.Object,
                new TestLogger<CodeownersTool>(),
                null,
                validator.Object,
                _mockcodeownersGenerateHelper.Object,
                _mockGitHelper.Object
            );
            var result = await tool.UpdateCodeowners("repoName", false, "/sdk/myservice/", "MyService", ["@oldowner", "@newowner"], ["@newowner", "@newowner2"], true);
            Assert.IsNotNull(result);
            Assert.That(result.ToString(), Does.Contain("URL:").And.Contains("Created"));
        }

        [Test]
        public async Task Update_RemoveOwnersFromExistingEntry_Success()
        {
            Assert.Ignore("This test needs to be updated to not make an http request via codeowners utils TeamUserCache");

            var gh = new Mock<IGitHubService>();
            // Simulate CODEOWNERS file with an entry for /sdk/myservice/ with two owners
            string codeownersContent = "/sdk/myservice/ @oldowner @removeowner\n";
            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
                .ReturnsAsync(new RepositoryContent("CODEOWNERS", ".github/CODEOWNERS", "shaCode", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(codeownersContent)), null, null));
            gh.Setup(s => s.SearchPullRequestsByTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ItemState?>()))
                .ReturnsAsync(new List<PullRequest?>().AsReadOnly());
            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string?>()))
                .ReturnsAsync(new RepositoryContent("labels.md", "labels.md", "sha", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# Labels\n")), null, null));
            gh.Setup(s => s.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            gh.Setup(s => s.CreateBranchAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), It.IsAny<string>(), "main")).ReturnsAsync(CreateBranchStatus.Created);
            gh.Setup(s => s.UpdateFileAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            gh.Setup(s => s.CreatePullRequestAsync(It.IsAny<string>(), Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
                .ReturnsAsync(new PullRequestResult { Url = "https://pr", Messages = ["Created"] });

            var validator = new Mock<ICodeownersValidatorHelper>();
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new CodeownersValidationResult { Username = "user", IsValidCodeOwner = true });

            var tool = new CodeownersTool(
                gh.Object,
                new TestLogger<CodeownersTool>(),
                null, validator.Object,
                _mockcodeownersGenerateHelper.Object,
                _mockGitHelper.Object
            );
            // Remove @removeowner, keep @oldowner
            var result = await tool.UpdateCodeowners("repoName", false, "/sdk/myservice/", "MyService", ["@oldowner"], [], false);
            Assert.IsNotNull(result);
            Assert.That(result.ToString(), Does.Contain("URL:").And.Contains("Created"));
        }

        [Test]
        public async Task Update_CreateNewEntry_PRSuccess()
        {
            var gh = new Mock<IGitHubService>();

            // No existing CODEOWNERS PRs
            gh.Setup(s => s.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), "[CODEOWNERS]", It.IsAny<ItemState?>()))
                .ReturnsAsync(new List<PullRequest?>().AsReadOnly());

            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string?>()))
                .ReturnsAsync(new RepositoryContent("labels.md", "labels.md", "sha", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# Labels\n")), null, null));

            gh.Setup(s => s.GetContentsSingleAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS", It.IsAny<string>()))
                .ReturnsAsync(new RepositoryContent("CODEOWNERS", ".github/CODEOWNERS", "shaCode", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("")), null, null));

            gh.Setup(s => s.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);
            gh.Setup(s => s.CreateBranchAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), It.IsAny<string>(), "main"))
                .ReturnsAsync(CreateBranchStatus.Created);

            // Also return single-file content when GetContentsSingleAsync is used by CreateCodeownersPR
            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
                .ReturnsAsync(new RepositoryContent("CODEOWNERS", ".github/CODEOWNERS", "shaCode", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("")), null, null));

            gh.Setup(s => s.UpdateFileAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            gh.Setup(s => s.CreatePullRequestAsync(It.IsAny<string>(), Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
                .ReturnsAsync(new PullRequestResult { Url = "https://pr", Messages = ["Created"] });

            var validator = new Mock<ICodeownersValidatorHelper>();
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new CodeownersValidationResult { Username = "user", IsValidCodeOwner = true });

            var tool = new CodeownersTool(
                gh.Object,
                new TestLogger<CodeownersTool>(),
                null,
                validator.Object,
                _mockcodeownersGenerateHelper.Object,
                _mockGitHelper.Object
            );

            var result = await tool.UpdateCodeowners("repoName", false, "/sdk/newsvc/", "NewSvc", ["@a", "@b"], ["@s1", "@s2"], true);
            Assert.IsNotNull(result);
            Assert.That(result.ToString(), Does.Contain("URL:").And.Contains("Created"));
        }

        // ValidateCodeownersEntryForService - separate tests to avoid null in TestCase attribute
        [Test]
        public async Task ValidateCodeownersEntryForService_MissingLabelAndPath_ReturnsResult()
        {
            var result = await _tool.ValidateCodeownersEntryForService("test-repo", string.Empty, string.Empty);
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task ValidateCodeownersEntryForService_ByServiceLabel_ReturnsResult()
        {
            var result = await _tool.ValidateCodeownersEntryForService("test-repo", "My Service", null);
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task ValidateCodeownersEntryForService_ByPath_ReturnsResult()
        {
            var result = await _tool.ValidateCodeownersEntryForService("test-repo", null, "sdk/myservice/");
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task Validate_CODEOWNERS_Missing_ReturnsError()
        {
            var gh = new Mock<IGitHubService>();
            // Simulate missing CODEOWNERS file
            gh.Setup(s => s.SearchPullRequestsByTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ItemState?>()))
                .ReturnsAsync([]);
            gh.Setup(s => s.GetContentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync((IReadOnlyList<RepositoryContent>?)null);

            var validator = new Mock<ICodeownersValidatorHelper>();

            var generateHelper = new Mock<ICodeownersGenerateHelper>();
            var tool = new CodeownersTool(
                gh.Object,
                new TestLogger<CodeownersTool>(),
                null,
                validator.Object,
                generateHelper.Object,
                _mockGitHelper.Object
            );

            var result = await tool.ValidateCodeownersEntryForService("test-repo", "Any", null);
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.ResponseError);
            Assert.That(result.ToString(), Does.Contain("Could not retrieve upstream CODEOWNERS"));
        }

        [Test]
        public async Task Validate_NoMatchingEntry_ReturnsNotFound()
        {
            var result = await _tool.ValidateCodeownersEntryForService("test-repo", "NoMatch", null);
            Assert.IsNotNull(result);
            Assert.That(result.ToString(), Does.Contain("not found"));
        }

        [Test]
        [Ignore("This is an AI slop generated test and needs to be fixed")]
        public async Task Validate_MatchingEntry_Success()
        {
            // validator returns valid owners
            _mockCodeownersValidator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new CodeownersValidationResult { Username = "u", IsValidCodeOwner = true });

            var result = await _tool.ValidateCodeownersEntryForService("test-repo", "SvcOK", null);
            Assert.IsNotNull(result);
            Assert.That(result.ToString(), Does.Contain("Validation passed"));
        }
    }
}
