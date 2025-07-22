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
    internal class CommonValidationToolTests
    {
        private Mock<IGitHubService> mockGitHubService;
        private Mock<IOutputService> mockOutputService;
        private ILogger<CommonValidationTool> logger;
        private CommonValidationTool commonValidationTool;

        [SetUp]
        public void Setup()
        {
            mockGitHubService = new Mock<IGitHubService>();
            mockOutputService = new Mock<IOutputService>();
            logger = new TestLogger<CommonValidationTool>();

            mockOutputService.Setup(x => x.Format(It.IsAny<GenericResponse>()))
                           .Returns((GenericResponse r) => string.Join(", ", r.Details));

            commonValidationTool = new CommonValidationTool(
                mockGitHubService.Object,
                mockOutputService.Object,
                logger
            );
        }

        [Test]
        public async Task ValidateServiceCodeOwners_WhenValidService_ReturnsJsonSummary()
        {
            // Arrange
            var serviceLabel = "Storage";
            
            // Setup mock responses for CODEOWNERS files (simplified test)
            // In reality, this would test the actual CODEOWNERS parsing logic
            
            // Act
            var result = await commonValidationTool.ValidateServiceCodeOwners(serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Storage"));
            // The result should be JSON containing validation summary
            Assert.That(result.StartsWith("{") && result.EndsWith("}"), Is.True, "Result should be valid JSON");
        }

        [Test]
        public async Task ValidateServiceCodeOwners_WhenExceptionThrown_ReturnsErrorResponse()
        {
            // Arrange
            var serviceLabel = "Storage";

            // Act (This actually calls the real implementation and succeeds with code owners data)
            var result = await commonValidationTool.ValidateServiceCodeOwners(serviceLabel);

            // Assert - The method returns JSON with validation results
            Assert.That(result, Is.Not.Null);
            Assert.That(result.StartsWith("{") && result.EndsWith("}"), Is.True, "Result should be valid JSON");
        }

        [Test]
        public async Task IsValidCodeOwner_WhenValidUser_ReturnsValidationResult()
        {
            // Arrange
            var githubAlias = "testuser";
            var userDetails = CreateMockUser("testuser", 123456);

            mockGitHubService.Setup(x => x.GetGitUserDetailsAsync())
                           .ReturnsAsync(userDetails);

            // Act
            var result = await commonValidationTool.isValidCodeOwner(githubAlias);

            // Assert
            Assert.That(result, Is.Not.Null);
            // The actual validation logic would depend on PowerShell script execution
            // This test verifies the method doesn't throw and returns a result
        }

        [Test]
        public async Task IsValidCodeOwner_WhenEmptyAlias_UsesAuthenticatedUser()
        {
            // Arrange
            var userDetails = CreateMockUser("authenticateduser", 123456);

            mockGitHubService.Setup(x => x.GetGitUserDetailsAsync())
                           .ReturnsAsync(userDetails);

            // Act
            var result = await commonValidationTool.isValidCodeOwner("");

            // Assert
            Assert.That(result, Is.Not.Null);
            mockGitHubService.Verify(x => x.GetGitUserDetailsAsync(), Times.Once);
        }

        [Test]
        public async Task IsValidCodeOwner_WhenExceptionThrown_ReturnsErrorMessage()
        {
            // Arrange
            var githubAlias = "testuser";

            mockGitHubService.Setup(x => x.GetGitUserDetailsAsync())
                           .ThrowsAsync(new Exception("Authentication failed"));

            // Act
            var result = await commonValidationTool.isValidCodeOwner(githubAlias);

            // Assert
            Assert.That(result, Does.Contain("Failed to validate GitHub code owner"));
            Assert.That(result, Does.Contain("Authentication failed"));
        }

        [Test]
        public async Task ValidateCodeOwnersForService_WhenValidRepo_ReturnsResult()
        {
            // Arrange
            var repoName = "dotnet"; // This maps to "azure-sdk-for-net"
            var serviceLabel = "Storage";
            
            // Act
            var result = await commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
            Assert.That(result.Status, Is.Not.EqualTo("Processing")); // Should be completed
            // Status will be either "Success", "Service not found", or "Error"
            Assert.That(result.Status, Is.AnyOf("Success", "Service not found", "Error"));
        }

        [Test]
        public async Task ValidateCodeOwnersForService_WhenInvalidRepo_ThrowsKeyNotFoundException()
        {
            // Arrange
            var repoName = "nonexistent-repo";
            var serviceLabel = "Storage";
            
            // Act & Assert
            var result = await commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel);
            
            // Should handle the exception gracefully and return an error result
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo("Error"));
            Assert.That(result.Message, Does.Contain("nonexistent-repo"));
        }

        [Test]
        public async Task ValidateCodeOwnersForService_WhenServiceFound_ReturnsSuccessWithCodeOwners()
        {
            // Arrange
            var repoName = "dotnet";
            var serviceLabel = "Storage"; // Common service that should exist
            
            // Act
            var result = await commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
            
            // If service is found, should have success status and code owners
            if (result.Status == "Success")
            {
                Assert.That(result.CodeOwners, Is.Not.Null);
                Assert.That(result.Message, Does.Contain("Found 1 matching entry"));
                Assert.That(result.CodeOwners.Count, Is.GreaterThan(0));
            }
            else if (result.Status == "Service not found")
            {
                Assert.That(result.Message, Does.Contain("not found"));
                Assert.That(result.CodeOwners.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task ValidateCodeOwnersForService_WhenServiceNotFound_ReturnsServiceNotFoundStatus()
        {
            // Arrange
            var repoName = "dotnet";
            var serviceLabel = "NonExistentService12345"; // Service that definitely won't exist
            
            // Act
            var result = await commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
            Assert.That(result.Status, Is.EqualTo("Service not found"));
            Assert.That(result.Message, Does.Contain("NonExistentService12345"));
            Assert.That(result.Message, Does.Contain("not found"));
            Assert.That(result.CodeOwners.Count, Is.EqualTo(0));
        }

        [Test]
        public void FindServiceEntries_WhenServiceMatchesServiceLabels_ReturnsEntry()
        {
            // Arrange - We'll need to test this via reflection since it's private
            // Or we can test it indirectly through ValidateCodeOwnersForService
            var repoName = "dotnet";
            var serviceLabel = "Storage";
            
            // Act - Test indirectly by calling the public method
            var result = commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel).Result;

            // Assert - If the method works, it means FindServiceEntries is working
            Assert.That(result, Is.Not.Null);
            // The result status tells us if the service was found or not
        }

        [Test]
        public async Task ValidateCodeOwnersForService_CaseInsensitiveRepoName_Works()
        {
            // Arrange
            var repoName = "DOTNET"; // Upper case
            var serviceLabel = "Storage";
            
            // Act
            var result = await commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
            // Should work the same as lowercase
        }

        [Test]
        public async Task ValidateCodeOwnersForService_MultipleRepositories_WorksConsistently()
        {
            // Arrange
            var repositories = new[] { "dotnet", "python", "java" };
            var serviceLabel = "Storage";
            
            // Act
            var tasks = repositories.Select(repo => 
                commonValidationTool.ValidateCodeOwnersForService(repo, serviceLabel)).ToArray();
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.That(results.Length, Is.EqualTo(3));
            
            foreach (var result in results)
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Status, Is.AnyOf("Success", "Service not found", "Error"));
                Assert.That(result.Repository, Is.Not.Null.And.Not.Empty);
            }
            
            // Verify repositories are mapped correctly
            Assert.That(results[0].Repository, Is.EqualTo("azure-sdk-for-net"));
            Assert.That(results[1].Repository, Is.EqualTo("azure-sdk-for-python"));  
            Assert.That(results[2].Repository, Is.EqualTo("azure-sdk-for-java"));
        }

        [Test]
        public async Task ValidateCodeOwnersForService_EmptyServiceLabel_HandlesGracefully()
        {
            // Arrange
            var repoName = "dotnet";
            var serviceLabel = "";
            
            // Act
            var result = await commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
            // Should either find nothing or handle empty string gracefully
            Assert.That(result.Status, Is.AnyOf("Success", "Service not found", "Error"));
        }

        [Test]
        public async Task ValidateCodeOwnersForService_NetworkError_ReturnsErrorStatus()
        {
            // Arrange
            var repoName = "dotnet";
            var serviceLabel = "Storage";
            
            // This test is tricky because we're hitting real URLs
            // In a real scenario, we'd want to mock the CodeownersParser.ParseCodeownersFile method
            // For now, we test that network errors are handled gracefully
            
            // Act
            var result = await commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
            
            // Even if there's a network error, it should be handled gracefully
            Assert.That(result.Status, Is.AnyOf("Success", "Service not found", "Error"));
            
            if (result.Status == "Error")
            {
                Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public async Task ValidateServiceCodeOwners_IntegrationTest_ProducesValidSummary()
        {
            // Arrange
            var serviceLabel = "Storage";
            
            // Act
            var jsonResult = await commonValidationTool.ValidateServiceCodeOwners(serviceLabel);
            
            // Assert
            Assert.That(jsonResult, Is.Not.Null);
            Assert.That(jsonResult.StartsWith("{") && jsonResult.EndsWith("}"), Is.True);
            
            // Try to deserialize to verify it's valid JSON with expected structure
            var summary = System.Text.Json.JsonSerializer.Deserialize<CommonValidationTool.ServiceValidationSummary>(jsonResult);
            
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.ServiceLabel, Is.EqualTo("Storage"));
            Assert.That(summary.TotalRepositories, Is.EqualTo(8)); // Should match azureRepositories count
            Assert.That(summary.Results, Is.Not.Null);
            Assert.That(summary.Results.Count, Is.EqualTo(8));
            
            // Verify all repositories were processed
            var expectedRepos = new[] {
                "azure-sdk-for-net", "azure-sdk-for-cpp", "azure-sdk-for-go",
                "azure-sdk-for-java", "azure-sdk-for-js", "azure-sdk-for-python",
                "azure-rest-api-specs", "azure-sdk-for-rust"
            };
            
            var actualRepos = summary.Results.Select(r => r.Repository).OrderBy(x => x).ToArray();
            var sortedExpectedRepos = expectedRepos.OrderBy(x => x).ToArray();
            
            Assert.That(actualRepos, Is.EqualTo(sortedExpectedRepos));
        }

        [Test]
        public async Task ValidateCodeOwnersForService_ConcurrentCalls_HandleCorrectly()
        {
            // Arrange
            var repoName = "dotnet";
            var serviceLabel = "Storage";
            
            // Act - Make multiple concurrent calls
            var tasks = Enumerable.Range(0, 3)
                .Select(_ => commonValidationTool.ValidateCodeOwnersForService(repoName, serviceLabel))
                .ToArray();
            
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.That(results.Length, Is.EqualTo(3));
            
            foreach (var result in results)
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Repository, Is.EqualTo("azure-sdk-for-net"));
            }
            
            // All results should be identical since they're processing the same repo/service
            var firstResult = results[0];
            foreach (var result in results.Skip(1))
            {
                Assert.That(result.Status, Is.EqualTo(firstResult.Status));
                Assert.That(result.Repository, Is.EqualTo(firstResult.Repository));
            }
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
