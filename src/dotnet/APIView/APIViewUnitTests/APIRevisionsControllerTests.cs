using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class APIRevisionsControllerTests
{
    private readonly APIRevisionsTokenAuthController _controller;
    private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
    private readonly Mock<ILogger<APIRevisionsTokenAuthController>> _mockLogger;

    public APIRevisionsControllerTests()
    {
        _mockLogger = new Mock<ILogger<APIRevisionsTokenAuthController>>();
        _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();

        Mock<IReviewManager> mockReviewManager = new();
        Mock<INotificationManager> mockNotificationManager = new();
        Mock<IHubContext<SignalRHub>> mockSignalRHubContext = new();
        Mock<IConfiguration> mockConfiguration = new();
        Mock<IHttpClientFactory> mockHttpClientFactory = new();
        Mock<IPullRequestManager> mockPullRequestManager = new();

        _controller = new APIRevisionsTokenAuthController(
            _mockLogger.Object,
            _mockApiRevisionsManager.Object
        );

        List<Claim> claims = new() { new Claim("login", "testuser") };
        ClaimsIdentity identity = new(claims, "mock");
        ClaimsPrincipal principal = new(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_SpecificType_WithValidId_ReturnsRevisionText()
    {
        string reviewId = "review123";
        string apiRevisionId = "revision456";
        string expectedText = "API revision text content";

        APIRevisionListItemModel expectedRevision = CreateMockAPIRevision(apiRevisionId);

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync(It.IsAny<ClaimsPrincipal>(), apiRevisionId))
            .ReturnsAsync(expectedRevision);

        _mockApiRevisionsManager
            .Setup(x => x.GetApiRevisionText(expectedRevision))
            .ReturnsAsync(expectedText);

        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId,
            apiRevisionId);

        LeanJsonResult actionResult = Assert.IsType<LeanJsonResult>(result.Result);
        Assert.Equal(expectedText, actionResult.Value);
        _mockApiRevisionsManager.Verify(x => x.GetAPIRevisionAsync(It.IsAny<ClaimsPrincipal>(), apiRevisionId),
            Times.Once);
        _mockApiRevisionsManager.Verify(x => x.GetApiRevisionText(expectedRevision), Times.Once);
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_SpecificType_WithoutId_ReturnsBadRequest()
    {
        string reviewId = "review123";
        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId);

        BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("apiRevisionId is required when selectionType is Specific",
            badRequestResult.Value);
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_SpecificType_WithDeletedRevision_ReturnsNoContent()
    {
        string reviewId = "review123";
        string apiRevisionId = "revision456";
        APIRevisionListItemModel deletedRevision = CreateMockAPIRevision(apiRevisionId, true);

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync(It.IsAny<ClaimsPrincipal>(), apiRevisionId))
            .ReturnsAsync(deletedRevision);

        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId,
            apiRevisionId);

        LeanJsonResult actionResult = Assert.IsType<LeanJsonResult>(result.Result);
        Assert.Null(actionResult.Value);
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_LatestType_ReturnsLatestRevision()
    {
        string reviewId = "review123";
        APIRevisionListItemModel expectedRevision = CreateMockAPIRevision("latest456");
        string expectedText = "Latest revision text";

        _mockApiRevisionsManager
            .Setup(x => x.GetLatestAPIRevisionsAsync(reviewId, null, APIRevisionType.All))
            .ReturnsAsync(expectedRevision);

        _mockApiRevisionsManager
            .Setup(x => x.GetApiRevisionText(expectedRevision))
            .ReturnsAsync(expectedText);

        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId,
            null,
            APIRevisionSelectionType.Latest);

        LeanJsonResult actionResult = Assert.IsType<LeanJsonResult>(result.Result);
        Assert.Equal(expectedText, actionResult.Value);
        _mockApiRevisionsManager.Verify(x => x.GetLatestAPIRevisionsAsync(reviewId, null, APIRevisionType.All),
            Times.Once);
        _mockApiRevisionsManager.Verify(x => x.GetApiRevisionText(expectedRevision), Times.Once);
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_LatestApprovedType_ReturnsLatestApprovedRevision()
    {
        string reviewId = "review123";
        List<APIRevisionListItemModel> allRevisions =
        [
            CreateMockAPIRevision("rev1", false, false, DateTime.UtcNow.AddDays(-3)), // not approved
            CreateMockAPIRevision("rev2", false, true, DateTime.UtcNow.AddDays(-2)), // approved, older
            CreateMockAPIRevision("rev3", false, true, DateTime.UtcNow.AddDays(-1)), // approved, newer
            CreateMockAPIRevision("rev4", true, true, DateTime.UtcNow) // approved but deleted
        ];

        APIRevisionListItemModel expectedRevision = allRevisions.FirstOrDefault(r => r.Id == "rev3");
        string expectedText = "Approved revision text";

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
            .ReturnsAsync(allRevisions);

        _mockApiRevisionsManager
            .Setup(x => x.GetApiRevisionText(expectedRevision))
            .ReturnsAsync(expectedText);

        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId,
            null,
            APIRevisionSelectionType.LatestApproved);

        LeanJsonResult actionResult = Assert.IsType<LeanJsonResult>(result.Result);
        Assert.Equal(expectedText, actionResult.Value);
        _mockApiRevisionsManager.Verify(x => x.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All), Times.Once);
        _mockApiRevisionsManager.Verify(x => x.GetApiRevisionText(expectedRevision), Times.Once);
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_LatestApprovedType_NoApprovedRevisions_ReturnsNotFound()
    {
        string reviewId = "review123";
        List<APIRevisionListItemModel> allRevisions = new()
        {
            CreateMockAPIRevision("rev1"), // not approved
            CreateMockAPIRevision("rev2", true, true) // approved but deleted
        };

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
            .ReturnsAsync(allRevisions);

        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId,
            null,
            APIRevisionSelectionType.LatestApproved);

        NotFoundObjectResult notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("No API revision found for selection type: LatestApproved", notFoundResult.Value);
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_LatestManualType_ReturnsLatestManualRevision()
    {
        string reviewId = "review123";
        APIRevisionListItemModel expectedRevision = CreateMockAPIRevision("manual456");
        string expectedText = "Manual revision text";

        _mockApiRevisionsManager
            .Setup(x => x.GetLatestAPIRevisionsAsync(reviewId, null, APIRevisionType.Manual))
            .ReturnsAsync(expectedRevision);

        _mockApiRevisionsManager
            .Setup(x => x.GetApiRevisionText(expectedRevision))
            .ReturnsAsync(expectedText);

        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId,
            null,
            APIRevisionSelectionType.LatestManual);

        LeanJsonResult actionResult = Assert.IsType<LeanJsonResult>(result.Result);
        Assert.Equal(expectedText, actionResult.Value);
        _mockApiRevisionsManager.Verify(x => x.GetLatestAPIRevisionsAsync(reviewId, null, APIRevisionType.Manual),
            Times.Once);
        _mockApiRevisionsManager.Verify(x => x.GetApiRevisionText(expectedRevision), Times.Once);
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_LatestType_NoRevisionFound_ReturnsNotFound()
    {
        string reviewId = "review123";
        _mockApiRevisionsManager
            .Setup(x => x.GetLatestAPIRevisionsAsync(reviewId, null, APIRevisionType.All))
            .ReturnsAsync((APIRevisionListItemModel)null);

        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId,
            null,
            APIRevisionSelectionType.Latest);

        NotFoundObjectResult notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("No API revision found for selection type: Latest", notFoundResult.Value);
    }

    [Fact]
    public async Task GetAPIRevisionTextAsync_ExceptionThrown_ReturnsInternalServerError()
    {
        string reviewId = "review123";
        string apiRevisionId = "revision456";

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync(It.IsAny<ClaimsPrincipal>(), apiRevisionId))
            .ThrowsAsync(new Exception("Database error"));

        ActionResult<string> result = await _controller.GetAPIRevisionTextAsync(
            reviewId,
            apiRevisionId);

        ObjectResult statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        Assert.Equal("Failed to generate review text", statusCodeResult.Value);
    }

    private static APIRevisionListItemModel CreateMockAPIRevision(
        string id,
        bool isDeleted = false,
        bool isApproved = false,
        DateTime? createdOn = null)
    {
        return new APIRevisionListItemModel
        {
            Id = id,
            IsDeleted = isDeleted,
            IsApproved = isApproved,
            CreatedOn = createdOn ?? DateTime.UtcNow,
            ReviewId = "review123",
            Files = [],
            ChangeHistory = [],
            Approvers = [],
            ViewedBy = [],
            AssignedReviewers = [],
            HeadingsOfSectionsWithDiff = new Dictionary<string, HashSet<int>>()
        };
    }
}
