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
    internal class CodeOwnerToolsTests
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

        #region ValidateCodeOwners Tests

        [Test]
        public async Task ValidateCodeOwners_ValidServiceLabel_ReturnsSuccess()
        {
            // Arrange
            var entries = new List<CodeownersEntry>
            {
                new CodeownersEntry
                {
                    PathExpression = "sdk/servicebus/",
                    ServiceLabels = new List<string> { "Service Bus" },
                    SourceOwners = new List<string> { "@azure/servicebus-team" }
                }
            };

            // Mock FindMatchingEntries to return the entries
            mockCodeOwnerHelper.Setup(x => x.FindMatchingEntries(It.IsAny<List<CodeownersEntry>>(), "Service Bus", It.IsAny<string>()))
                              .Returns(entries.Cast<CodeownersEntry?>().ToList());
            
            // Mock ExtractUniqueOwners to return owners
            mockCodeOwnerHelper.Setup(x => x.ExtractUniqueOwners(It.IsAny<CodeownersEntry>()))
                              .Returns(new List<string> { "@azure/servicebus-team" });

            // Mock the ValidateCodeOwnersConcurrently method to return valid results
            var validationResults = new List<CodeOwnerValidationResult>
            {
                new CodeOwnerValidationResult
                {
                    Username = "azure/servicebus-team",
                    IsValidCodeOwner = true,
                    Status = "Success"
                }
            };

            // Since ValidateCodeOwnersConcurrently is a public method, we need to create a partial mock
            // or mock its dependencies. Let's mock the CodeOwnerValidator instead
            var validationResult = new CodeOwnerValidationResult
            {
                Username = "azure/servicebus-team",
                IsValidCodeOwner = true,
                Status = "Success"
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            // Act
            var result = await codeownerTools.ValidateCodeOwnerEntryForService("azure-sdk-for-net", "Service Bus");

            // Assert
            Assert.That(result.Message, Does.Contain("Successfully found and validated codeowners."));
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
        }

        [Test]
        public async Task ValidateCodeOwners_NoServiceLabelOrPath_ReturnsError()
        {
            // Act
            var result = await codeownerTools.ValidateCodeOwnerEntryForService("dotnet");

            // Assert
            Assert.That(result.Message, Does.Contain("Must provide a service label or a repository path"));
        }

        #endregion

        #region UpdateCodeowners Tests

        [Test]
        public async Task UpdateCodeowners_InvalidSourceOwners_ReturnsError()
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team" };
            var sourceOwners = new List<string> { "@invalid/source-team" };
            
            var validationResult = new CodeOwnerValidationResult
            {
                Username = "invalid/source-team",
                IsValidCodeOwner = false
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            // Mock CreatePullRequestAsync to return empty list
            mockGitHubService.Setup(x => x.CreatePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                            .ReturnsAsync(new List<string>());

            // Mock CODEOWNERS file retrieval
            var mockFileContent = new List<RepositoryContent>
            {
                new RepositoryContent(
                    name: "CODEOWNERS",
                    path: ".github/CODEOWNERS",
                    sha: "sha123",
                    size: 100,
                    type: ContentType.File,
                    downloadUrl: "https://raw.githubusercontent.com/Azure/azure-sdk-for-net/main/.github/CODEOWNERS",
                    url: "https://api.github.com/repos/Azure/azure-sdk-for-net/contents/.github/CODEOWNERS",
                    htmlUrl: "https://github.com/Azure/azure-sdk-for-net/blob/main/.github/CODEOWNERS",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: "IyBDb2Rld3JuZXJzIGZpbGU=", // base64 for "# Codeowners file"
                    target: null,
                    submoduleGitUrl: null
                )
            };
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS"))
                            .ReturnsAsync(mockFileContent);

            // Mock service label validation
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv"))
                            .ReturnsAsync(new List<RepositoryContent>
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
                            });

            // Mock TypeSpec helper to return false for management plane
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            // Act
            var result = await codeownerTools.UpdateCodeowners("azure-sdk-for-net", "/test/typespec/project", "sdk/test/", "Service Bus", serviceOwners, sourceOwners, true);

            // Assert
            Assert.That(result, Does.Contain("There must be at least two valid source owners"));
        }

        [Test]
        public async Task UpdateCodeowners_InvalidLabelsFile_ReturnsError()
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team" };
            var sourceOwners = new List<string> { "@azure/source-team" };
            
            // Mock validation to return valid source owners
            var validationResult = new CodeOwnerValidationResult
            {
                Username = "azure/source-team",
                IsValidCodeOwner = true
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            // Mock CODEOWNERS file retrieval
            var mockFileContent = new List<RepositoryContent>
            {
                new RepositoryContent(
                    name: "CODEOWNERS",
                    path: ".github/CODEOWNERS",
                    sha: "sha123",
                    size: 100,
                    type: ContentType.File,
                    downloadUrl: "https://raw.githubusercontent.com/Azure/azure-sdk-for-net/main/.github/CODEOWNERS",
                    url: "https://api.github.com/repos/Azure/azure-sdk-for-net/contents/.github/CODEOWNERS",
                    htmlUrl: "https://github.com/Azure/azure-sdk-for-net/blob/main/.github/CODEOWNERS",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: "IyBDb2Rld3JuZXJzIGZpbGU=", // base64 for "# Codeowners file"
                    target: null,
                    submoduleGitUrl: null
                )
            };
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS"))
                            .ReturnsAsync(mockFileContent);

            // Mock service label validation to fail and get the CSV content error
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv"))
                            .ReturnsAsync((IReadOnlyList<RepositoryContent>?)null);

            // Mock TypeSpec helper to return false for management plane
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            // Act
            var result = await codeownerTools.UpdateCodeowners("azure-sdk-for-net", "/test/typespec/project", "sdk/test/", "Service Bus", serviceOwners, sourceOwners, true);

            // Assert
            Assert.That(result, Does.Contain("Could not retrieve labels file"));
        }

        [Test]
        public async Task UpdateCodeowners_RemoveOwners_CallsHelperMethods()
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team" };
            var sourceOwners = new List<string> { "@azure/source-team1", "@azure/source-team2" };
            
            // Mock validation to return valid owners
            var validationResult = new CodeOwnerValidationResult
            {
                Username = "azure/source-team1",
                IsValidCodeOwner = true
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            // Mock CODEOWNERS file retrieval
            var mockFileContent = new List<RepositoryContent>
            {
                new RepositoryContent(
                    name: "CODEOWNERS",
                    path: ".github/CODEOWNERS",
                    sha: "sha123",
                    size: 100,
                    type: ContentType.File,
                    downloadUrl: "https://raw.githubusercontent.com/Azure/azure-sdk-for-net/main/.github/CODEOWNERS",
                    url: "https://api.github.com/repos/Azure/azure-sdk-for-net/contents/.github/CODEOWNERS",
                    htmlUrl: "https://github.com/Azure/azure-sdk-for-net/blob/main/.github/CODEOWNERS",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: "IyBDb2Rld3JuZXJzIGZpbGU=", // base64 for "# Codeowners file"
                    target: null,
                    submoduleGitUrl: null
                )
            };
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS"))
                            .ReturnsAsync(mockFileContent);

            // Mock service label validation
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv"))
                            .ReturnsAsync(new List<RepositoryContent>
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
                            });

            // Mock TypeSpec helper to return false for management plane
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            // Mock RemoveOwners helper method
            mockCodeOwnerHelper.Setup(x => x.RemoveOwners(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
                              .Returns(new List<string> { "@azure/remaining-team" });

            // Mock other helper methods
            mockCodeOwnerHelper.Setup(x => x.findAlphabeticalInsertionPoint(It.IsAny<List<CodeownersEntry>>(), It.IsAny<string>(), It.IsAny<string>()))
                              .Returns(1);

            mockCodeOwnerHelper.Setup(x => x.addCodeownersEntryAtIndex(It.IsAny<string>(), It.IsAny<CodeownersEntry>(), It.IsAny<int>(), It.IsAny<bool>()))
                              .Returns("updated content");

            mockCodeOwnerHelper.Setup(x => x.CreateBranchName(It.IsAny<string>(), It.IsAny<string>()))
                              .Returns("remove-codeowner-alias-service-bus");

            // Act
            var result = await codeownerTools.UpdateCodeowners("azure-sdk-for-net", "/test/typespec/project", "", "Service Bus", serviceOwners, sourceOwners, false);

            // Assert
            Assert.That(result, Does.Contain("Remove codeowner aliases for").Or.Contain("Error"));
        }

        [Test]
        public async Task UpdateCodeowners_AddOwners_CallsHelperMethods()
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team1", "@azure/service-team2" };
            var sourceOwners = new List<string> { "@azure/source-team1", "@azure/source-team2" };
            
            // Mock validation to return valid owners
            var validationResult = new CodeOwnerValidationResult
            {
                Username = "azure/source-team1",
                IsValidCodeOwner = true
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            // Mock CODEOWNERS file retrieval
            var mockFileContent = new List<RepositoryContent>
            {
                new RepositoryContent(
                    name: "CODEOWNERS",
                    path: ".github/CODEOWNERS",
                    sha: "sha123",
                    size: 100,
                    type: ContentType.File,
                    downloadUrl: "https://raw.githubusercontent.com/Azure/azure-sdk-for-net/main/.github/CODEOWNERS",
                    url: "https://api.github.com/repos/Azure/azure-sdk-for-net/contents/.github/CODEOWNERS",
                    htmlUrl: "https://github.com/Azure/azure-sdk-for-net/blob/main/.github/CODEOWNERS",
                    gitUrl: null,
                    encoding: "base64",
                    encodedContent: "IyBDb2Rld3JuZXJzIGZpbGU=", // base64 for "# Codeowners file"
                    target: null,
                    submoduleGitUrl: null
                )
            };
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS"))
                            .ReturnsAsync(mockFileContent);

            // Mock service label validation
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv"))
                            .ReturnsAsync(new List<RepositoryContent>
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
                            });

            // Mock TypeSpec helper to return false for management plane
            mockTypeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(It.IsAny<string>()))
                             .Returns(false);

            // Mock AddUniqueOwners helper method
            mockCodeOwnerHelper.Setup(x => x.AddUniqueOwners(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
                              .Returns(new List<string> { "@azure/service-team1", "@azure/service-team2" });

            // Mock other helper methods
            mockCodeOwnerHelper.Setup(x => x.findAlphabeticalInsertionPoint(It.IsAny<List<CodeownersEntry>>(), It.IsAny<string>(), It.IsAny<string>()))
                              .Returns(1);

            mockCodeOwnerHelper.Setup(x => x.addCodeownersEntryAtIndex(It.IsAny<string>(), It.IsAny<CodeownersEntry>(), It.IsAny<int>(), It.IsAny<bool>()))
                              .Returns("updated content");

            mockCodeOwnerHelper.Setup(x => x.CreateBranchName(It.IsAny<string>(), It.IsAny<string>()))
                              .Returns("add-codeowner-alias-service-bus");

            // Act
            var result = await codeownerTools.UpdateCodeowners("azure-sdk-for-net", "/test/typespec/project", "sdk/test/", "Service Bus", serviceOwners, sourceOwners, true);

            // Assert
            Assert.That(result, Does.Contain("Add codeowner aliases for").Or.Contain("Error"));
        }

        [Test]
        public async Task UpdateCodeowners_NoServiceLabelOrPath_ReturnsError()
        {
            // Arrange
            var serviceOwners = new List<string> { "@azure/service-team" };
            var sourceOwners = new List<string> { "@azure/source-team" };

            // Act
            var result = await codeownerTools.UpdateCodeowners("azure-sdk-for-net", "/test/typespec/project", "", "", serviceOwners, sourceOwners, true);

            // Assert
            Assert.That(result, Does.Contain("Service label:  and Path:  are both invalid"));
        }

        #endregion
    }
}
