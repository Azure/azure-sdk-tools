using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class ReviewsTokenAuthControllerTests
{
    private readonly ReviewsTokenAuthController _controller;
    private readonly Mock<IReviewSearch> _mockReviewSearch;
    private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
    private readonly Mock<ICosmosPullRequestsRepository> _mockPullRequestsRepository;
    private readonly Mock<IReviewManager> _mockReviewManager;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<ReviewsTokenAuthController>> _mockLogger;
    private readonly List<LanguageService> _languageServices;

    public ReviewsTokenAuthControllerTests()
    {
        _mockReviewSearch = new Mock<IReviewSearch>();
        _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
        _mockPullRequestsRepository = new Mock<ICosmosPullRequestsRepository>();
        _mockReviewManager = new Mock<IReviewManager>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<ReviewsTokenAuthController>>();
        _languageServices = new List<LanguageService>() {new TestLanguageService()};

        _mockConfiguration.Setup(c => c["APIViewUri"]).Returns("https://apiview.any.test");
        _controller = new ReviewsTokenAuthController(
            _mockReviewSearch.Object,
            _mockApiRevisionsManager.Object,
            _mockPullRequestsRepository.Object,
            _mockReviewManager.Object,
            _mockConfiguration.Object,
            _languageServices,
            _mockLogger.Object
        );
    }

    #region GetReviewUrl Tests

    [Fact]
    public async Task GetReviewUrl_WithValidPackageAndLanguage_RedirectsByDefault()
    {
        string package = "azure-template";
        string language = "Python";
        var mockResult = new ResolvePackageResponse
        {
            PackageName = package,
            Language = language,
            ReviewId = "review123",
            RevisionId = "revision456",
            Version = "12.0.0"
        };

        _mockReviewSearch
            .Setup(x => x.ResolvePackageQuery(package, language, null))
            .ReturnsAsync(mockResult);

        IActionResult result = await _controller.GetReviewUrl(package, language);
        RedirectResult redirectResult = Assert.IsType<RedirectResult>(result);
        redirectResult.Url.Should().Contain("review123");
        redirectResult.Url.Should().Contain("revision456");
        _mockReviewSearch.Verify(x => x.ResolvePackageQuery(package, language, null), Times.Once);
    }

    [Fact]
    public async Task GetReviewUrl_WithRedirectFalse_ReturnsJsonWithUrl()
    {
        string package = "azure-template";
        string language = "Python";
        var mockResult = new ResolvePackageResponse
        {
            PackageName = package,
            Language = language,
            ReviewId = "review123",
            RevisionId = "revision456",
            Version = "12.0.0"
        };

        _mockReviewSearch
            .Setup(x => x.ResolvePackageQuery(package, language, It.IsAny<string>()))
            .ReturnsAsync(mockResult);

        IActionResult result = await _controller.GetReviewUrl(package, language, redirect: false);

        LeanJsonResult jsonResult = Assert.IsType<LeanJsonResult>(result);
        var resultValue = jsonResult.Value;
        resultValue.Should().NotBeNull();
        
        var json = System.Text.Json.JsonSerializer.Serialize(resultValue);
        json.Should().Contain("/review/review123?activeApiRevisionId=revision456");
        _mockReviewSearch.Verify(x => x.ResolvePackageQuery(package, language, null), Times.Once);
    }

    [Theory]
    [InlineData(null, "C#")]
    [InlineData("", "C#")]
    [InlineData("Azure.Storage.Blobs", null)]
    [InlineData("Azure.Storage.Blobs", "")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public async Task GetReviewUrl_WithMissingRequiredParameters_ReturnsBadRequest(string package, string language)
    {
        IActionResult result = await _controller.GetReviewUrl(package, language);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        badRequestResult.Value?.ToString().Should().Contain("'package' and 'language' parameters are required");
    }

    [Fact]
    public async Task GetReviewUrl_WhenPackageNotFound_ReturnsNotFound()
    {
        string package = "NonExistent.Package";
        string language = "C#";

        _mockReviewSearch
            .Setup(x => x.ResolvePackageQuery(package, language, null))
            .ReturnsAsync((ResolvePackageResponse)null);

        IActionResult result = await _controller.GetReviewUrl(package, language);

        NotFoundObjectResult notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        notFoundResult.Value?.ToString().Should().Contain("Could not find an APIView review");
        notFoundResult.Value?.ToString().Should().Contain(package);
        notFoundResult.Value?.ToString().Should().Contain(language);
    }

    #endregion
}
