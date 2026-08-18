using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
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

    #region MarkReleased Tests

    [Fact]
    public async Task MarkReleased_WithExactVersion_MarksResolvedRevisionReleased()
    {
        DateTime releasedOn = DateTime.UtcNow;
        var resolved = new ResolvePackageResponse
        {
            PackageName = "Azure.Storage.Blobs",
            Language = "C#",
            ReviewId = "review123",
            RevisionId = "revision456",
            Version = "12.0.0"
        };
        _mockReviewSearch
            .Setup(x => x.ResolveAutomaticRevisionForRelease("Azure.Storage.Blobs", "C#", "12.0.0"))
            .ReturnsAsync(resolved);
        _mockApiRevisionsManager
            .Setup(x => x.MarkAPIRevisionReleasedAsync("revision456"))
            .ReturnsAsync(new APIRevisionListItemModel
            {
                Id = "revision456",
                PackageName = "Azure.Storage.Blobs",
                Language = "C#",
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "12.0.0" } },
                IsReleased = true,
                ReleasedOn = releasedOn
            });

        ActionResult<MarkReleasedResult> result = await _controller.MarkReleased("Azure.Storage.Blobs", "C#", "12.0.0");

        LeanJsonResult jsonResult = Assert.IsType<LeanJsonResult>(result.Result);
        MarkReleasedResult response = Assert.IsType<MarkReleasedResult>(jsonResult.Value);
        Assert.Equal("review123", response.ReviewId);
        Assert.Equal("revision456", response.RevisionId);
        Assert.Equal("Azure.Storage.Blobs", response.PackageName);
        Assert.Equal("C#", response.Language);
        Assert.Equal("12.0.0", response.Version);
        Assert.True(response.IsReleased);
        Assert.Equal(releasedOn, response.ReleasedOn);
        _mockApiRevisionsManager.Verify(x => x.MarkAPIRevisionReleasedAsync("revision456"), Times.Once);
    }

    [Fact]
    public async Task MarkReleased_WithDryRun_ReturnsResolvedRevisionWithoutMarkingReleased()
    {
        var resolved = new ResolvePackageResponse
        {
            PackageName = "Azure.Storage.Blobs",
            Language = "C#",
            ReviewId = "review123",
            RevisionId = "revision456",
            Version = "12.0.0"
        };
        _mockReviewSearch
            .Setup(x => x.ResolveAutomaticRevisionForRelease("Azure.Storage.Blobs", "C#", "12.0.0"))
            .ReturnsAsync(resolved);
        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync("revision456"))
            .ReturnsAsync(new APIRevisionListItemModel
            {
                Id = "revision456",
                PackageName = "Azure.Storage.Blobs",
                Language = "C#",
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "12.0.0" } },
                IsReleased = false
            });

        ActionResult<MarkReleasedResult> result = await _controller.MarkReleased(
            "Azure.Storage.Blobs", "C#", "12.0.0", dryRun: true);

        LeanJsonResult jsonResult = Assert.IsType<LeanJsonResult>(result.Result);
        MarkReleasedResult response = Assert.IsType<MarkReleasedResult>(jsonResult.Value);
        Assert.Equal("review123", response.ReviewId);
        Assert.Equal("revision456", response.RevisionId);
        Assert.Equal("Azure.Storage.Blobs", response.PackageName);
        Assert.Equal("C#", response.Language);
        Assert.Equal("12.0.0", response.Version);
        Assert.False(response.IsReleased);
        Assert.Null(response.ReleasedOn);
        _mockApiRevisionsManager.Verify(x => x.GetAPIRevisionAsync("revision456"), Times.Once);
        _mockApiRevisionsManager.Verify(x => x.MarkAPIRevisionReleasedAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MarkReleased_WhenRevisionMetadataMissing_ReturnsEmptyStrings()
    {
        var resolved = new ResolvePackageResponse
        {
            PackageName = "Azure.Storage.Blobs",
            Language = "C#",
            ReviewId = "review123",
            RevisionId = "revision456",
            Version = "12.0.0"
        };
        _mockReviewSearch
            .Setup(x => x.ResolveAutomaticRevisionForRelease("Azure.Storage.Blobs", "C#", "12.0.0"))
            .ReturnsAsync(resolved);
        _mockApiRevisionsManager
            .Setup(x => x.MarkAPIRevisionReleasedAsync("revision456"))
            .ReturnsAsync(new APIRevisionListItemModel
            {
                Id = "revision456",
                IsReleased = true,
                ReleasedOn = DateTime.UtcNow
            });

        ActionResult<MarkReleasedResult> result = await _controller.MarkReleased("Azure.Storage.Blobs", "C#", "12.0.0");

        LeanJsonResult jsonResult = Assert.IsType<LeanJsonResult>(result.Result);
        MarkReleasedResult response = Assert.IsType<MarkReleasedResult>(jsonResult.Value);
        Assert.Equal(string.Empty, response.PackageName);
        Assert.Equal(string.Empty, response.Language);
        Assert.Equal(string.Empty, response.Version);
    }

    [Theory]
    [InlineData(null, "C#", "12.0.0")]
    [InlineData("Azure.Storage.Blobs", null, "12.0.0")]
    [InlineData("Azure.Storage.Blobs", "C#", null)]
    public async Task MarkReleased_WithMissingParameter_ReturnsBadRequest(string package, string language, string version)
    {
        ActionResult<MarkReleasedResult> result = await _controller.MarkReleased(package, language, version);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        System.Text.Json.JsonSerializer.Serialize(badRequest.Value).Should().Contain("\"message\"");
        _mockApiRevisionsManager.Verify(x => x.MarkAPIRevisionReleasedAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MarkReleased_WithMajorMinorFallback_ReturnsNotFound()
    {
        _mockReviewSearch
            .Setup(x => x.ResolveAutomaticRevisionForRelease("Azure.Storage.Blobs", "C#", "12.0.0"))
            .ReturnsAsync((ResolvePackageResponse)null);

        ActionResult<MarkReleasedResult> result = await _controller.MarkReleased("Azure.Storage.Blobs", "C#", "12.0.0");

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        System.Text.Json.JsonSerializer.Serialize(notFound.Value).Should().Contain("\"message\"");
        _mockApiRevisionsManager.Verify(x => x.MarkAPIRevisionReleasedAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MarkReleased_WhenResolvedRevisionDisappears_ReturnsStructuredNotFound()
    {
        _mockReviewSearch
            .Setup(x => x.ResolveAutomaticRevisionForRelease("Azure.Storage.Blobs", "C#", "12.0.0"))
            .ReturnsAsync(new ResolvePackageResponse { RevisionId = "revision456" });
        _mockApiRevisionsManager
            .Setup(x => x.MarkAPIRevisionReleasedAsync("revision456"))
            .ReturnsAsync((APIRevisionListItemModel)null);

        ActionResult<MarkReleasedResult> result = await _controller.MarkReleased("Azure.Storage.Blobs", "C#", "12.0.0");

        NotFoundObjectResult notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        System.Text.Json.JsonSerializer.Serialize(notFound.Value).Should().Contain("\"message\"");
    }

    [Fact]
    public async Task MarkReleased_WhenResolutionThrows_ReturnsStructuredInternalServerError()
    {
        _mockReviewSearch
            .Setup(x => x.ResolveAutomaticRevisionForRelease("Azure.Storage.Blobs", "C#", "12.0.0"))
            .ThrowsAsync(new InvalidOperationException("failure"));

        ActionResult<MarkReleasedResult> result = await _controller.MarkReleased("Azure.Storage.Blobs", "C#", "12.0.0");

        ObjectResult internalServerError = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, internalServerError.StatusCode);
        System.Text.Json.JsonSerializer.Serialize(internalServerError.Value).Should().Contain("\"message\"");
    }

    #endregion

    #region StartReviewJob Tests

    [Fact]
    public async Task StartReviewJob_WithNullRequest_ReturnsBadRequest()
    {
        ActionResult<AIReviewJobStartedResponseModel> result = await _controller.StartReviewJob(null);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task StartReviewJob_WithEmptyTarget_ReturnsBadRequest()
    {
        var request = new StartReviewJobRequest { Target = "" };
        ActionResult<AIReviewJobStartedResponseModel> result = await _controller.StartReviewJob(request);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task StartReviewJob_WithValidRequest_Returns202WithJobId()
    {
        var expectedResponse = new AIReviewJobStartedResponseModel { JobId = "job-abc" };
        _mockReviewManager
            .Setup(m => m.StartCopilotReviewJobAsync(It.IsAny<StartReviewJobRequest>()))
            .ReturnsAsync(expectedResponse);

        var request = new StartReviewJobRequest { Target = "def hello(): pass", Language = "python" };
        ActionResult<AIReviewJobStartedResponseModel> result = await _controller.StartReviewJob(request);

        Assert.IsType<LeanJsonResult>(result.Result);
    }

    [Fact]
    public async Task StartReviewJob_WhenManagerThrows_Returns500WithMessage()
    {
        _mockReviewManager
            .Setup(m => m.StartCopilotReviewJobAsync(It.IsAny<StartReviewJobRequest>()))
            .ThrowsAsync(new HttpRequestException("Copilot service returned 500: Internal Server Error", null, HttpStatusCode.InternalServerError));

        var request = new StartReviewJobRequest { Target = "def hello(): pass", Language = "python" };
        ActionResult<AIReviewJobStartedResponseModel> result = await _controller.StartReviewJob(request);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        objectResult.Value.ToString().Should().Contain("An error occurred while starting the review job.");
    }

    #endregion

    #region GetReviewJob Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetReviewJob_WithEmptyJobId_ReturnsBadRequest(string jobId)
    {
        ActionResult<AIReviewJobPolledResponseModel> result = await _controller.GetReviewJob(jobId);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetReviewJob_WithValidJobId_Returns200WithStatus()
    {
        var expectedResponse = new AIReviewJobPolledResponseModel { Status = "completed" };
        _mockReviewManager
            .Setup(m => m.GetCopilotReviewJobAsync("job-123"))
            .ReturnsAsync(expectedResponse);

        ActionResult<AIReviewJobPolledResponseModel> result = await _controller.GetReviewJob("job-123");

        Assert.IsType<LeanJsonResult>(result.Result);
    }

    [Fact]
    public async Task GetReviewJob_WhenManagerThrows_Returns500()
    {
        _mockReviewManager
            .Setup(m => m.GetCopilotReviewJobAsync("bad-job"))
            .ThrowsAsync(new Exception("connection failed"));

        ActionResult<AIReviewJobPolledResponseModel> result = await _controller.GetReviewJob("bad-job");

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    #endregion
}
