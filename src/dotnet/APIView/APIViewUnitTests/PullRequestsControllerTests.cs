using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Exceptions;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
using APIViewWeb.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class PullRequestsControllerTests
{
    private readonly PullRequestsController _controller;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<PullRequestsController>> _mockLogger;
    private readonly Mock<IPullRequestManager> _mockPullRequestManager;

    public PullRequestsControllerTests()
    {
        _mockPullRequestManager = new Mock<IPullRequestManager>();
        _mockLogger = new Mock<ILogger<PullRequestsController>>();
        _mockConfiguration = new Mock<IConfiguration>();

        _controller = new PullRequestsController(
            _mockLogger.Object,
            _mockPullRequestManager.Object,
            _mockConfiguration.Object,
            new List<LanguageService>());

        DefaultHttpContext httpContext = new();
        httpContext.Request.Host = new HostString("localhost");

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task CreateAPIRevisionIfAPIHasChanges_WithDuplicateLineIdException_ReturnsBadRequest()
    {
        string language = "C#";
        string duplicateLineId = "duplicate-123";
        DuplicateLineIdException exception = new(language, duplicateLineId);

        _mockPullRequestManager
            .Setup(x => x.CreateAPIRevisionIfAPIHasChanges(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<CreateAPIRevisionAPIResponse>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(exception);

        ActionResult<IEnumerable<CreateAPIRevisionAPIResponse>> result =
            await _controller.CreateAPIRevisionIfAPIHasChanges(
                "123",
                "test-artifact",
                "test.json",
                "abc123",
                "owner/repo",
                "test-package");

        ActionResult<IEnumerable<CreateAPIRevisionAPIResponse>> actionResult =
            Assert.IsType<ActionResult<IEnumerable<CreateAPIRevisionAPIResponse>>>(result);
        LeanJsonResult leanJsonResult = Assert.IsType<LeanJsonResult>(actionResult.Result);

        ActionContext responseContext = new() { HttpContext = new DefaultHttpContext() };
        await leanJsonResult.ExecuteResultAsync(responseContext);
        Assert.Equal(StatusCodes.Status400BadRequest, responseContext.HttpContext.Response.StatusCode);

        CreateAPIRevisionAPIResponse responseContent = Assert.IsType<CreateAPIRevisionAPIResponse>(leanJsonResult.Value);
        Assert.Single(responseContent.ActionsTaken);
        Assert.Contains(exception.Message, responseContent.ActionsTaken[0]);
    }

    [Fact]
    public async Task CreateAPIRevisionIfAPIHasChanges_WithoutDuplicateLineIdException_ContinuesNormally()
    {
        string expectedUrl = "https://apiview.dev/review/123";
        _mockPullRequestManager
            .Setup(x => x.CreateAPIRevisionIfAPIHasChanges(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<CreateAPIRevisionAPIResponse>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedUrl);

        ActionResult<IEnumerable<CreateAPIRevisionAPIResponse>> result =
            await _controller.CreateAPIRevisionIfAPIHasChanges(
                "123",
                "test-artifact",
                "test.json",
                "abc123",
                "owner/repo",
                "test-package");

        LeanJsonResult leanJsonResult = Assert.IsType<LeanJsonResult>(result.Result);

        ActionContext responseContext = new() { HttpContext = new DefaultHttpContext() };
        await leanJsonResult.ExecuteResultAsync(responseContext);
        Assert.Equal(StatusCodes.Status201Created, responseContext.HttpContext.Response.StatusCode);

        CreateAPIRevisionAPIResponse responseContent = Assert.IsType<CreateAPIRevisionAPIResponse>(leanJsonResult.Value);
        Assert.Equal(expectedUrl, responseContent.APIRevisionUrl);
    }

    [Theory]
    [InlineData("Python", "python-duplicate-id")]
    [InlineData("Java", "java-duplicate-method")]
    [InlineData("TypeScript", "ts-duplicate-interface")]
    public async Task CreateAPIRevisionIfAPIHasChanges_WithDifferentLanguageExceptions_ReturnsCorrectErrorMessage(
        string language, string duplicateLineId)
    {
        DuplicateLineIdException exception = new(language, duplicateLineId);
        _mockPullRequestManager
            .Setup(x => x.CreateAPIRevisionIfAPIHasChanges(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<CreateAPIRevisionAPIResponse>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(exception);

        ActionResult<IEnumerable<CreateAPIRevisionAPIResponse>> result =
            await _controller.CreateAPIRevisionIfAPIHasChanges(
                "123",
                "test-artifact",
                "test.json",
                "abc123",
                "owner/repo",
                "test-package");

        LeanJsonResult leanJsonResult = Assert.IsType<LeanJsonResult>(result.Result);
        ActionContext responseContext = new() { HttpContext = new DefaultHttpContext() };
        await leanJsonResult.ExecuteResultAsync(responseContext);
        Assert.Equal(StatusCodes.Status400BadRequest, responseContext.HttpContext.Response.StatusCode);

        CreateAPIRevisionAPIResponse responseContent = Assert.IsType<CreateAPIRevisionAPIResponse>(leanJsonResult.Value);
        Assert.Single(responseContent.ActionsTaken);
        Assert.Contains($"language-specific parser for {language}", responseContent.ActionsTaken[0]);
        Assert.Contains($"(IDs: '{duplicateLineId}')", responseContent.ActionsTaken[0]);
    }

    [Fact]
    public async Task CreateAPIRevisionIfAPIHasChanges_WithInvalidParams_ReturnsBadRequest()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.QueryString =
            new QueryString(
                "?buildId=123&artifactName=test-artifact&filePath=test.invalid&commitSha=abc123&repoName=owner/repo&packageName=test-package");

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        ActionResult<IEnumerable<CreateAPIRevisionAPIResponse>> result =
            await _controller.CreateAPIRevisionIfAPIHasChanges(
                "123",
                "test-artifact",
                "test.invalid", // Invalid extension
                "abc123",
                "owner/repo",
                "test-package");

        LeanJsonResult leanJsonResult = Assert.IsType<LeanJsonResult>(result.Result);

        ActionContext responseContext = new() { HttpContext = new DefaultHttpContext() };
        await leanJsonResult.ExecuteResultAsync(responseContext);
        Assert.Equal(StatusCodes.Status400BadRequest, responseContext.HttpContext.Response.StatusCode);

        _mockPullRequestManager.Verify(x => x.CreateAPIRevisionIfAPIHasChanges(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<CreateAPIRevisionAPIResponse>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
