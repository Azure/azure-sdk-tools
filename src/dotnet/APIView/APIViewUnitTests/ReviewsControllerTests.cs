using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb;
using APIViewWeb.Hubs;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class ReviewsControllerTests
    {
        private readonly Mock<ILogger<ReviewsController>> _mockLogger;
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<IReviewManager> _mockReviewManager;
        private readonly Mock<ICommentsManager> _mockCommentsManager;
        private readonly Mock<IBlobCodeFileRepository> _mockCodeFileRepository;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly UserProfileCache _userProfileCache;
        private readonly Mock<IHubContext<SignalRHub>> _mockSignalRHubContext;
        private readonly Mock<INotificationManager> _mockNotificationManager;
        private readonly Mock<IPermissionsManager> _mockPermissionsManager;
        private readonly ReviewsController _controller;

        public ReviewsControllerTests()
        {
            _mockLogger = new Mock<ILogger<ReviewsController>>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockReviewManager = new Mock<IReviewManager>();
            _mockCommentsManager = new Mock<ICommentsManager>();
            _mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockSignalRHubContext = new Mock<IHubContext<SignalRHub>>();
            _mockNotificationManager = new Mock<INotificationManager>();
            _mockPermissionsManager = new Mock<IPermissionsManager>();

            var mockMemoryCache = new Mock<IMemoryCache>();
            var mockUserProfileManager = new Mock<IUserProfileManager>();
            var mockUserProfileLogger = new Mock<ILogger<UserProfileCache>>();
            _userProfileCache = new UserProfileCache(
                mockMemoryCache.Object,
                mockUserProfileManager.Object,
                mockUserProfileLogger.Object);

            var mockLanguageServices = new List<LanguageService>();

            _controller = new ReviewsController(
                _mockLogger.Object,
                _mockApiRevisionsManager.Object,
                _mockReviewManager.Object,
                _mockCommentsManager.Object,
                _mockCodeFileRepository.Object,
                _mockConfiguration.Object,
                _userProfileCache,
                mockLanguageServices,
                _mockSignalRHubContext.Object,
                _mockNotificationManager.Object,
                _mockPermissionsManager.Object);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetIsReviewByCopilotRequired_WhenConfigurationIsNullOrEmpty_ReturnsFalse(string configValue)
        {
            _mockConfiguration.Setup(c => c["CopilotReviewIsRequired"]).Returns(configValue);

            ActionResult<bool> result = _controller.GetIsReviewByCopilotRequired("Java");
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();

            OkObjectResult okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(false);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        public void GetIsReviewByCopilotRequired_WhenConfigurationIsBooleanValue_ReturnsExpectedResult(string configValue, bool expectedResult)
        {
            _mockConfiguration.Setup(c => c["CopilotReviewIsRequired"]).Returns(configValue);

            ActionResult<bool> result = _controller.GetIsReviewByCopilotRequired("Java");
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();

            OkObjectResult okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("  *  ")]
        public void GetIsReviewByCopilotRequired_WhenConfigurationIsWildcard_ReturnsTrue(string configValue)
        {
            _mockConfiguration.Setup(c => c["CopilotReviewIsRequired"]).Returns(configValue);

            ActionResult<bool> result = _controller.GetIsReviewByCopilotRequired("Java");
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();

            OkObjectResult okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(true);
        }

        [Theory]
        [InlineData("Java,Python,C#", "Java", true)]
        [InlineData("Java,Python,C#", "TypeScript", false)]
        [InlineData("Java,Python,C#", "JAVA", true)] // Case insensitive matching
        [InlineData("  Java  ,  Python  ,  C#  ", "  python  ", true)] // Whitespace handling
        [InlineData("Java,,Python,", "Java", true)] // Empty entries in list
        [InlineData("Java,,Python,", "Python", true)] // Empty entries in list
        [InlineData("Java,,Python,", "C#", false)] // Empty entries in list
        public void GetIsReviewByCopilotRequired_WhenLanguageMatchingConfiguration_ReturnsExpectedResult(string configValue, string language, bool expectedResult)
        {
            _mockConfiguration.Setup(c => c["CopilotReviewIsRequired"]).Returns(configValue);

            ActionResult<bool> result = _controller.GetIsReviewByCopilotRequired(language);
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();

            OkObjectResult okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData("Java,Python,C#", null, false)]
        [InlineData("Java,Python,C#", "", false)]
        [InlineData("invalid-value", "Java", false)]
        public void GetIsReviewByCopilotRequired_WhenInvalidInput_ReturnsFalse(string configValue, string language, bool expectedResult)
        {
            _mockConfiguration.Setup(c => c["CopilotReviewIsRequired"]).Returns(configValue);

            ActionResult<bool> result = _controller.GetIsReviewByCopilotRequired(language);
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();

            OkObjectResult okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData("True")]
        [InlineData("TRUE")]
        [InlineData("False")]
        [InlineData("FALSE")]
        public void GetIsReviewByCopilotRequired_WhenConfigurationIsBooleanWithDifferentCasing_ReturnsParsedValue(string configValue)
        {
            _mockConfiguration.Setup(c => c["CopilotReviewIsRequired"]).Returns(configValue);
            bool expectedResult = bool.Parse(configValue);

            ActionResult<bool> result = _controller.GetIsReviewByCopilotRequired("Java");
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();

            OkObjectResult okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData("1.0.0", true, true)]
        [InlineData("1.0.0", false, false)]
        [InlineData("3.0.0", true, false)]
        public async Task GetIsReviewVersionReviewedByCopilot_BasicScenarios_ReturnsExpectedResult(
            string packageVersion, bool hasAutoGeneratedComments, bool expectedResult)
        {
            string reviewId = "test-review-id";
            var apiRevisions = new List<APIRevisionListItemModel>
            {
                new() 
                { 
                    Files = [new APICodeFileModel { PackageVersion = "1.0.0" }],
                    HasAutoGeneratedComments = hasAutoGeneratedComments 
                },
                new() 
                { 
                    Files = [new APICodeFileModel { PackageVersion = "2.0.0" }],
                    HasAutoGeneratedComments = !hasAutoGeneratedComments 
                }
            };

            var result = await ExecuteGetIsReviewVersionReviewedByCopilot(reviewId, packageVersion, apiRevisions);

            AssertOkResult(result, expectedResult);
        }

        [Fact]
        public async Task GetIsReviewVersionReviewedByCopilot_WhenMultipleVersionsMatchButOnlyOneHasComments_ReturnsTrue()
        {
            string reviewId = "test-review-id";
            string packageVersion = "1.0.0";

            var apiRevisions = new List<APIRevisionListItemModel>
            {
                new() 
                { 
                    Files = [new APICodeFileModel { PackageVersion = "1.0.0" }],
                    HasAutoGeneratedComments = false 
                },
                new()
                { 
                    Files = [new APICodeFileModel { PackageVersion = "1.0.0" }], // Same version
                    HasAutoGeneratedComments = true  // But this one has comments
                },
                new()
                { 
                    Files = [new APICodeFileModel { PackageVersion = "2.0.0" }],
                    HasAutoGeneratedComments = false 
                }
            };

            ActionResult<bool> result = await ExecuteGetIsReviewVersionReviewedByCopilot(reviewId, packageVersion, apiRevisions);

            AssertOkResult(result, true);
        }
        private static void AssertOkResult(ActionResult<bool> result, bool expectedValue)
        {
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();

            OkObjectResult okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedValue);
        }

        private async Task<ActionResult<bool>> ExecuteGetIsReviewVersionReviewedByCopilot(
            string reviewId, string packageVersion, List<APIRevisionListItemModel> apiRevisions)
        {
            _mockApiRevisionsManager
                .Setup(m => m.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
                .ReturnsAsync(apiRevisions);

            return await _controller.GetIsReviewVersionReviewedByCopilot(reviewId, packageVersion);
        }

        [Fact]
        public async Task GetReviewRevisionCount_ReturnsCorrectCount()
        {
            // Arrange
            string reviewId = "test-review-id";
            var apiRevisions = new List<APIRevisionListItemModel>
            {
                new() { Id = "rev1", Files = [new APICodeFileModel { PackageVersion = "1.0.0" }] },
                new() { Id = "rev2", Files = [new APICodeFileModel { PackageVersion = "2.0.0" }] },
                new() { Id = "rev3", Files = [new APICodeFileModel { PackageVersion = "3.0.0" }] }
            };

            _mockApiRevisionsManager
                .Setup(m => m.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
                .ReturnsAsync(apiRevisions);

            // Act
            var result = await _controller.GetReviewRevisionCount(reviewId);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(3);
        }

        [Fact]
        public async Task GetReviewRevisionCount_WhenNoRevisions_ReturnsZero()
        {
            // Arrange
            string reviewId = "test-review-id";
            var apiRevisions = new List<APIRevisionListItemModel>();

            _mockApiRevisionsManager
                .Setup(m => m.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
                .ReturnsAsync(apiRevisions);

            // Act
            var result = await _controller.GetReviewRevisionCount(reviewId);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(0);
        }

        [Fact]
        public async Task DeleteReviewAsync_WhenUserIsNotAdmin_ReturnsForbidden()
        {
            // Arrange
            string reviewId = "test-review-id";
            string userName = "testuser";

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new(ClaimConstants.Login, userName),
                new(System.Security.Claims.ClaimTypes.Name, "Test User")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            _mockPermissionsManager
                .Setup(m => m.IsAdminAsync(userName))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteReviewAsync(reviewId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(403);
            objectResult.Value.Should().Be("Only administrators can delete reviews.");
        }

        [Fact]
        public async Task DeleteReviewAsync_WhenUserIsAdmin_CallsSoftDeleteAndReturnsOk()
        {
            // Arrange
            string reviewId = "test-review-id";
            string userName = "adminuser";

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new(ClaimConstants.Login, userName),
                new(System.Security.Claims.ClaimTypes.Name, "Admin User")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            _mockPermissionsManager
                .Setup(m => m.IsAdminAsync(userName))
                .ReturnsAsync(true);

            _mockReviewManager
                .Setup(m => m.SoftDeleteReviewAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), reviewId, true))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteReviewAsync(reviewId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<OkResult>();
            _mockReviewManager.Verify(m => m.SoftDeleteReviewAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), reviewId, true), Times.Once);
        }

        [Fact]
        public async Task DeleteReviewAsync_WhenAuthorizationFails_ReturnsForbidden()
        {
            // Arrange
            string reviewId = "test-review-id";
            string userName = "adminuser";

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new(ClaimConstants.Login, userName),
                new(System.Security.Claims.ClaimTypes.Name, "Admin User")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            _mockPermissionsManager
                .Setup(m => m.IsAdminAsync(userName))
                .ReturnsAsync(true);

            _mockReviewManager
                .Setup(m => m.SoftDeleteReviewAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), reviewId, true))
                .ThrowsAsync(new AuthorizationFailedException());

            // Act
            var result = await _controller.DeleteReviewAsync(reviewId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(403);
            objectResult.Value.Should().Be("You are not authorized to delete this review.");
        }
    }
}
