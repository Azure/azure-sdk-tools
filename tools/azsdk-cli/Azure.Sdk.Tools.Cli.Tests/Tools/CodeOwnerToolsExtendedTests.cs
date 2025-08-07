using Azure.Sdk.Tools.Cli.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    internal class CodeOwnerToolsExtendedTests
    {
        private CodeownerTools codeownerTools;
        private Mock<IGitHubService> mockGitHubService;
        private Mock<IOutputService> mockOutputService;
        private Mock<ILogger<CodeownerTools>> mockLogger;
        private Mock<ITypeSpecHelper> mockTypeSpecHelper;
        private Mock<ICodeOwnerHelper> mockCodeOwnerHelper;
        private Mock<ICodeOwnerValidatorHelper> mockCodeOwnerValidator;
        private Mock<ILabelHelper> mockLabelHelper;

        [SetUp]
        public void Setup()
        {
            mockGitHubService = new Mock<IGitHubService>();
            mockOutputService = new Mock<IOutputService>();
            mockLogger = new Mock<ILogger<CodeownerTools>>();
            mockTypeSpecHelper = new Mock<ITypeSpecHelper>();
            mockCodeOwnerHelper = new Mock<ICodeOwnerHelper>();
            mockCodeOwnerValidator = new Mock<ICodeOwnerValidatorHelper>();
            mockLabelHelper = new Mock<ILabelHelper>();

            codeownerTools = new CodeownerTools(
                mockGitHubService.Object,
                mockOutputService.Object,
                mockLogger.Object,
                mockTypeSpecHelper.Object,
                mockCodeOwnerHelper.Object,
                mockCodeOwnerValidator.Object,
                mockLabelHelper.Object);
        }

        #region UpdateCodeowners Edge Cases

        [Test]
        [TestCase("", "", "Service label:  and Path:  are both invalid")]
        [TestCase("   ", "   ", "Service label:     and Path:     are both invalid")]
        public async Task UpdateCodeowners_InvalidInputValidation_EmptyStrings(string serviceLabel, string path, string expectedErrorFragment)
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team" };
            var sourceOwners = new List<string> { "@azure/source-team" };

            // Act
            var result = await codeownerTools.UpdateCodeowners(
                "azure-sdk-for-net", 
                "/test/typespec/project", 
                path, 
                serviceLabel, 
                serviceOwners, 
                sourceOwners, 
                true);

            // Assert
            Assert.That(result, Does.Contain(expectedErrorFragment));
        }

        [Test]
        [TestCase("Service Bus", "")]
        [TestCase("", "sdk/test/")]
        [TestCase(" Service Bus ", " sdk/test/ ")]
        public async Task UpdateCodeowners_ValidInputValidation(string serviceLabel, string path)
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team" };
            var sourceOwners = new List<string> { "@azure/source-team" };

            SetupValidCodeownersFileMocks();
            SetupValidLabelsMocks();
            SetupValidOwnerValidation();
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            // Act
            var result = await codeownerTools.UpdateCodeowners(
                "azure-sdk-for-net", 
                "/test/typespec/project", 
                path, 
                serviceLabel, 
                serviceOwners, 
                sourceOwners, 
                true);

            // Assert
            // Should not contain the validation error
            Assert.That(result, Does.Not.Contain("are both invalid"));
        }

        [Test]
        public async Task UpdateCodeowners_NullInputs_ReturnsError()
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team" };
            var sourceOwners = new List<string> { "@azure/source-team" };

            // Act
            var result = await codeownerTools.UpdateCodeowners(
                "azure-sdk-for-net", 
                "/test/typespec/project", 
                null, 
                null, 
                serviceOwners, 
                sourceOwners, 
                true);

            // Assert
            Assert.That(result, Does.Contain("Service label:  and Path:  are both invalid"));
        }

        [Test]
        [TestCase("azure-sdk-for-net")]
        [TestCase("azure-sdk-for-python")]
        [TestCase("azure-sdk-for-java")]
        [TestCase("azure-sdk-for-js")]
        [TestCase("azure-sdk-for-go")]
        [TestCase("azure-rest-api-specs")]
        [TestCase("azure-cli")]
        [TestCase("azure-powershell")]
        public async Task UpdateCodeowners_DifferentRepositories_HandlesProperly(string repoName)
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team1", "@azure/service-team2" };
            var sourceOwners = new List<string> { "@azure/source-team1", "@azure/source-team2" };

            SetupValidCodeownersFileMocks(repoName);
            SetupValidLabelsMocks();
            SetupValidOwnerValidation();
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            // Act
            var result = await codeownerTools.UpdateCodeowners(
                repoName, 
                "/test/typespec/project", 
                "sdk/test/", 
                "Service Bus", 
                serviceOwners, 
                sourceOwners, 
                true);

            // Assert
            Assert.That(result, Does.Not.Contain("Error:"));
            mockGitHubService.Verify(x => x.GetContentsAsync("Azure", repoName, ".github/CODEOWNERS"), Times.Once);
        }

        [Test]
        [TestCase(true, "# Management Libraries")]
        [TestCase(false, "# Client Libraries")]
        public async Task UpdateCodeowners_ManagementPlaneDetection(bool isMgmtPlane, string expectedCategory)
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team1", "@azure/service-team2" };
            var sourceOwners = new List<string> { "@azure/source-team1", "@azure/source-team2" };

            SetupValidCodeownersFileMocks();
            SetupValidLabelsMocks();
            SetupValidOwnerValidation();
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(isMgmtPlane);

            // Act
            await codeownerTools.UpdateCodeowners(
                "azure-sdk-for-net", 
                "/test/typespec/project", 
                "sdk/test/", 
                "Service Bus", 
                serviceOwners, 
                sourceOwners, 
                true);

            // Assert
            mockCodeOwnerHelper.Verify(x => x.findBlock(It.IsAny<string>(), expectedCategory), Times.Once);
        }

        [Test]
        [TestCase("sdk/test", "/sdk/test/")]
        [TestCase("/sdk/test", "/sdk/test/")]
        [TestCase("sdk/test/", "/sdk/test/")]
        [TestCase("/sdk/test/", "/sdk/test/")]
        [TestCase("///sdk/test///", "/sdk/test/")]
        [TestCase("sdk//test//", "/sdk/test/")]
        [TestCase("SDK/TEST", "/SDK/TEST/")]
        [TestCase("", "//")]
        public async Task UpdateCodeowners_PathNormalization(string inputPath, string expectedNormalizedPath)
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team1", "@azure/service-team2" };
            var sourceOwners = new List<string> { "@azure/source-team1", "@azure/source-team2" };

            SetupValidCodeownersFileMocks();
            SetupValidLabelsMocks();
            SetupValidOwnerValidation();
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            var capturedPath = "";
            mockCodeOwnerHelper.Setup(x => x.findAlphabeticalInsertionPoint(It.IsAny<List<CodeownersEntry>>(), It.IsAny<string>(), It.IsAny<string>()))
                              .Callback<List<CodeownersEntry>, string, string>((entries, path, serviceLabel) => capturedPath = path)
                              .Returns(1);

            // Act
            await codeownerTools.UpdateCodeowners(
                "azure-sdk-for-net", 
                "/test/typespec/project", 
                inputPath, 
                "Service Bus", 
                serviceOwners, 
                sourceOwners, 
                true);

            // Assert
            Assert.That(capturedPath, Is.EqualTo(expectedNormalizedPath));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(10)]
        public async Task UpdateCodeowners_VariableOwnerCounts(int ownerCount)
        {
            // Arrange
            var serviceOwners = Enumerable.Range(1, ownerCount).Select(i => $"@azure/service-team{i}").ToList();
            var sourceOwners = Enumerable.Range(1, Math.Max(2, ownerCount)).Select(i => $"@azure/source-team{i}").ToList(); // Ensure at least 2 for validation

            SetupValidCodeownersFileMocks();
            SetupValidLabelsMocks();
            SetupValidOwnerValidation();
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            // Act & Assert
            if (ownerCount < 2 && serviceOwners.Count > 0)
            {
                var result = await codeownerTools.UpdateCodeowners(
                    "azure-sdk-for-net", 
                    "/test/typespec/project", 
                    "", 
                    "Service Bus", 
                    serviceOwners, 
                    sourceOwners, 
                    true);
                Assert.That(result, Does.Contain("There must be at least two valid service owners"));
            }
            else
            {
                var result = await codeownerTools.UpdateCodeowners(
                    "azure-sdk-for-net", 
                    "/test/typespec/project", 
                    "", 
                    "Service Bus", 
                    serviceOwners, 
                    sourceOwners, 
                    true);
                Assert.That(result, Does.Not.Contain("Error:"));
            }
        }

        #endregion

        #region ValidateCodeOwnerEntryForService Edge Cases

        [Test]
        [TestCase("azure-sdk-for-net", "Service Bus", "")]
        [TestCase("azure-sdk-for-python", "Storage", "")]
        [TestCase("azure-rest-api-specs", "Communication", "")]
        public async Task ValidateCodeOwnerEntryForService_ValidInputs_WithServiceLabel(string repoName, string serviceLabel, string repoPath)
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "sdk/servicebus/",
                    ServiceLabels = new List<string> { serviceLabel },
                    SourceOwners = new List<string> { "@azure/servicebus-team" }
                }
            };

            mockCodeOwnerHelper.Setup(x => x.FindMatchingEntries(It.IsAny<List<CodeownersEntry>>(), serviceLabel, repoPath))
                              .Returns(entries.Cast<CodeownersEntry?>().ToList());
            
            mockCodeOwnerHelper.Setup(x => x.ExtractUniqueOwners(It.IsAny<CodeownersEntry>()))
                              .Returns(new List<string> { "@azure/servicebus-team" });

            var validationResult = new CodeOwnerValidationResult
            {
                Username = "azure/servicebus-team",
                IsValidCodeOwner = true,
                Status = "Success"
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            // Act
            var result = await codeownerTools.ValidateCodeOwnerEntryForService(repoName, serviceLabel, repoPath);

            // Assert
            Assert.That(result.Message, Does.Contain("Successfully found and validated codeowners"));
            Assert.That(result.Repository, Is.EqualTo(repoName));
        }

        [Test]
        [TestCase("azure-sdk-for-net", "", "sdk/servicebus/")]
        [TestCase("azure-sdk-for-java", "", "sdk/storage/")]
        [TestCase("azure-cli", "", "src/azure-cli/")]
        public async Task ValidateCodeOwnerEntryForService_ValidInputs_WithRepoPath(string repoName, string serviceLabel, string repoPath)
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = repoPath,
                    ServiceLabels = new List<string> { "Service Bus" },
                    SourceOwners = new List<string> { "@azure/servicebus-team" }
                }
            };

            mockCodeOwnerHelper.Setup(x => x.FindMatchingEntries(It.IsAny<List<CodeownersEntry>>(), serviceLabel, repoPath))
                              .Returns(entries.Cast<CodeownersEntry?>().ToList());
            
            mockCodeOwnerHelper.Setup(x => x.ExtractUniqueOwners(It.IsAny<CodeownersEntry>()))
                              .Returns(new List<string> { "@azure/servicebus-team" });

            var validationResult = new CodeOwnerValidationResult
            {
                Username = "azure/servicebus-team",
                IsValidCodeOwner = true,
                Status = "Success"
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            // Act
            var result = await codeownerTools.ValidateCodeOwnerEntryForService(repoName, serviceLabel, repoPath);

            // Assert
            Assert.That(result.Message, Does.Contain("Successfully found and validated codeowners"));
            Assert.That(result.Repository, Is.EqualTo(repoName));
        }

        [Test]
        [TestCase("azure-sdk-for-net", "", "", "Must provide a service label or a repository path")]
        [TestCase("", "", "", "Must provide a service label or a repository path")]
        [TestCase("", "Service Bus", "", "Must provide a service label or a repository path")]
        public async Task ValidateCodeOwnerEntryForService_InvalidInputs(string repoName, string serviceLabel, string repoPath, string expectedError)
        {
            // Act
            var result = await codeownerTools.ValidateCodeOwnerEntryForService(repoName, serviceLabel, repoPath);

            // Assert
            Assert.That(result.Message, Does.Contain(expectedError));
        }

        [Test]
        public async Task ValidateCodeOwnerEntryForService_NullInputs_ReturnsError()
        {
            // Act
            var result = await codeownerTools.ValidateCodeOwnerEntryForService("", null, null);

            // Assert
            Assert.That(result.Message, Does.Contain("Must provide a service label or a repository path"));
        }

        [Test]
        [TestCase("NonExistentService")]
        [TestCase("Invalid Service")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Service@#$%")]
        public async Task ValidateCodeOwnerEntryForService_ServiceNotFound(string serviceLabel)
        {
            // Arrange
            mockCodeOwnerHelper.Setup(x => x.FindMatchingEntries(It.IsAny<List<CodeownersEntry>>(), serviceLabel, It.IsAny<string>()))
                              .Returns(new List<CodeownersEntry?>());

            // Act
            var result = await codeownerTools.ValidateCodeOwnerEntryForService("azure-sdk-for-net", serviceLabel);

            // Assert
            Assert.That(result.Message, Does.Contain($"Service label '{serviceLabel}' or Repo Path '' not found"));
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public async Task UpdateCodeowners_GitHubServiceFailure_ReturnsError()
        {
            // Arrange
            mockGitHubService.Setup(x => x.GetContentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ThrowsAsync(new Exception("GitHub API failure"));

            // Act
            var result = await codeownerTools.UpdateCodeowners(
                "azure-sdk-for-net", 
                "/test/typespec/project", 
                "sdk/test/", 
                "Service Bus", 
                new List<string> { "@azure/team1", "@azure/team2" }, 
                new List<string> { "@azure/team1", "@azure/team2" }, 
                true);

            // Assert
            Assert.That(result, Does.StartWith("Error:"));
            Assert.That(result, Does.Contain("GitHub API failure"));
        }

        [Test]
        public async Task ValidateCodeOwnerEntryForService_ExceptionHandling_ReturnsErrorMessage()
        {
            // Arrange
            mockCodeOwnerHelper.Setup(x => x.FindMatchingEntries(It.IsAny<List<CodeownersEntry>>(), It.IsAny<string>(), It.IsAny<string>()))
                              .Throws(new Exception("Parser error"));

            // Act
            var result = await codeownerTools.ValidateCodeOwnerEntryForService("azure-sdk-for-net", "Service Bus");

            // Assert
            Assert.That(result.Message, Does.Contain("Error processing repository"));
            Assert.That(result.Message, Does.Contain("Parser error"));
        }

        [Test]
        public async Task UpdateCodeowners_InvalidServiceLabel_ThrowsException()
        {
            // Arrange
            SetupValidCodeownersFileMocks();
            SetupInvalidLabelsMocks();
            SetupValidOwnerValidation();
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            // Act
            var result = await codeownerTools.UpdateCodeowners(
                "azure-sdk-for-net", 
                "/test/typespec/project", 
                "", 
                "Invalid Service", 
                new List<string> { "@azure/team1", "@azure/team2" }, 
                new List<string> { "@azure/team1", "@azure/team2" }, 
                true);

            // Assert
            Assert.That(result, Does.StartWith("Error:"));
            Assert.That(result, Does.Contain("Service label: Invalid Service is invalid"));
        }

        #endregion

        #region Helper Methods

        private void SetupValidCodeownersFileMocks(string repoName = "azure-sdk-for-net")
        {
            var mockFileContent = new List<RepositoryContent>
            {
                new RepositoryContent(
                    name: "CODEOWNERS",
                    path: ".github/CODEOWNERS",
                    sha: "sha123",
                    size: 100,
                    type: ContentType.File,
                    downloadUrl: $"https://raw.githubusercontent.com/Azure/{repoName}/main/.github/CODEOWNERS",
                    url: $"https://api.github.com/repos/Azure/{repoName}/contents/.github/CODEOWNERS",
                    htmlUrl: $"https://github.com/Azure/{repoName}/blob/main/.github/CODEOWNERS",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: "IyBDb2Rld3JuZXJzIGZpbGU=", // base64 for "# Codeowners file"
                    target: null,
                    submoduleGitUrl: null
                )
            };
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", repoName, ".github/CODEOWNERS"))
                            .ReturnsAsync(mockFileContent);
        }

        private void SetupValidLabelsMocks()
        {
            var mockLabelsContent = new List<RepositoryContent>
            {
                new RepositoryContent(
                    name: "common-labels.csv",
                    path: "tools/github/data/common-labels.csv",
                    sha: "sha456",
                    size: 100,
                    type: ContentType.File,
                    downloadUrl: "https://raw.githubusercontent.com/Azure/azure-sdk-tools/main/tools/github/data/common-labels.csv",
                    url: "https://api.github.com/repos/Azure/azure-sdk-tools/contents/tools/github/data/common-labels.csv",
                    htmlUrl: "https://github.com/Azure/azure-sdk-tools/blob/main/tools/github/data/common-labels.csv",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: "U2VydmljZSBCdXMsaHR0cHM6Ly9naXRodWIuY29tL0F6dXJlL2F6dXJlLXNkay1mb3ItbmV0L2lzc3Vlcy9uZXc=", // base64 for "Service Bus,https://github.com/Azure/azure-sdk-for-net/issues/new"
                    target: null,
                    submoduleGitUrl: null
                )
            };
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv"))
                            .ReturnsAsync(mockLabelsContent);

            mockLabelHelper.Setup(x => x.CheckServiceLabel(It.IsAny<string>(), "Service Bus"))
                          .Returns(LabelHelper.ServiceLabelStatus.Exists);
        }

        private void SetupInvalidLabelsMocks()
        {
            var mockLabelsContent = new List<RepositoryContent>
            {
                new RepositoryContent(
                    name: "common-labels.csv",
                    path: "tools/github/data/common-labels.csv",
                    sha: "sha456",
                    size: 100,
                    type: ContentType.File,
                    downloadUrl: "https://raw.githubusercontent.com/Azure/azure-sdk-tools/main/tools/github/data/common-labels.csv",
                    url: "https://api.github.com/repos/Azure/azure-sdk-tools/contents/tools/github/data/common-labels.csv",
                    htmlUrl: "https://github.com/Azure/azure-sdk-tools/blob/main/tools/github/data/common-labels.csv",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: "U2VydmljZSBCdXMsaHR0cHM6Ly9naXRodWIuY29tL0F6dXJlL2F6dXJlLXNkay1mb3ItbmV0L2lzc3Vlcy9uZXc=",
                    target: null,
                    submoduleGitUrl: null
                )
            };
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv"))
                            .ReturnsAsync(mockLabelsContent);

            mockLabelHelper.Setup(x => x.CheckServiceLabel(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(LabelHelper.ServiceLabelStatus.DoesNotExist);

            mockLabelHelper.Setup(x => x.CheckServiceLabelInReview(It.IsAny<List<PullRequest>>(), It.IsAny<string>()))
                          .Returns(false);

            mockGitHubService.Setup(x => x.SearchPullRequestsByTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ItemState?>()))
                            .ReturnsAsync(new List<PullRequest?>());
        }

        private void SetupValidOwnerValidation()
        {
            var validationResult = new CodeOwnerValidationResult
            {
                Username = "azure/test-team",
                IsValidCodeOwner = true,
                Status = "Success"
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            mockCodeOwnerHelper.Setup(x => x.AddUniqueOwners(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
                              .Returns<List<string>, List<string>>((existing, toAdd) => existing.Concat(toAdd).ToList());

            mockCodeOwnerHelper.Setup(x => x.findAlphabeticalInsertionPoint(It.IsAny<List<CodeownersEntry>>(), It.IsAny<string>(), It.IsAny<string>()))
                              .Returns(1);

            mockCodeOwnerHelper.Setup(x => x.addCodeownersEntryAtIndex(It.IsAny<string>(), It.IsAny<CodeownersEntry>(), It.IsAny<int>(), It.IsAny<bool>()))
                              .Returns("updated content");

            mockCodeOwnerHelper.Setup(x => x.CreateBranchName(It.IsAny<string>(), It.IsAny<string>()))
                              .Returns("test-branch-name");

            mockGitHubService.Setup(x => x.GetBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync(false);

            mockGitHubService.Setup(x => x.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync(CreateBranchStatus.Created);

            mockGitHubService.Setup(x => x.UpdateFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .Returns(Task.CompletedTask);

            mockGitHubService.Setup(x => x.GetPullRequestForBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync((PullRequest?)null);

            mockGitHubService.Setup(x => x.CreatePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                            .ReturnsAsync(new List<string> { "PR created successfully" });
        }

        #endregion
    }
}
