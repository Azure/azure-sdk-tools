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
using CodeownersToolsType = Azure.Sdk.Tools.Cli.Tools.CodeownersTools;

namespace Azure.Sdk.Tools.Cli.Tests.CodeownersToolsSuite
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
    internal class CodeownersToolsTests
    {
        // Added fields initialized per-test in SetUp
        private CodeownersToolsType sut = null!;
        private Mock<IGitHubService> gh = null!;
        private Mock<IOutputService> output = null!;
        private Mock<ITypeSpecHelper> typespec = null!;
        private ICodeownersHelper helper = null!;
        private Mock<ICodeownersValidatorHelper> validator = null!;

        [SetUp]
        public void SetUp()
        {
            sut = CreateSut(out gh, out output, out typespec, out helper, out validator);
            
            // Setup common mocks that most tests need
            // Setup for CODEOWNERS PR search (needed by most UpdateCodeowners tests)
            gh.Setup(g => g.SearchPullRequestsByTitleAsync(It.IsAny<string>(), It.IsAny<string>(), "CODEOWNERS", It.IsAny<Octokit.ItemState?>()))
              .ReturnsAsync(new List<Octokit.PullRequest?>());
        }

        private static CodeownersToolsType CreateSut(
            out Mock<IGitHubService> gh,
            out Mock<IOutputService> output,
            out Mock<ITypeSpecHelper> typespec,
            out ICodeownersHelper helper,
            out Mock<ICodeownersValidatorHelper> validator)
        {
            gh = new Mock<IGitHubService>(MockBehavior.Strict);
            output = new Mock<IOutputService>(MockBehavior.Loose);
            typespec = new Mock<ITypeSpecHelper>(MockBehavior.Strict);
            helper = new CodeownersHelper();
            validator = new Mock<ICodeownersValidatorHelper>(MockBehavior.Strict);

            // Default behaviors that are commonly used
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);

            return new CodeownersToolsType(gh.Object, output.Object, typespec.Object, helper, validator.Object);
        }

        private static MethodInfo GetPrivateMethod(object sut, string name)
        {
            return sut.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                   ?? throw new AssertionException($"Could not find private method '{name}'");
        }

        private static void ClearValidationCache()
        {
            var toolsType = typeof(CodeownersToolsType);
            var field = toolsType.GetField("codeownersValidationCache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Validation cache field not found");
            field!.SetValue(null, new Dictionary<string, CodeownersValidationResult>());
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
            Assert.That(names, Does.Contain("validate-codeowners-entry"));
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
                isAdding: true);

            Assert.That(result, Does.StartWith("Error:"));
            Assert.That(result, Does.Contain("At least one must be valid"));
        }

        [Test]
        public async Task UpdateCodeowners_Fails_WhenCodeownersFileMissing()
        {
            var sut = this.sut;

            // Labels file works but Codeowners file missing
            var labelsContent = BuildLabelsContent("Service Bus,,e99695\n");
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH))
              .ReturnsAsync(labelsContent.FirstOrDefault()!);

            // Codeowners file missing
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync((IReadOnlyList<Octokit.RepositoryContent>)null!);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH))
              .ReturnsAsync((Octokit.RepositoryContent)null!);

            var result = await sut.UpdateCodeowners(
                repo: "azure-sdk-for-net",
                typeSpecProjectRoot: "path/to/spec",
                path: "/sdk/compute/",
                serviceLabel: string.Empty,
                serviceOwners: new List<string>{"@ok1","@ok2"},
                sourceOwners: new List<string>{"@ok1","@ok2"},
                isAdding: true);

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

            // Labels file missing - need to setup both GetContentsAsync and GetContentsSingleAsync
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync((IReadOnlyList<Octokit.RepositoryContent>)null!);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH))
              .ReturnsAsync((Octokit.RepositoryContent)null!);

            var result = await sut.UpdateCodeowners(
                repo: "azure-sdk-for-net",
                typeSpecProjectRoot: "path/to/spec",
                path: "/sdk/compute/",
                serviceLabel: "Azure.Compute",
                serviceOwners: new List<string>{"@ok1","@ok2"},
                sourceOwners: new List<string>{"@ok1","@ok2"},
                isAdding: true);

            Assert.That(result, Does.StartWith("Error:"));
            Assert.That(result, Does.Contain("Could not retrieve labels file"));
        }

        [TestCase("feature/codeowners-change", true)]
        [TestCase(null, false)]
        public async Task CreateCodeownersPR_CreatesOrUsesBranch_AndCreatesDraftPR(string? workingBranch, bool existingBranch)
        {
            var sut = this.sut;

            // Prepare private invoker
            var method = GetPrivateMethod(sut, "CreateCodeownersPR");

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

            // Add missing GetContentsAsync setup for the branch
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, "azure-sdk-for-net", Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(new FakeRepoContentList(1));

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
                .ReturnsAsync((string username, bool _) => new CodeownersValidationResult
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
            var task = (Task<(string, List<CodeownersValidationResult>)>)method.Invoke(sut, new object?[] { entry })!;
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

            var missingRepo = await sut.ValidateCodeownersEntryForService("");
            Assert.That(missingRepo.Message, Does.StartWith("Error processing repository:"));
            Assert.That(missingRepo.Message, Does.Contain("Must provide a repository name"));

            var missingBoth = await sut.ValidateCodeownersEntryForService("azure-sdk-for-net");
            Assert.That(missingBoth.Message, Does.StartWith("Error processing repository:"));
            Assert.That(missingBoth.Message, Does.Contain("Must provide a service label or a repository path."));
        }

        [Test]
        public async Task CreateCodeownersPR_WorkingBranchProvidedButMissing_CreatesNewBranch()
        {
            var sut = this.sut;
            var method = GetPrivateMethod(sut, "CreateCodeownersPR");

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
                    return new CodeownersValidationResult
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
            var t1 = (Task<(string, List<CodeownersValidationResult>)>)method.Invoke(sut, new object?[] { entry })!;
            _ = await t1;

            // Second invocation with same users should hit cache
            var t2 = (Task<(string, List<CodeownersValidationResult>)>)method.Invoke(sut, new object?[] { entry })!;
            _ = await t2;

            Assert.That(callCounts.TryGetValue("ok1", out var c1) ? c1 : 0, Is.EqualTo(1), "ok1 should be validated only once due to cache");
            Assert.That(callCounts.TryGetValue("ok2", out var c2) ? c2 : 0, Is.EqualTo(1), "ok2 should be validated only once due to cache");
        }

        [Test, Category("Integration")]
        public async Task ValidateCodeOwnerEntryForService_Validates_ForRepoPath_WithMockedOwners()
        {
            var sut = this.sut;

            // All owners considered valid by the validator; this isolates validation aggregation in the method
            validator
                .Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string username, bool _) => new CodeownersValidationResult
                {
                    Username = username,
                    IsValidCodeOwner = true,
                    HasWritePermission = true
                });

            // Choose a common repo path that exists in azure-sdk-for-net
            var result = await sut.ValidateCodeownersEntryForService(
                repoName: "azure-sdk-for-net",
                serviceLabel: null,
                path: "/sdk/storage/");

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
            var codeownersContent = await BuildCodeownersContentFromRepoAsync(repoName);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(codeownersContent);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH))
              .ReturnsAsync(codeownersContent.FirstOrDefault()!);

            // Labels contains our new service label
            var csv = "Contoso.UnitTest,,e99695\n";
            var labelsContent = BuildLabelsContent(csv);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(labelsContent);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH))
              .ReturnsAsync(labelsContent.FirstOrDefault()!);

            // Owners valid
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync((string username, bool _) => new CodeownersValidationResult
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
                path: "/sdk/contoso/", // Add a path since new entry requires both
                serviceLabel: "Contoso.UnitTest",
                serviceOwners: new List<string> { "@ok1", "@ok2" },
                sourceOwners: new List<string> { "@ok1", "@ok2" },
                isAdding: true);

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
            var codeownersContent = await BuildCodeownersContentFromRepoAsync(repoName);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(codeownersContent);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH))
              .ReturnsAsync(codeownersContent.FirstOrDefault()!);

            // Labels include commonly used label
            var csv = "Service Bus,,e99695\n";
            var labelsContent = BuildLabelsContent(csv);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(labelsContent);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH))
              .ReturnsAsync(labelsContent.FirstOrDefault()!);

            // Validator: ok* valid
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync((string username, bool _) => new CodeownersValidationResult
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
                isAdding: true);

            Assert.That(result, Does.Contain("Pull request created successfully as draft PR."));
        }

        [Test, Category("Integration")]
        public async Task UpdateCodeowners_PathAlreadyExists_UpdatesExistingEntry_CreatesPR()
        {
            var sut = this.sut;
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);
            var repoName = "azure-sdk-for-net";

            // Use real CODEOWNERS content so block indices align
            var codeownersContent = await BuildCodeownersContentFromRepoAsync(repoName);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(codeownersContent);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH))
              .ReturnsAsync(codeownersContent.FirstOrDefault()!);

            // Dynamically select an existing path from parsed entries to ensure an exact match in the Client block
            var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repoName}/main/{Constants.AZURE_CODEOWNERS_PATH}";
            var entries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");
            var existingPath = entries.First(e => !string.IsNullOrEmpty(e.PathExpression) && e.PathExpression.StartsWith("/sdk/") && e.PathExpression.EndsWith("/")).PathExpression.Trim('/');

            var csv = "Service Bus,,e99695\n";
            var labelsContent = BuildLabelsContent(csv);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(labelsContent);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH))
              .ReturnsAsync(labelsContent.FirstOrDefault()!);

            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync(new CodeownersValidationResult { Username = "ok", IsValidCodeOwner = true, HasWritePermission = true });

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
                serviceLabel: "Service Bus", // Add service label for the existing path
                serviceOwners: new List<string> { "@ok1", "@ok2" },
                sourceOwners: new List<string> { "@ok1", "@ok2" },
                isAdding: true);

            Assert.That(result, Does.Contain("Pull request created successfully as draft PR."));
        }

        [Test, Category("Integration")]
        public async Task UpdateCodeowners_InvalidServiceLabel_WithPath_AllowsProceeding()
        {
            var sut = this.sut;
            typespec.Setup(t => t.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>())).Returns(false);
            var repoName = "azure-sdk-for-net";

            // Use real CODEOWNERS content
            var codeownersContent = await BuildCodeownersContentFromRepoAsync(repoName);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH, It.IsAny<string>()))
              .ReturnsAsync(codeownersContent);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, repoName, Constants.AZURE_CODEOWNERS_PATH))
              .ReturnsAsync(codeownersContent.FirstOrDefault()!);

            // Choose an existing path from parsed entries to ensure match by path even with invalid label
            var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repoName}/main/{Constants.AZURE_CODEOWNERS_PATH}";
            var entries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");
            var existingPath = entries.First(e => !string.IsNullOrEmpty(e.PathExpression) && e.PathExpression.StartsWith("/sdk/") && e.PathExpression.EndsWith("/")).PathExpression.Trim('/');

            // Labels CSV without the target label (invalid label scenario)
            var csv = "Service Bus,,e99695\n"; // does not contain NonExistentLabel
            var labelsContent = BuildLabelsContent(csv);
            gh.Setup(g => g.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, It.IsAny<string>()))
              .ReturnsAsync(labelsContent);
            gh.Setup(g => g.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH))
              .ReturnsAsync(labelsContent.FirstOrDefault()!);

            // No PRs found for service labels (still invalid)
            gh.Setup(g => g.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, "Service Label", It.IsAny<Octokit.ItemState?>()))
              .ReturnsAsync(new List<Octokit.PullRequest?>());

            // Owners valid
            validator.Setup(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>()))
                     .ReturnsAsync(new CodeownersValidationResult { Username = "ok", IsValidCodeOwner = true, HasWritePermission = true });

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
                isAdding: true);

            Assert.That(result, Does.Contain("Pull request created successfully as draft PR."));
            Assert.That(result, Does.Not.Contain("Service label: NonExistentLabel is invalid."));
        }
    }
}
