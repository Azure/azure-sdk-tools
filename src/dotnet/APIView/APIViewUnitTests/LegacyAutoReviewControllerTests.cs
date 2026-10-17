using System.Threading.Tasks;
using APIViewWeb.Controllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class LegacyAutoReviewControllerTests
{
    private readonly Mock<IAPIRevisionsManager> _apiRevisionsManager = new();
    private readonly Mock<IReviewManager> _reviewManager = new();
    private readonly Mock<INamespaceManager> _namespaceManager = new();
    private readonly AutoReviewController _controller;

    public LegacyAutoReviewControllerTests()
    {
        _controller = new AutoReviewController(
            _reviewManager.Object,
            _apiRevisionsManager.Object,
            _namespaceManager.Object);
    }

    [Theory]
    [InlineData("Python", "4.12.0b3")]
    [InlineData("Python", "4.12.0b3.post1")]
    [InlineData("Python", "4.12.0-b3")]
    [InlineData("Python", "4.12.0-B3")]
    [InlineData("Python", "4.12.0beta3")]
    [InlineData("Python", "4.12.0_beta_3")]
    [InlineData("C#", "4.12.0-beta.3")]
    [InlineData("Java", "4.12.0-BETA.3+build.1")]
    public async Task GetReviewStatus_ReturnsOkForBetaWhenPackageNameIsApproved(string language, string packageVersion)
    {
        SetupReview(isPackageNameApproved: true);

        var result = await _controller.GetReviewStatus(language, "Azure.Test", packageVersion: packageVersion);

        result.Should().BeOfType<OkResult>();
    }

    [Theory]
    [InlineData("Python", "4.12.0b3")]
    [InlineData("C#", "4.12.0-beta.3")]
    public async Task GetReviewStatus_RequiresPackageNameApprovalForBeta(string language, string packageVersion)
    {
        SetupReview(isPackageNameApproved: false);

        var result = await _controller.GetReviewStatus(language, "Azure.Test", packageVersion: packageVersion);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status202Accepted);
    }

    [Fact]
    public async Task GetReviewStatus_ReturnsCreatedForStableWhenPackageNameIsApproved()
    {
        SetupReview(isPackageNameApproved: true);

        var result = await _controller.GetReviewStatus("C#", "Azure.Test", packageVersion: "4.12.0");

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    private void SetupReview(bool isPackageNameApproved)
    {
        const string reviewId = "review-id";
        var review = new ReviewListItemModel
        {
            Id = reviewId,
            ProjectId = "project-id",
            Language = "C#",
            PackageName = "Azure.Test",
            IsApproved = isPackageNameApproved
        };
        var revision = new APIRevisionListItemModel
        {
            Id = "revision-id",
            ReviewId = reviewId,
            IsApproved = false
        };

        _reviewManager
            .Setup(manager => manager.GetReviewAsync(It.IsAny<string>(), "Azure.Test", null))
            .ReturnsAsync(review);
        _apiRevisionsManager
            .Setup(manager => manager.GetAPIRevisionsAsync(reviewId, It.IsAny<string>(), APIRevisionType.Automatic))
            .ReturnsAsync([revision]);
        _namespaceManager
            .Setup(manager => manager.IsNamespaceApprovedAsync(review.ProjectId, review.Language))
            .ReturnsAsync(false);
    }
}
