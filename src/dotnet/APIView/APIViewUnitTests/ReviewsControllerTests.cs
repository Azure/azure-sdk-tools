using System.Collections.Generic;
using APIViewWeb;
using APIViewWeb.Hubs;
using APIViewWeb.LeanControllers;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
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
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
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
            _mockEnvironment = new Mock<IWebHostEnvironment>();

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
                _mockEnvironment.Object);
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
    }
}
