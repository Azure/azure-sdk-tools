using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    internal class CodeOwnerToolsTests
    {
        private Mock<IGitHubService> mockGitHubService;
        private Mock<IOutputService> mockOutputService;
        private Mock<ICodeOwnerValidationHelper> mockValidationHelper;
        private Mock<ICodeOwnerValidator> mockCodeOwnerValidator;
        private ILogger<CodeOwnerTools> logger;
        private CodeOwnerTools codeOwnerTools;

        [SetUp]
        public void Setup()
        {
            mockGitHubService = new Mock<IGitHubService>();
            mockOutputService = new Mock<IOutputService>();
            mockValidationHelper = new Mock<ICodeOwnerValidationHelper>();
            mockCodeOwnerValidator = new Mock<ICodeOwnerValidator>();
            logger = new TestLogger<CodeOwnerTools>();

            mockOutputService.Setup(x => x.Format(It.IsAny<GenericResponse>()))
                           .Returns((GenericResponse r) => string.Join(", ", r.Details));

            codeOwnerTools = new CodeOwnerTools(
                mockGitHubService.Object,
                mockOutputService.Object,
                mockValidationHelper.Object,
                mockCodeOwnerValidator.Object,
                logger
            );
        }

        [Test]
        public async Task IsValidCodeOwner_WhenValidUser_ReturnsValidationResult()
        {
            // Arrange
            var githubAlias = "testuser";
            var userDetails = CreateMockUser("testuser", 123456);
            var validationResult = new CodeOwnerValidationResult
            {
                Username = "testuser",
                Status = "Success",
                IsValidCodeOwner = true
            };

            mockGitHubService.Setup(x => x.GetGitUserDetailsAsync())
                           .ReturnsAsync(userDetails);
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync("testuser", false))
                                 .ReturnsAsync(validationResult);

            // Act - Test both with explicit alias and empty alias (authenticated user)
            var resultWithAlias = await codeOwnerTools.isValidCodeOwner(githubAlias);
            var resultWithEmptyAlias = await codeOwnerTools.isValidCodeOwner("");

            // Assert
            Assert.That(resultWithAlias, Is.Not.Null);
            Assert.That(resultWithEmptyAlias, Is.Not.Null);
            // Both should call GetGitUserDetailsAsync
            mockGitHubService.Verify(x => x.GetGitUserDetailsAsync(), Times.Exactly(2));
        }

        [Test]
        public async Task ValidateCodeOwnersForService_WhenValidRepo_CallsHelper()
        {
            // Arrange
            var repoName = "dotnet"; // This maps to "azure-sdk-for-net"
            var serviceLabel = "Storage";

            // Setup mock helper to return null (service not found) to avoid complex setup
            mockValidationHelper.Setup(x => x.FindServiceEntries(It.IsAny<IList<Azure.Sdk.Tools.CodeownersUtils.Parsing.CodeownersEntry>>(), serviceLabel))
                              .Returns((Azure.Sdk.Tools.CodeownersUtils.Parsing.CodeownersEntry?)null);

            // Act
            var result = await codeOwnerTools.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
            
            // Verify helper methods were called
            mockValidationHelper.Verify(x => x.FindServiceEntries(It.IsAny<IList<Azure.Sdk.Tools.CodeownersUtils.Parsing.CodeownersEntry>>(), serviceLabel), Times.Once);
        }

        [Test]
        public async Task ValidateCodeOwnersForService_WhenServiceFound_ReturnsSuccessWithCodeOwners()
        {
            // Arrange
            var repoName = "dotnet";
            var serviceLabel = "Storage";
            var mockEntry = new Azure.Sdk.Tools.CodeownersUtils.Parsing.CodeownersEntry();
            var uniqueOwners = new List<string> { "user1", "user2" };

            mockValidationHelper.Setup(x => x.FindServiceEntries(It.IsAny<IList<Azure.Sdk.Tools.CodeownersUtils.Parsing.CodeownersEntry>>(), serviceLabel))
                              .Returns(mockEntry);
            mockValidationHelper.Setup(x => x.ExtractUniqueOwners(mockEntry))
                              .Returns(uniqueOwners);

            // Act
            var result = await codeOwnerTools.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo("Success"));
            Assert.That(result.Message, Contains.Substring("Found 1 matching entry"));
            mockValidationHelper.Verify(x => x.ExtractUniqueOwners(mockEntry), Times.Once);
        }

        [Test]
        public async Task ValidateCodeOwnersForService_WhenExceptionThrown_ReturnsError()
        {
            // Arrange
            var repoName = "dotnet";
            var serviceLabel = "Storage";

            mockValidationHelper.Setup(x => x.FindServiceEntries(It.IsAny<IList<Azure.Sdk.Tools.CodeownersUtils.Parsing.CodeownersEntry>>(), serviceLabel))
                              .Throws(new Exception("Test exception"));

            // Act
            var result = await codeOwnerTools.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo("Error"));
            Assert.That(result.Message, Contains.Substring("Test exception"));
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
        }

        [Test]
        public async Task IsValidCodeOwner_WhenGitHubServiceReturnsNull_ReturnsErrorResponse()
        {
            // Arrange
            mockGitHubService.Setup(x => x.GetGitUserDetailsAsync())
                           .ReturnsAsync((User)null!);

            // Act
            var result = await codeOwnerTools.isValidCodeOwner("");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Contains.Substring("Unable to determine GitHub username"));
            mockOutputService.Verify(x => x.Format(It.Is<GenericResponse>(r => r.Status == "Failed")), Times.Once);
        }

        [Test]
        public async Task IsValidCodeOwner_WhenValidationSucceeds_ReturnsJsonResult()
        {
            // Arrange
            var githubAlias = "testuser";
            var userDetails = CreateMockUser("testuser", 123456);
            var mockValidationResult = new CodeOwnerValidationResult
            {
                Username = "testuser",
                Status = "Success",
                IsValidCodeOwner = true,
                HasWritePermission = true,
                Organizations = new Dictionary<string, bool> { { "azure", true } }
            };

            mockGitHubService.Setup(x => x.GetGitUserDetailsAsync())
                           .ReturnsAsync(userDetails);
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync("testuser", false))
                                 .ReturnsAsync(mockValidationResult);

            // Act
            var result = await codeOwnerTools.isValidCodeOwner(githubAlias);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Contains.Substring("\"IsValidCodeOwner\": true"));
            Assert.That(result, Contains.Substring("\"HasWritePermission\": true"));
        }

        [Test]
        public async Task IsValidCodeOwner_WhenExceptionThrown_ReturnsErrorResponse()
        {
            // Arrange
            var githubAlias = "testuser";
            
            mockGitHubService.Setup(x => x.GetGitUserDetailsAsync())
                           .ThrowsAsync(new Exception("GitHub service error"));

            // Act
            var result = await codeOwnerTools.isValidCodeOwner(githubAlias);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Contains.Substring("GitHub service error"));
            mockOutputService.Verify(x => x.Format(It.Is<GenericResponse>(r => r.Status == "Failed")), Times.Once);
        }

        [Test]
        public async Task ValidateCodeOwnersConcurrently_WithMultipleUsers_ValidatesCorrectly()
        {
            // Arrange
            var owners = new List<string> { "@user1", "@user2", "user3" };

            // Mock the CodeOwnerValidator to return different results for each user
            var user1Result = new CodeOwnerValidationResult
            {
                Username = "user1",
                Status = "Success",
                IsValidCodeOwner = true,
                HasWritePermission = true,
                Organizations = new Dictionary<string, bool> { { "azure", true } }
            };

            var user2Result = new CodeOwnerValidationResult
            {
                Username = "user2",
                Status = "Success",
                IsValidCodeOwner = false,
                HasWritePermission = false,
                Organizations = new Dictionary<string, bool> { { "azure", false } }
            };

            var user3Result = new CodeOwnerValidationResult
            {
                Username = "user3",
                Status = "Success",
                IsValidCodeOwner = true,
                HasWritePermission = true,
                Organizations = new Dictionary<string, bool> { { "azure", true } }
            };

            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync("user1", false))
                                 .ReturnsAsync(user1Result);
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync("user2", false))
                                 .ReturnsAsync(user2Result);
            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync("user3", false))
                                 .ReturnsAsync(user3Result);

            // Act
            var results = await codeOwnerTools.ValidateCodeOwnersConcurrently(owners);

            // Assert
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(3));
            
            // Verify each user's result
            var user1Actual = results.First(r => r.Username == "user1");
            Assert.That(user1Actual.IsValidCodeOwner, Is.True);
            Assert.That(user1Actual.HasWritePermission, Is.True);
            
            var user2Actual = results.First(r => r.Username == "user2");
            Assert.That(user2Actual.IsValidCodeOwner, Is.False);
            Assert.That(user2Actual.HasWritePermission, Is.False);
            
            var user3Actual = results.First(r => r.Username == "user3");
            Assert.That(user3Actual.IsValidCodeOwner, Is.True);
            Assert.That(user3Actual.HasWritePermission, Is.True);

            // Verify CodeOwnerValidator was called for each user
            mockCodeOwnerValidator.Verify(x => x.ValidateCodeOwnerAsync("user1", false), Times.Once);
            mockCodeOwnerValidator.Verify(x => x.ValidateCodeOwnerAsync("user2", false), Times.Once);
            mockCodeOwnerValidator.Verify(x => x.ValidateCodeOwnerAsync("user3", false), Times.Once);
        }

        [Test]
        public async Task ValidateCodeOwnersConcurrently_WhenCalledTwiceWithSameUsers_ShowsCachingBehavior()
        {
            // Arrange
            var owners = new List<string> { "@testUser" };
            var expectedResult = new CodeOwnerValidationResult
            {
                Username = "testUser",
                Status = "Success",
                IsValidCodeOwner = true,
                HasWritePermission = true,
                Organizations = new Dictionary<string, bool> { { "azure", true } }
            };

            mockCodeOwnerValidator.Setup(x => x.ValidateCodeOwnerAsync("testUser", false))
                                 .ReturnsAsync(expectedResult);

            // Act - Call twice with the same user
            var firstResults = await codeOwnerTools.ValidateCodeOwnersConcurrently(owners);
            var secondResults = await codeOwnerTools.ValidateCodeOwnersConcurrently(owners);

            // Assert
            Assert.That(firstResults.Count, Is.EqualTo(1));
            Assert.That(secondResults.Count, Is.EqualTo(1));
            
            var firstResult = firstResults.First();
            var secondResult = secondResults.First();
            
            // Both results should be identical
            Assert.That(firstResult.Username, Is.EqualTo(secondResult.Username));
            Assert.That(firstResult.IsValidCodeOwner, Is.EqualTo(secondResult.IsValidCodeOwner));
            Assert.That(firstResult.HasWritePermission, Is.EqualTo(secondResult.HasWritePermission));

            // Verify CodeOwnerValidator was called only once (cached on second call)
            mockCodeOwnerValidator.Verify(x => x.ValidateCodeOwnerAsync("testUser", false), Times.Once);
        }

        private User CreateMockUser(string login, int id)
        {
            // Create a real User object using constructor instead of mocking
            return new User(
                avatarUrl: $"https://github.com/{login}.png",
                bio: null,
                blog: null,
                collaborators: 0,
                company: null,
                createdAt: DateTimeOffset.Now,
                diskUsage: 0,
                email: $"{login}@example.com",
                followers: 0,
                following: 0,
                hireable: null,
                htmlUrl: $"https://github.com/{login}",
                totalPrivateRepos: 0,
                id: id,
                location: null,
                login: login,
                name: login,
                nodeId: $"node{id}",
                ownedPrivateRepos: 0,
                plan: null,
                privateGists: 0,
                publicGists: 0,
                publicRepos: 0,
                updatedAt: DateTimeOffset.Now,
                url: $"https://api.github.com/users/{login}",
                permissions: null,
                siteAdmin: false,
                suspendedAt: null,
                ldapDistinguishedName: null
            );
        }
    }
}
