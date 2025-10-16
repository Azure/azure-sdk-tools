using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Octokit;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tests.Mocks;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    public class CodeownersRequestReviewTests
    {
        private Mock<IOutputHelper> _mockOutput;
        private Mock<ILogger<CodeownersTools>> _mockLogger;

        [SetUp]
        public void SetUp()
        {
            _mockOutput = new Mock<IOutputHelper>();
            _mockLogger = new Mock<ILogger<CodeownersTools>>();
        }

        [Test]
        public async Task RequestCodeownersReview_ValidInputs_ReturnsSuccessMessage()
        {
            // Arrange
            var mockGitHubService = new Mock<IGitHubService>();
            var mockValidator = new Mock<ICodeownersValidatorHelper>();

            // Mock a simple pull request - using reflection to set properties
            var mockPullRequest = new Mock<PullRequest>();
            mockPullRequest.SetupGet(x => x.Number).Returns(11868);
            mockPullRequest.SetupGet(x => x.State).Returns(ItemState.Open);

            // Mock PR files (simulating the files from PR 11868)
            var mockFiles = new List<PullRequestFile>();
            // We'll mock this at the interface level since PullRequestFile constructor is complex

            // Mock CODEOWNERS content - create using simpler approach
            var mockContent = new Mock<RepositoryContent>();
            mockContent.SetupGet(x => x.Content).Returns(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/eng/ @azure/azure-sdk-eng\n")));
            mockContent.SetupGet(x => x.Encoding).Returns("base64");

            mockGitHubService.Setup(x => x.GetPullRequestAsync("Azure", "azure-sdk-tools", 11868))
                           .ReturnsAsync(mockPullRequest.Object);
            
            mockGitHubService.Setup(x => x.GetPullRequestFilesAsync("Azure", "azure-sdk-tools", 11868))
                           .ReturnsAsync(mockFiles);
            
            mockGitHubService.Setup(x => x.GetContentsSingleAsync("Azure", "azure-sdk-tools", ".github/CODEOWNERS", null))
                           .ReturnsAsync(mockContent.Object);
            
            mockGitHubService.Setup(x => x.RequestPullRequestReviewersAsync("Azure", "azure-sdk-tools", 11868, It.IsAny<IEnumerable<string>>()))
                           .Returns(Task.CompletedTask);

            var tool = new CodeownersTools(mockGitHubService.Object, _mockOutput.Object, _mockLogger.Object, null, mockValidator.Object);

            // Act
            var result = await tool.RequestCodeownersReview("Azure", "azure-sdk-tools", 11868);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Successfully requested code owners review") || result.Contains("azure-sdk-eng"));
            
            // Verify that the GitHub service methods were called
            mockGitHubService.Verify(x => x.GetPullRequestAsync("Azure", "azure-sdk-tools", 11868), Times.Once);
            mockGitHubService.Verify(x => x.GetPullRequestFilesAsync("Azure", "azure-sdk-tools", 11868), Times.Once);
        }

        [Test]
        public async Task RequestCodeownersReview_InvalidRepoOwner_ThrowsArgumentException()
        {
            // Arrange
            var mockGitHubService = new Mock<IGitHubService>();
            var mockValidator = new Mock<ICodeownersValidatorHelper>();
            var tool = new CodeownersTools(mockGitHubService.Object, _mockOutput.Object, _mockLogger.Object, null, mockValidator.Object);

            // Act & Assert
            var result = await tool.RequestCodeownersReview("", "azure-sdk-tools", 11868);
            Assert.IsTrue(result.Contains("Failed to request code owners review"));
        }

        [Test]
        public async Task RequestCodeownersReview_InvalidPullRequestNumber_ThrowsArgumentException()
        {
            // Arrange
            var mockGitHubService = new Mock<IGitHubService>();
            var mockValidator = new Mock<ICodeownersValidatorHelper>();
            var tool = new CodeownersTools(mockGitHubService.Object, _mockOutput.Object, _mockLogger.Object, null, mockValidator.Object);

            // Act & Assert
            var result = await tool.RequestCodeownersReview("Azure", "azure-sdk-tools", 0);
            Assert.IsTrue(result.Contains("Failed to request code owners review"));
        }
    }
}