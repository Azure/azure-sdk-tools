using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.DTOs;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class PullRequestsControllerTests
    {
        private readonly Mock<ILogger<PullRequestsController>> _mockLogger;
        private readonly Mock<IPullRequestManager> _mockPullRequestManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly List<LanguageService> _languageServices;
        private readonly PullRequestsController _controller;

        public PullRequestsControllerTests()
        {
            _mockLogger = new Mock<ILogger<PullRequestsController>>();
            _mockPullRequestManager = new Mock<IPullRequestManager>();
            _mockConfiguration = new Mock<IConfiguration>();
            _languageServices = new List<LanguageService>();

            _controller = new PullRequestsController(
                _mockLogger.Object,
                _mockPullRequestManager.Object,
                _mockConfiguration.Object,
                _languageServices);
        }

        [Theory]
        [InlineData("client")]
        [InlineData("mgmt")]
        [InlineData("CLIENT")]
        [InlineData("MGMT")]
        public async Task CreateAPIRevisionIfAPIHasChanges_WithValidPackageType_PassesCorrectValueToManager(string packageTypeValue)
        {
            // Arrange
            _mockPullRequestManager.Setup(m => m.CreateAPIRevisionIfAPIHasChanges(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CreateAPIRevisionAPIResponse>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null))
                .ReturnsAsync("https://test.com/review/test-id");

            // Setup HTTP context for Request.Host
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString("test.com");
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.CreateAPIRevisionIfAPIHasChanges(
                buildId: "test-build-id",
                artifactName: "test-artifact",
                filePath: "test/path",
                commitSha: "abc123",
                repoName: "test-repo",
                packageName: "test-package",
                pullRequestNumber: 123,
                packageType: packageTypeValue);

            // Assert
            result.Should().NotBeNull();

            // Verify that the manager was called with the exact packageType value passed from controller
            _mockPullRequestManager.Verify(m => m.CreateAPIRevisionIfAPIHasChanges(
                "test-build-id",
                "test-artifact", 
                "test/path",
                "abc123",
                "test-repo",
                "test-package",
                123,
                "test.com",
                It.IsAny<CreateAPIRevisionAPIResponse>(),
                null, // codeFile
                null, // baselineCodeFile
                null, // language - actual value from controller
                "internal", // default project
                packageTypeValue,
                null), // packageType should be passed exactly as received
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("unknown")]
        public async Task CreateAPIRevisionIfAPIHasChanges_WithInvalidPackageType_PassesValueToManager(string packageTypeValue)
        {
            // Arrange
            _mockPullRequestManager.Setup(m => m.CreateAPIRevisionIfAPIHasChanges(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CreateAPIRevisionAPIResponse>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null))
                .ReturnsAsync("https://test.com/review/test-id");

            // Setup HTTP context for Request.Host
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString("test.com");
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.CreateAPIRevisionIfAPIHasChanges(
                buildId: "test-build-id",
                artifactName: "test-artifact",
                filePath: "test/path",
                commitSha: "abc123",
                repoName: "test-repo",
                packageName: "test-package",
                pullRequestNumber: 123,
                packageType: packageTypeValue);

            // Assert
            result.Should().NotBeNull();

            // Verify that the manager was called with the exact packageType value (even if invalid)
            _mockPullRequestManager.Verify(m => m.CreateAPIRevisionIfAPIHasChanges(
                "test-build-id",
                "test-artifact", 
                "test/path",
                "abc123",
                "test-repo",
                "test-package",
                123,
                "test.com",
                It.IsAny<CreateAPIRevisionAPIResponse>(),
                null, // codeFile
                null, // baselineCodeFile
                null, // language - actual value from controller
                "internal", // default project
                packageTypeValue, // packageType should be passed exactly as received (even invalid values)
                null), 
                Times.Once);
        }

        [Fact]
        public async Task CreateAPIRevisionIfAPIHasChanges_WhenPackageTypeOmitted_PassesNullToManager()
        {
            // Arrange
            _mockPullRequestManager.Setup(m => m.CreateAPIRevisionIfAPIHasChanges(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CreateAPIRevisionAPIResponse>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null))
                .ReturnsAsync("https://test.com/review/test-id");

            // Setup HTTP context for Request.Host
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString("test.com");
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act - Not providing packageType parameter to test default behavior
            var result = await _controller.CreateAPIRevisionIfAPIHasChanges(
                buildId: "test-build-id",
                artifactName: "test-artifact",
                filePath: "test/path",
                commitSha: "abc123",
                repoName: "test-repo",
                packageName: "test-package",
                pullRequestNumber: 123);
                // packageType parameter omitted

            // Assert
            result.Should().NotBeNull();

            // Verify that the manager was called with null packageType when omitted
            _mockPullRequestManager.Verify(m => m.CreateAPIRevisionIfAPIHasChanges(
                "test-build-id",
                "test-artifact", 
                "test/path",
                "abc123",
                "test-repo",
                "test-package",
                123,
                "test.com",
                It.IsAny<CreateAPIRevisionAPIResponse>(),
                null, // codeFile
                null, // baselineCodeFile
                null, // language - actual value from controller
                "internal", // default project
                null, // packageType should be null when omitted
                null), 
                Times.Once);
        }

        [Fact]
        public async Task CreateAPIRevisionIfAPIHasChanges_WhenNoAPIRevisionUrlReturned_ReturnsAlreadyReported()
        {
            // Arrange - Manager returns null/empty URL indicating no changes
            _mockPullRequestManager.Setup(m => m.CreateAPIRevisionIfAPIHasChanges(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CreateAPIRevisionAPIResponse>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null))
                .ReturnsAsync((string)null); // No API revision URL returned

            // Setup HTTP context for Request.Host
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString("test.com");
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.CreateAPIRevisionIfAPIHasChanges(
                buildId: "test-build-id",
                artifactName: "test-artifact",
                filePath: "test/path",
                commitSha: "abc123",
                repoName: "test-repo",
                packageName: "test-package",
                pullRequestNumber: 123,
                packageType: "client");

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task GetAssociatedPullRequestsAsync_ReturnsExpectedResult()
        {
            // Arrange
            var reviewId = "test-review-id";
            var apiRevisionId = "test-revision-id";
            var expectedPullRequests = new List<PullRequestModel>
            {
                new PullRequestModel { ReviewId = reviewId, PullRequestNumber = 123 },
                new PullRequestModel { ReviewId = reviewId, PullRequestNumber = 456 }
            };

            _mockPullRequestManager.Setup(m => m.GetPullRequestsModelAsync(reviewId, apiRevisionId))
                .ReturnsAsync(expectedPullRequests);

            // Act
            var result = await _controller.GetAssociatedPullRequestsAsync(reviewId, apiRevisionId);

            // Assert
            result.Should().NotBeNull();

            _mockPullRequestManager.Verify(m => m.GetPullRequestsModelAsync(reviewId, apiRevisionId), Times.Once);
        }
    }
}
