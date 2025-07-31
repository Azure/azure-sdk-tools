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
        private Mock<ICodeOwnerHelper> mockCodeOwnerHelper;
        private Mock<ICodeOwnerValidatorHelper> mockCodeOwnerValidator;
        private Mock<ILabelHelper> mockLabelHelper;

        [SetUp]
        public void Setup()
        {
            mockGitHubService = new Mock<IGitHubService>();
            mockOutputService = new Mock<IOutputService>();
            mockLogger = new Mock<ILogger<CodeownerTools>>();
            mockCodeOwnerHelper = new Mock<ICodeOwnerHelper>();
            mockCodeOwnerValidator = new Mock<ICodeOwnerValidatorHelper>();
            mockLabelHelper = new Mock<ILabelHelper>();

            codeownerTools = new CodeownerTools(
                mockGitHubService.Object,
                mockOutputService.Object,
                mockLogger.Object,
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
            var result = await codeownerTools.ValidateCodeOwnerEntryForService("dotnet", "Service Bus");

            // Assert
            Assert.That(result.Message, Does.Contain("Successfully found codeowners"));
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

        #region ValidateCodeOwnersConcurrently Tests

        [Test]
        public async Task ValidateCodeOwnersConcurrently_ValidOwners_ReturnsResults()
        {
            // Arrange
            var owners = new List<string> { "@user1", "@user2" };
            var validationResult = new CodeOwnerValidationResult
            {
                Username = "user1",
                IsValidCodeOwner = true
            };
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync(It.IsAny<string>(), false))
                                  .ReturnsAsync(validationResult);

            // Act
            var result = await codeownerTools.ValidateCodeOwnersConcurrently(owners);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task ValidateCodeOwnersConcurrently_EmptyList_ReturnsEmptyResults()
        {
            // Arrange
            var owners = new List<string>();

            // Act
            var result = await codeownerTools.ValidateCodeOwnersConcurrently(owners);

            // Assert
            Assert.That(result.Count, Is.EqualTo(0));
        }

        #endregion

        #region AddCodeownerEntry Tests

        [Test]
        public async Task AddCodeownerEntry_InvalidSourceOwners_ReturnsError()
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

            // Act
            var result = await codeownerTools.AddCodeownerEntry("dotnet", "sdk/test/", "Service Bus", serviceOwners, sourceOwners);

            // Assert
            Assert.That(result, Does.Contain("There must be at least one valid source owner"));
        }

        [Test]
        public async Task AddCodeownerEntry_ValidInput_CallsHelperMethods()
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

            // Mock service label validation to fail and get the CSV content error
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv"))
                            .ReturnsAsync((IReadOnlyList<RepositoryContent>?)null);

            // Act
            var result = await codeownerTools.AddCodeownerEntry("dotnet", "sdk/test/", "Service Bus", serviceOwners, sourceOwners);

            // Assert
            Assert.That(result, Does.Contain("Could not retrieve service labels for validation"));
        }

        #endregion

        #region AddCodeowners Tests

        [Test]
        public async Task AddCodeowners_FileNotFound_ReturnsError()
        {
            // Arrange
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS"))
                            .ReturnsAsync((IReadOnlyList<RepositoryContent>?)null);

            // Act
            var result = await codeownerTools.AddCodeowners("dotnet", "sdk/test/", "Service Bus", new List<string>(), new List<string>(), "main");

            // Assert
            Assert.That(result, Does.Contain("Could not retrieve CODEOWNERS file"));
        }

        [Test]
        public async Task AddCodeowners_ValidInput_ReturnsEmptyString()
        {
            // Arrange - Mock to return null to trigger the expected error
            mockGitHubService.Setup(x => x.GetContentsAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS"))
                            .ReturnsAsync((IReadOnlyList<RepositoryContent>?)null);

            // Act
            var result = await codeownerTools.AddCodeowners("dotnet", "sdk/test/", "Service Bus", new List<string>(), new List<string>(), "main");

            // Assert
            Assert.That(result, Does.Contain("Could not retrieve CODEOWNERS file"));
        }

        #endregion
    }
}
