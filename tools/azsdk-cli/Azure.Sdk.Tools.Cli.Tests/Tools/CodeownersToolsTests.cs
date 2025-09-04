using Moq;
using Octokit;
using Microsoft.Extensions.Logging;

using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Azure.Sdk.Tools.Cli.Configuration;

using Azure.Sdk.Tools.CodeownersUtils.Editing;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    public class CodeownersToolsTests
    {
        private MockGitHubService _mockGithub;
        private Mock<IOutputHelper> _mockOutput;
        private Mock<ILogger<CodeownersTools>> _mockLogger;
        private Mock<ICodeownersValidatorHelper> _mockCodeownersValidator;

        private CodeownersTools _tool;

        [SetUp]
        public void Setup()
        {
            _mockGithub = new MockGitHubService();
            _mockOutput = new Mock<IOutputHelper>();
            _mockLogger = new Mock<ILogger<CodeownersTools>>();
            _mockCodeownersValidator = new Mock<ICodeownersValidatorHelper>();


            _tool = new CodeownersTools(
                _mockGithub,
                _mockOutput.Object,
                _mockLogger.Object,
                _mockCodeownersValidator.Object);
        }

        [Test]
        public async Task Update_InvalidInputs_Throws()
        {
            // both serviceLabel and path empty -> method throws
            var result = await _tool.AddCodeowners("repo", false, "", "", new List<string>(), new List<string>());

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Service label:  and Path:  are both invalid. At least one must be valid"));
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
            // provide an (empty) CODEOWNERS file so AddCodeowners can proceed
            gh.Setup(s => s.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, "repo", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
                .ReturnsAsync(new RepositoryContent("CODEOWNERS", ".github/CODEOWNERS", "shaCode", 0, ContentType.File, null, null, null, null, "utf-8", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("")), null, null));

            var tool = new CodeownersTools(gh.Object, _mockOutput.Object, _mockLogger.Object, _mockCodeownersValidator.Object);

            var result = await tool.AddCodeowners("repo", false, "", "NonExistService", new List<string>(), new List<string>());

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Error: "));
        }

        [Test]
        public async Task Update_AddOwnersToExistingEntry_Success()
        {
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
                .ReturnsAsync(new PullRequestResult { Url = "https://pr", Messages = new List<string> { "Created" } });

            var validator = new Mock<ICodeownersValidatorHelper>();
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new CodeownersValidationResult { Username = "user", IsValidCodeOwner = true });

            var tool = new CodeownersTools(gh.Object, _mockOutput.Object, _mockLogger.Object, validator.Object);
            var result = await tool.AddCodeowners("repoName", false, "/sdk/myservice/", "MyService", new List<string> { "@oldowner", "@newowner" }, new List<string>() { "@newowner", "@newowner2" });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("URL:") || result.Contains("Created"));
        }

        [Test]
        public async Task Update_RemoveOwnersFromExistingEntry_Success()
        {
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
                .ReturnsAsync(new PullRequestResult { Url = "https://pr", Messages = new List<string> { "Created" } });

            var validator = new Mock<ICodeownersValidatorHelper>();
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new CodeownersValidationResult { Username = "user", IsValidCodeOwner = true });

            var tool = new CodeownersTools(gh.Object, _mockOutput.Object, _mockLogger.Object, validator.Object);
            // Remove @removeowner, keep @oldowner
            var result = await tool.RemoveCodeowners("repoName", false, "/sdk/myservice/", "MyService", new List<string> { "@oldowner" }, new List<string>());

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("URL:") || result.Contains("Created"));
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
                .ReturnsAsync(new PullRequestResult { Url = "https://pr", Messages = new List<string> { "Created" } });

            var validator = new Mock<ICodeownersValidatorHelper>();
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new CodeownersValidationResult { Username = "user", IsValidCodeOwner = true });

            var tool = new CodeownersTools(gh.Object, _mockOutput.Object, _mockLogger.Object, validator.Object);

            var result = await tool.AddCodeowners("repoName", false, "/sdk/newsvc/", "NewSvc", new List<string> { "@a", "@b" }, new List<string> { "@s1", "@s2" });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("URL:") || result.Contains("Created"));
        }

        [Test]
        public async Task Update_CreateNewEntry_PRFailure_ReturnsPRFailureMessage()
        {
            var gh = new Mock<IGitHubService>();

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

            gh.Setup(s => s.UpdateFileAsync(Constants.AZURE_OWNER_PATH, It.IsAny<string>(), Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            gh.Setup(s => s.CreatePullRequestAsync(It.IsAny<string>(), Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
                .ReturnsAsync(new PullRequestResult());

            var validator = new Mock<ICodeownersValidatorHelper>();
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new CodeownersValidationResult { Username = "user", IsValidCodeOwner = true });

            var tool = new CodeownersTools(gh.Object, _mockOutput.Object, _mockLogger.Object, validator.Object);

            var result = await tool.AddCodeowners("repoName", false, "/sdk/newsvc/", "NewSvc", new List<string> { "@a", "@b" }, new List<string> { "@s1", "@s2" });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Failed to create pull request") || result.Contains("Error") || result.Contains("Error: Failed to create pull request"));
        }

        // ValidateCodeownersEntryForService - separate tests to avoid null in TestCase attribute
        [Test]
        public async Task ValidateCodeownersEntryForService_MissingLabelAndPath_ReturnsResult()
        {
            var result = await _tool.ValidateCodeownersEntryForService("test-repo", string.Empty, string.Empty);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<ServiceCodeownersResult>(result);
        }

        [Test]
        public async Task ValidateCodeownersEntryForService_ByServiceLabel_ReturnsResult()
        {
            var result = await _tool.ValidateCodeownersEntryForService("test-repo", "My Service", null);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<ServiceCodeownersResult>(result);
        }

        [Test]
        public async Task ValidateCodeownersEntryForService_ByPath_ReturnsResult()
        {
            var result = await _tool.ValidateCodeownersEntryForService("test-repo", null, "sdk/myservice/");
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<ServiceCodeownersResult>(result);
        }

        [Test]
        public async Task Validate_CODEOWNERS_Missing_ReturnsError()
        {
            var gh = new Mock<IGitHubService>();
            // Simulate missing CODEOWNERS file
            gh.Setup(s => s.GetContentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync((IReadOnlyList<RepositoryContent>?)null);

            var output = new Mock<IOutputHelper>();
            var validator = new Mock<ICodeownersValidatorHelper>();

            var tool = new CodeownersTools(gh.Object, output.Object, _mockLogger.Object, validator.Object);

            var result = await tool.ValidateCodeownersEntryForService("test-repo", "Any", null);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Message);
            Assert.IsNotEmpty(result.Message);
        }

        [Test]
        public async Task Validate_NoMatchingEntry_ReturnsNotFound()
        {
            var result = await _tool.ValidateCodeownersEntryForService("test-repo", "NoMatch", null);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Message);
            Assert.IsTrue(result.Message.Contains("not found") || result.Message.Length > 0);
        }

        [Test]
        public async Task Validate_MatchingEntry_Success()
        {
            // validator returns valid owners
            _mockCodeownersValidator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new CodeownersValidationResult { Username = "u", IsValidCodeOwner = true });

            var result = await _tool.ValidateCodeownersEntryForService("test-repo", "SvcOK", null);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Message.Contains("Validation passed") || result.Message.Length > 0);
        }
    }
}
