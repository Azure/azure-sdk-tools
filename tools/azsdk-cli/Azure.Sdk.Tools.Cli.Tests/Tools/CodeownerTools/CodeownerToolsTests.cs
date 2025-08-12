using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tools;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using CodeownerToolsType = Azure.Sdk.Tools.Cli.Tools.CodeownerTools;

namespace Azure.Sdk.Tools.Cli.Tests.CodeownerToolsSuite
{
    // Minimal fake IReadOnlyList to satisfy Count > 0 for GetContentsAsync without constructing RepositoryContent
    internal sealed class FakeRepoContentList : IReadOnlyList<Octokit.RepositoryContent>
    {
        private readonly int _count;
        public FakeRepoContentList(int count = 1) { _count = count; }
        public Octokit.RepositoryContent this[int index] => null!; // null items acceptable for our use (FirstOrDefault?.Content)
        public int Count => _count;
        public IEnumerator<Octokit.RepositoryContent> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return null!;
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [TestFixture]
    internal class CodeownerToolsTests
    {
        // Added fields initialized per-test in SetUp
        private CodeownerToolsType sut = null!;
        private Mock<IGitHubService> gh = null!;
        private Mock<IOutputService> output = null!;
        private Mock<ITypeSpecHelper> typespec = null!;
        private ICodeOwnerHelper helper = null!;
        private Mock<ICodeOwnerValidatorHelper> validator = null!;

        [SetUp]
        public void SetUp()
        {
            sut = CreateSut(out gh, out output, out typespec, out helper, out validator);
        }

        private static CodeownerToolsType CreateSut(
            out Mock<IGitHubService> gh,
            out Mock<IOutputService> output,
            out Mock<ITypeSpecHelper> typespec,
            out ICodeOwnerHelper helper,
            out Mock<ICodeOwnerValidatorHelper> validator)
        {
            gh = new Mock<IGitHubService>(MockBehavior.Strict);
            output = new Mock<IOutputService>(MockBehavior.Loose);
            typespec = new Mock<ITypeSpecHelper>(MockBehavior.Strict);
            helper = new CodeOwnerHelper();
            validator = new Mock<ICodeOwnerValidatorHelper>(MockBehavior.Strict);

            // Default behaviors that are commonly used
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);

            return new CodeownerToolsType(gh.Object, output.Object, typespec.Object, helper, validator.Object);
        }

        private static MethodInfo GetPrivateMethod(object sut, string name)
        {
            return sut.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                   ?? throw new AssertionException($"Could not find private method '{name}'");
        }

        private static void ClearValidationCache()
        {
            var toolsType = typeof(CodeownerToolsType);
            var field = toolsType.GetField("codeOwnerValidationCache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Validation cache field not found");
            field!.SetValue(null, new Dictionary<string, CodeOwnerValidationResult>());
        }

        private static List<Octokit.RepositoryContent> BuildLabelsContent(string csvPlainText)
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvPlainText));
            return new List<Octokit.RepositoryContent>
            {
                new Octokit.RepositoryContent(
                    name: "common-labels.csv",
                    path: Constants.AZURE_COMMON_LABELS_PATH,
                    sha: "sha-labels",
                    size: csvPlainText.Length,
                    type: Octokit.ContentType.File,
                    downloadUrl: "https://raw.githubusercontent.com/Azure/azure-sdk-tools/main/" + Constants.AZURE_COMMON_LABELS_PATH,
                    url: "https://api.github.com/repos/Azure/azure-sdk-tools/contents/" + Constants.AZURE_COMMON_LABELS_PATH,
                    htmlUrl: "",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: encoded,
                    target: null,
                    submoduleGitUrl: null)
            };
        }

        private static async Task<List<Octokit.RepositoryContent>> BuildCodeownersContentFromRepoAsync(string repo)
        {
            string url = $"https://raw.githubusercontent.com/Azure/{repo}/main/{Constants.AZURE_CODEOWNERS_PATH}";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var text = await http.GetStringAsync(url);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            return new List<Octokit.RepositoryContent>
            {
                new Octokit.RepositoryContent(
                    name: "CODEOWNERS",
                    path: Constants.AZURE_CODEOWNERS_PATH,
                    sha: "sha-codeowners",
                    size: text.Length,
                    type: Octokit.ContentType.File,
                    downloadUrl: url,
                    url: url,
                    htmlUrl: url,
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: encoded,
                    target: null,
                    submoduleGitUrl: null)
            };
        }

        [Test]
        public void GetCommand_ContainsExpectedSubcommands()
        {
            var sut = this.sut;
            var cmd = sut.GetCommand();
            var names = cmd.Subcommands.Select(c => c.Name).ToArray();

            Assert.That(names, Does.Contain("update-codeowners"));
            Assert.That(names, Does.Contain("validate-codeowner-entry"));
        }

        [Test]
        public async Task UpdateCodeowners_Fails_WhenServiceLabelAndPathMissing()
        {
            var sut = this.sut;
            // Prevent any unintended calls beyond input validation
            gh.Setup(g => g.GetContentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync((IReadOnlyList<Octokit.RepositoryContent>)null!);

            var result = await sut.UpdateCodeowners(
                repo: "azure-sdk-for-net",
                typeSpecProjectRoot: "path/to/spec",
                path: string.Empty,
                serviceLabel: string.Empty,
                serviceOwners: new List<string>(),
                sourceOwners: new List<string>(),
                isAdding: true,
                workingBranch: string.Empty);

            Assert.That(result, Does.StartWith("Error:"));
            Assert.That(result, Does.Contain("Atleast one must be valid"));
        }

        [Test]
        public async Task UpdateCodeowners_Fails_WhenCodeownersFileMissing()
        {
            var sut = this.sut;

            // Codeowners file missing
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync((IReadOnlyList<Octokit.RepositoryContent>)null!);

            var result = await sut.UpdateCodeowners(
                repo: "azure-sdk-for-net",
                typeSpecProjectRoot: "path/to/spec",
                path: "/sdk/compute/",
                serviceLabel: string.Empty,
                serviceOwners: new List<string>{"@ok1","@ok2"},
                sourceOwners: new List<string>{"@ok1","@ok2"},
                isAdding: true,
                workingBranch: string.Empty);

            Assert.That(result, Does.StartWith("Error:"));
            Assert.That(result, Does.Contain("Could not retrieve CODEOWNERS file"));
        }

        [Test]
        public async Task UpdateCodeowners_Fails_WhenLabelsFileMissing()
        {
            var sut = this.sut;

            // Pretend CODEOWNERS file exists by returning a fake list with Count=1
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(new FakeRepoContentList(1));

            // Labels file missing
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync((IReadOnlyList<Octokit.RepositoryContent>)null!);

            var result = await sut.UpdateCodeowners(
                repo: "azure-sdk-for-net",
                typeSpecProjectRoot: "path/to/spec",
                path: "/sdk/compute/",
                serviceLabel: "Azure.Compute",
                serviceOwners: new List<string>{"@ok1","@ok2"},
                sourceOwners: new List<string>{"@ok1","@ok2"},
                isAdding: true,
                workingBranch: string.Empty);

            Assert.That(result, Does.StartWith("Error:"));
            Assert.That(result, Does.Contain("Could not retrieve labels file"));
        }

        [TestCase("feature/codeowners-change", true)]
        [TestCase(null, false)]
        public async Task CreateCodeownerPR_CreatesOrUsesBranch_AndCreatesDraftPR(string? workingBranch, bool existingBranch)
        {
            var sut = this.sut;

            // Prepare private invoker
            var method = GetPrivateMethod(sut, "CreateCodeownerPR");

            // Set up GH interactions
            if (workingBranch != null)
            {
                gh.Setup(g => g.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", workingBranch))
                  .ReturnsAsync(existingBranch);
            }
            else
            {
                gh.Setup(g => g.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", It.IsAny<string>()))
                  .ReturnsAsync(existingBranch);
            }

            if (!existingBranch)
            {
                gh.Setup(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", It.IsAny<string>(), "main"))
                  .ReturnsAsync(CreateBranchStatus.Created);
            }

            gh.Setup(g => g.UpdateFileAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);

            // No existing PR so a new draft PR is created
            gh.Setup(g => g.GetPullRequestForBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", It.IsAny<string>()))
              .ReturnsAsync((Octokit.PullRequest?)null);

            gh.Setup(g => g.CreatePullRequestAsync("azure-sdk-for-net", Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
              .ReturnsAsync(new PullRequestResult { Url = "https://example/pr", Messages = new List<string> { "Pull request created successfully as draft PR." } });

            // Invoke private method
            var task = (Task<List<string>>)method.Invoke(sut, new object?[]
            {
                "azure-sdk-for-net",
                "modified-content",
                "sha123",
                "Update CODEOWNERS",
                "add-codeowner-alias",
                "Azure.Compute",
                workingBranch
            })!;
            var messages = await task;

            // Assertions
            if (existingBranch && workingBranch != null)
            {
                Assert.That(messages.Any(m => m.Contains("Using existing branch:")), Is.True);
                gh.Verify(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
            else
            {
                Assert.That(messages.Any(m => m.Contains("Created branch:")), Is.True);
                gh.Verify(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", It.IsAny<string>(), "main"), Times.Once);
            }

            gh.Verify(g => g.UpdateFileAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            gh.Verify(g => g.CreatePullRequestAsync("azure-sdk-for-net", Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
        }

        [TestCase("svc1", new[] { "ok1" }, new[] { "ok1", "ok2" }, new string[] { }, "There must be at least two valid service owners.")]
        [TestCase("svc2", new[] { "ok1", "bad1" }, new[] { "ok1" }, new[] { "ok1" }, "There must be at least two valid source owners.")]
        [TestCase("svc3", new[] { "ok1", "ok2" }, new[] { "ok1", "ok2" }, new[] { "ok1" }, "")]
        [TestCase("svc4", new[] { "ok1", "ok2" }, new[] { "ok1", "ok2" }, new[] { "ok1", "ok2" }, "")] // valid case
        public async Task ValidateMinimumOwnerRequirements_CoversOwnerRules(
            string svcLabel,
            string[] serviceOwners,
            string[] sourceOwners,
            string[] azureSdkOwners,
            string expectedErrorContains)
        {
            ClearValidationCache();
            var sut = this.sut;

            // Validator: usernames starting with "ok" are valid, others invalid
            validator
                .Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string username, bool _) => new CodeOwnerValidationResult
                {
                    Username = username,
                    IsValidCodeOwner = username.StartsWith("ok"),
                    HasWritePermission = username.StartsWith("ok")
                });

            var entry = new CodeownersEntry
            {
                ServiceLabels = new List<string> { svcLabel },
                PathExpression = "/sdk/sample/",
                ServiceOwners = serviceOwners.Select(u => $"@{u}").ToList(),
                SourceOwners = sourceOwners.Select(u => $"@{u}").ToList(),
                AzureSdkOwners = azureSdkOwners.Select(u => $"@{u}").ToList(),
                startLine = 0,
                endLine = 0
            };

            var method = GetPrivateMethod(sut, "ValidateMinimumOwnerRequirements");
            var task = (Task<(string, List<CodeOwnerValidationResult>)>)method.Invoke(sut, new object?[] { entry })!;
            var (errors, results) = await task;

            if (string.IsNullOrEmpty(expectedErrorContains))
            {
                Assert.That(errors, Is.EqualTo(""));
            }
            else
            {
                Assert.That(errors, Does.Contain(expectedErrorContains));
            }

            // Ensure validation results include all users
            var allOwners = serviceOwners.Concat(sourceOwners).Concat(azureSdkOwners).Select(u => u).ToHashSet();
            CollectionAssert.IsSubsetOf(allOwners, results.Select(r => r.Username));
        }

        [Test]
        public async Task ValidateCodeOwnerEntryForService_Errors_WhenMissingInputs()
        {
            var sut = this.sut;

            var missingRepo = await sut.ValidateCodeOwnerEntryForService("");
            Assert.That(missingRepo.Message, Does.StartWith("Error processing repository:"));
            Assert.That(missingRepo.Message, Does.Contain("Must provide a repository name"));

            var missingBoth = await sut.ValidateCodeOwnerEntryForService("azure-sdk-for-net");
            Assert.That(missingBoth.Message, Does.StartWith("Error processing repository:"));
            Assert.That(missingBoth.Message, Does.Contain("Must provide a service label or a repository path."));
        }

        [Test]
        public async Task CreateCodeownerPR_WorkingBranchProvidedButMissing_CreatesNewBranch()
        {
            var sut = this.sut;
            var method = GetPrivateMethod(sut, "CreateCodeownerPR");

            // workingBranch provided but not found
            gh.Setup(g => g.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", "feature/missing"))
              .ReturnsAsync(false);
            gh.Setup(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", It.IsAny<string>(), "main"))
              .ReturnsAsync(CreateBranchStatus.Created);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(new FakeRepoContentList());
            gh.Setup(g => g.UpdateFileAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
            gh.Setup(g => g.GetPullRequestForBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", It.IsAny<string>()))
              .ReturnsAsync((Octokit.PullRequest?)null);
            gh.Setup(g => g.CreatePullRequestAsync("azure-sdk-for-net", Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
              .ReturnsAsync(new PullRequestResult { Url = "https://example/pr", Messages = new List<string> { "Pull request created successfully as draft PR." } });

            var task = (Task<List<string>>)method.Invoke(sut, new object?[]
            {
                "azure-sdk-for-net",
                "modified-content",
                "sha123",
                "Update CODEOWNERS",
                "add-codeowner-alias",
                "Azure.Storage",
                "feature/missing"
            })!;
            var messages = await task;

            Assert.That(messages.Any(m => m.Contains("Created branch:")), Is.True);
            gh.Verify(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", It.IsAny<string>(), "main"), Times.Once);
        }

        [Test]
        public async Task ValidateOwners_CachesAcrossInvocations()
        {
            ClearValidationCache();
            var sut = this.sut;

            // Track invocation counts per-username
            var callCounts = new Dictionary<string, int>();
            validator
                .Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string username, bool _) =>
                {
                    callCounts[username] = callCounts.TryGetValue(username, out var n) ? n + 1 : 1;
                    return new CodeOwnerValidationResult
                    {
                        Username = username,
                        IsValidCodeOwner = true,
                        HasWritePermission = true
                    };
                });

            // Build one entry and validate twice
            var entry = new CodeownersEntry
            {
                ServiceLabels = new List<string> { "svc" },
                PathExpression = "/sdk/sample/",
                ServiceOwners = new List<string> { "@ok1", "@ok2" },
                SourceOwners = new List<string> { "@ok1" }, // duplicate ok1 across categories
                AzureSdkOwners = new List<string>()
            };

            var method = GetPrivateMethod(sut, "ValidateMinimumOwnerRequirements");
            var t1 = (Task<(string, List<CodeOwnerValidationResult>)>)method.Invoke(sut, new object?[] { entry })!;
            _ = await t1;

            // Second invocation with same users should hit cache
            var t2 = (Task<(string, List<CodeOwnerValidationResult>)>)method.Invoke(sut, new object?[] { entry })!;
            _ = await t2;

            Assert.That(callCounts.TryGetValue("ok1", out var c1) ? c1 : 0, Is.EqualTo(1), "ok1 should be validated only once due to cache");
            Assert.That(callCounts.TryGetValue("ok2", out var c2) ? c2 : 0, Is.EqualTo(1), "ok2 should be validated only once due to cache");
        }

        [Test]
        public async Task ValidateCodeOwnerEntryForService_ParserError_IsSurfaced()
        {
            var sut = this.sut;
            // Make every owner valid to ensure any later validation would pass if reached
            validator
                .Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string username, bool _) => new CodeOwnerValidationResult
                {
                    Username = username,
                    IsValidCodeOwner = true,
                    HasWritePermission = true
                });

            // Use a definitely non-existent repo to force parser failure
            var result = await sut.ValidateCodeOwnerEntryForService("not-a-real-repo-xyz-123", serviceLabel: "Any", repoPath: null);

            Assert.That(result.Message, Does.Contain("Error finding service in CODEOWNERS file."));
        }

        [Test, Category("Integration")]
        public async Task ValidateCodeOwnerEntryForService_Validates_ForRepoPath_WithMockedOwners()
        {
            var sut = this.sut;

            // All owners considered valid by the validator; this isolates validation aggregation in the method
            validator
                .Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string username, bool _) => new CodeOwnerValidationResult
                {
                    Username = username,
                    IsValidCodeOwner = true,
                    HasWritePermission = true
                });

            // Choose a common repo path that exists in azure-sdk-for-net
            var result = await sut.ValidateCodeOwnerEntryForService(
                repoName: "azure-sdk-for-net",
                serviceLabel: null,
                repoPath: "sdk/storage/");

            // We expect either a pass if the entry has >=2 owners, or a descriptive validation error otherwise.
            Assert.That(
                result.Message.Contains("Validation passed: minimum code owner requirements are met.") ||
                result.Message.Contains("There must be at least two valid source owners."),
                Is.True,
                $"Unexpected validation message: {result}");
        }

        [Test, Category("Integration")]
        public async Task UpdateCodeowners_AddNewEntry_ByNewServiceLabel_Succeeds()
        {
            var sut = this.sut;
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);

            // Real CODEOWNERS content so indices align when inserting
            var repoName = "azure-sdk-for-net";
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(await BuildCodeownersContentFromRepoAsync(repoName));

            // Labels contains our new service label
            var csv = "Contoso.UnitTest,,e99695\n";
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(BuildLabelsContent(csv));

            // Owners valid
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync((string username, bool _) => new CodeOwnerValidationResult
                     {
                         Username = username,
                         IsValidCodeOwner = true,
                         HasWritePermission = true
                     });

            // Branch/PR flow
            gh.Setup(g => g.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>())).ReturnsAsync(false);
            gh.Setup(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>(), "main")).ReturnsAsync(CreateBranchStatus.Created);

            string? capturedContent = null;
            gh.Setup(g => g.UpdateFileAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Callback<string, string, string, string, string, string, string>((_, _, _, _, content, _, _) => capturedContent = content)
              .Returns(Task.CompletedTask);

            gh.Setup(g => g.GetPullRequestForBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>()))
              .ReturnsAsync((Octokit.PullRequest?)null);
            gh.Setup(g => g.CreatePullRequestAsync(repoName, Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
              .ReturnsAsync(new PullRequestResult { Url = "https://example/pr", Messages = new List<string> { "Pull request created successfully as draft PR." } });

            var result = await sut.UpdateCodeowners(
                repo: repoName,
                typeSpecProjectRoot: "/spec/path",
                path: string.Empty,
                serviceLabel: "Contoso.UnitTest",
                serviceOwners: new List<string> { "@ok1", "@ok2" },
                sourceOwners: new List<string> { "@ok1", "@ok2" },
                isAdding: true,
                workingBranch: string.Empty);

            Assert.That(result, Does.Contain("Pull request created successfully as draft PR."));
            Assert.That(capturedContent, Is.Not.Null);
            Assert.That(capturedContent, Does.Contain("# ServiceLabel: %Contoso.UnitTest"));
        }

        [Test, Category("Integration")]
        public async Task UpdateCodeowners_AddOwners_ToExistingEntry_Succeeds()
        {
            var sut = this.sut;
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);

            var repoName = "azure-sdk-for-net";
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(await BuildCodeownersContentFromRepoAsync(repoName));

            // Labels include commonly used label
            var csv = "Service Bus,,e99695\n";
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(BuildLabelsContent(csv));

            // Validator: ok* valid
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync((string username, bool _) => new CodeOwnerValidationResult
                     {
                         Username = username,
                         IsValidCodeOwner = username.StartsWith("ok"),
                         HasWritePermission = username.StartsWith("ok")
                     });

            gh.Setup(g => g.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>())).ReturnsAsync(false);
            gh.Setup(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>(), "main")).ReturnsAsync(CreateBranchStatus.Created);
            gh.Setup(g => g.UpdateFileAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
            gh.Setup(g => g.GetPullRequestForBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>()))
              .ReturnsAsync((Octokit.PullRequest?)null);
            gh.Setup(g => g.CreatePullRequestAsync(repoName, Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
              .ReturnsAsync(new PullRequestResult { Url = "https://example/pr", Messages = new List<string> { "Pull request created successfully as draft PR." } });

            var result = await sut.UpdateCodeowners(
                repo: repoName,
                typeSpecProjectRoot: "/spec/path",
                path: string.Empty,
                serviceLabel: "Service Bus",
                serviceOwners: new List<string> { "@ok1", "@ok2" },
                sourceOwners: new List<string> { "@ok1", "@ok2" },
                isAdding: true,
                workingBranch: string.Empty);

            Assert.That(result, Does.Contain("Pull request created successfully as draft PR."));
        }

        [Test, Category("Integration")]
        public async Task UpdateCodeowners_PathAlreadyExists_UpdatesExistingEntry_CreatesPR()
        {
            var sut = this.sut;
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);
            var repoName = "azure-sdk-for-net";

            // Use real CODEOWNERS content so block indices align
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(await BuildCodeownersContentFromRepoAsync(repoName));

            // Dynamically select an existing path from parsed entries to ensure an exact match in the Client block
            var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repoName}/main/{Constants.AZURE_CODEOWNERS_PATH}";
            var entries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");
            var existingPath = entries.First(e => !string.IsNullOrEmpty(e.PathExpression) && e.PathExpression.StartsWith("/sdk/") && e.PathExpression.EndsWith("/")).PathExpression.Trim('/');

            var csv = "Service Bus,,e99695\n";
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(BuildLabelsContent(csv));

            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync(new CodeOwnerValidationResult { Username = "ok", IsValidCodeOwner = true, HasWritePermission = true });

            gh.Setup(g => g.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>())).ReturnsAsync(false);
            gh.Setup(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>(), "main")).ReturnsAsync(CreateBranchStatus.Created);
            gh.Setup(g => g.UpdateFileAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
            gh.Setup(g => g.GetPullRequestForBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>()))
              .ReturnsAsync((Octokit.PullRequest?)null);
            gh.Setup(g => g.CreatePullRequestAsync(repoName, Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
              .ReturnsAsync(new PullRequestResult { Url = "https://example/pr", Messages = new List<string> { "Pull request created successfully as draft PR." } });

            var result = await sut.UpdateCodeowners(
                repo: repoName,
                typeSpecProjectRoot: "/spec/path",
                path: existingPath,
                serviceLabel: string.Empty,
                serviceOwners: new List<string> { "@ok1", "@ok2" },
                sourceOwners: new List<string> { "@ok1", "@ok2" },
                isAdding: true,
                workingBranch: string.Empty);

            Assert.That(result, Does.Contain("Pull request created successfully as draft PR."));
        }

        [Test, Category("Integration")]
        public async Task UpdateCodeowners_InvalidServiceLabel_WithPath_AllowsProceeding()
        {
            var sut = this.sut;
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);
            var repoName = "azure-sdk-for-net";

            // Use real CODEOWNERS content
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(await BuildCodeownersContentFromRepoAsync(repoName));

            // Choose an existing path from parsed entries to ensure match by path even with invalid label
            var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repoName}/main/{Constants.AZURE_CODEOWNERS_PATH}";
            var entries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");
            var existingPath = entries.First(e => !string.IsNullOrEmpty(e.PathExpression) && e.PathExpression.StartsWith("/sdk/") && e.PathExpression.EndsWith("/")).PathExpression.Trim('/');

            // Labels CSV without the target label (invalid label scenario)
            var csv = "Service Bus,,e99695\n"; // does not contain NonExistentLabel
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(BuildLabelsContent(csv));

            // No PRs found for service labels (still invalid)
            gh.Setup(g => g.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, "Service Label", It.IsAny<Octokit.ItemState?>()))
              .ReturnsAsync(new List<Octokit.PullRequest?>());

            // Owners valid
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync(new CodeOwnerValidationResult { Username = "ok", IsValidCodeOwner = true, HasWritePermission = true });

            // Branch/PR flow proceeds
            gh.Setup(g => g.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>())).ReturnsAsync(false);
            gh.Setup(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>(), "main")).ReturnsAsync(CreateBranchStatus.Created);
            gh.Setup(g => g.UpdateFileAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
            gh.Setup(g => g.GetPullRequestForBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>()))
              .ReturnsAsync((Octokit.PullRequest?)null);
            gh.Setup(g => g.CreatePullRequestAsync(repoName, Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
              .ReturnsAsync(new PullRequestResult { Url = "https://example/pr", Messages = new List<string> { "Pull request created successfully as draft PR." } });

            var result = await sut.UpdateCodeowners(
                repo: repoName,
                typeSpecProjectRoot: "/spec/path",
                path: existingPath, // path provided
                serviceLabel: "NonExistentLabel", // invalid label
                serviceOwners: new List<string> { "@ok1", "@ok2" },
                sourceOwners: new List<string> { "@ok1", "@ok2" },
                isAdding: true,
                workingBranch: string.Empty);

            Assert.That(result, Does.Contain("Pull request created successfully as draft PR."));
            Assert.That(result, Does.Not.Contain("Service label: NonExistentLabel is invalid."));
        }

        [Test]
        public async Task UpdateCodeowners_PathDuplicateOutsideParsedBlock_ReturnsError()
        {
            var sut = this.sut;
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false); // use Client block
            var repoName = "azure-sdk-for-net";

            // Build CODEOWNERS content where the target path is OUTSIDE the parsed Client block
            var codeownersText = string.Join("\n", new[]
            {
                "# Client Libraries",
                "# End #", // ends the Client block immediately
                "# Other #",
                "/sdk/dup/                                                             @x"
            });
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(codeownersText));
            var contents = new List<Octokit.RepositoryContent>
            {
                new Octokit.RepositoryContent(
                    name: "CODEOWNERS",
                    path: Constants.AZURE_CODEOWNERS_PATH,
                    sha: "sha-test",
                    size: codeownersText.Length,
                    type: Octokit.ContentType.File,
                    downloadUrl: "",
                    url: "",
                    htmlUrl: "",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: encoded,
                    target: null,
                    submoduleGitUrl: null)
            };

            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(contents);

            // Labels file present (label not used since serviceLabel is empty)
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(BuildLabelsContent("Service Bus,,e99695\n"));

            // Add missing mock setups in case the method proceeds (though it shouldn't)
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync(new CodeOwnerValidationResult { Username = "ok", IsValidCodeOwner = true, HasWritePermission = true });

            gh.Setup(g => g.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>())).ReturnsAsync(false);
            gh.Setup(g => g.CreateBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>(), "main")).ReturnsAsync(CreateBranchStatus.Created);
            gh.Setup(g => g.UpdateFileAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
            gh.Setup(g => g.GetPullRequestForBranchAsync(Constants.AZURE_OWNER_PATH, repoName, It.IsAny<string>()))
              .ReturnsAsync((Octokit.PullRequest?)null);
            gh.Setup(g => g.CreatePullRequestAsync(repoName, Constants.AZURE_OWNER_PATH, "main", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
              .ReturnsAsync(new PullRequestResult { Url = "https://example/pr", Messages = new List<string> { "Pull request created successfully as draft PR." } });

            var result = await sut.UpdateCodeowners(
                repo: repoName,
                typeSpecProjectRoot: "/spec/path",
                path: "sdk/dup/",
                serviceLabel: string.Empty,
                serviceOwners: new List<string> { "@ok1", "@ok2" },
                sourceOwners: new List<string> { "@ok1", "@ok2" },
                isAdding: true,
                workingBranch: string.Empty);

                Assert.That(result, Does.StartWith("Error:"));
            Assert.That(result, Does.Contain("already exists in the CODEOWNERS file"));
        }
    }
}
