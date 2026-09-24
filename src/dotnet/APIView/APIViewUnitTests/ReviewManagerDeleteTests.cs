using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using APIViewWeb.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net.Http;
using Xunit;

namespace APIViewUnitTests;

public class ReviewManagerDeleteTests
{
    /// <summary>
    /// Verifies that SoftDeleteReviewAsync cascade-deletes ALL child revisions
    /// regardless of their type (Manual, Automatic, PullRequest) or creator,
    /// using the no-guard overload that skips owner and type assertions.
    /// </summary>
    [Fact]
    public async Task SoftDeleteReviewAsync_CascadeDeletesAllRevisions_RegardlessOfTypeOrOwner()
    {
        // Arrange
        (ReviewManager reviewManager, MockContainer mocks) = CreateTestSetup();
        var adminUser = CreateTestUser("adminuser");
        string reviewId = "test-review-id";

        var review = new ReviewListItemModel
        {
            Id = reviewId,
            ChangeHistory = new List<ReviewChangeHistoryModel>()
        };

        var revisions = new List<APIRevisionListItemModel>
        {
            new() { Id = "rev-manual", APIRevisionType = APIRevisionType.Manual, CreatedBy = "adminuser",
                     ReviewId = reviewId, ChangeHistory = new List<APIRevisionChangeHistoryModel>() },
            new() { Id = "rev-automatic", APIRevisionType = APIRevisionType.Automatic, CreatedBy = "otheruser",
                     ReviewId = reviewId, ChangeHistory = new List<APIRevisionChangeHistoryModel>() },
            new() { Id = "rev-pr", APIRevisionType = APIRevisionType.PullRequest, CreatedBy = "botuser",
                     ReviewId = reviewId, ChangeHistory = new List<APIRevisionChangeHistoryModel>() },
        };

        mocks.ReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
            .ReturnsAsync(review);
        mocks.ReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
            .Returns(Task.CompletedTask);
        mocks.ApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
            .ReturnsAsync(revisions);
        mocks.ApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        mocks.CommentManager.Setup(m => m.SoftDeleteCommentsAsync(It.IsAny<ClaimsPrincipal>(), reviewId))
            .Returns(Task.CompletedTask);

        // Act — should NOT throw (previously threw UnDeletableReviewException for non-Manual revisions)
        await reviewManager.SoftDeleteReviewAsync(adminUser, reviewId, skipOwnerCheck: true);

        // Assert — all three revisions were cascade-deleted via the no-guard overload
        mocks.ApiRevisionsManager.Verify(
            m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "rev-manual"),
                "adminuser",
                "Cascade deleted with review"),
            Times.Once);
        mocks.ApiRevisionsManager.Verify(
            m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "rev-automatic"),
                "adminuser",
                "Cascade deleted with review"),
            Times.Once);
        mocks.ApiRevisionsManager.Verify(
            m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "rev-pr"),
                "adminuser",
                "Cascade deleted with review"),
            Times.Once);

        // The guarded overloads (with ClaimsPrincipal) should never be called during cascade
        mocks.ApiRevisionsManager.Verify(
            m => m.SoftDeleteAPIRevisionAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<APIRevisionListItemModel>()),
            Times.Never);
        mocks.ApiRevisionsManager.Verify(
            m => m.SoftDeleteAPIRevisionAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        // Comments were also cascade-deleted
        mocks.CommentManager.Verify(
            m => m.SoftDeleteCommentsAsync(adminUser, reviewId),
            Times.Once);

        // The review itself was marked as deleted
        mocks.ReviewsRepository.Verify(
            r => r.UpsertReviewAsync(It.Is<ReviewListItemModel>(rev => rev.IsDeleted == true)),
            Times.Once);
    }

    /// <summary>
    /// Verifies that when skipOwnerCheck is false, the owner assertion is invoked.
    /// </summary>
    [Fact]
    public async Task SoftDeleteReviewAsync_WithoutSkipOwnerCheck_AssertReviewOwner()
    {
        // Arrange
        (ReviewManager reviewManager, MockContainer mocks) = CreateTestSetup();
        var user = CreateTestUser("owneruser");
        string reviewId = "test-review-id";

        var review = new ReviewListItemModel
        {
            Id = reviewId,
            ChangeHistory = new List<ReviewChangeHistoryModel>()
        };

        mocks.ReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
            .ReturnsAsync(review);
        mocks.ReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
            .Returns(Task.CompletedTask);
        mocks.ApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
            .ReturnsAsync(new List<APIRevisionListItemModel>());
        mocks.CommentManager.Setup(m => m.SoftDeleteCommentsAsync(It.IsAny<ClaimsPrincipal>(), reviewId))
            .Returns(Task.CompletedTask);

        // Make the authorization check succeed
        mocks.AuthorizationService.Setup(a => a.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        // Act
        await reviewManager.SoftDeleteReviewAsync(user, reviewId, skipOwnerCheck: false);

        // Assert — authorization was checked
        mocks.AuthorizationService.Verify(
            a => a.AuthorizeAsync(user, review, It.IsAny<IEnumerable<IAuthorizationRequirement>>()),
            Times.Once);
    }

    private ClaimsPrincipal CreateTestUser(string login)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimConstants.Login, login),
            new Claim(ClaimConstants.Name, login)
        });
        return new ClaimsPrincipal(identity);
    }

    private (ReviewManager reviewManager, MockContainer mocks) CreateTestSetup()
    {
        var mocks = new MockContainer();
        var reviewManager = new ReviewManager(
            mocks.AuthorizationService.Object,
            mocks.ReviewsRepository.Object,
            mocks.ApiRevisionsManager.Object,
            mocks.CommentManager.Object,
            mocks.CodeFileRepository.Object,
            mocks.CommentsRepository.Object,
            mocks.ApiRevisionsRepository.Object,
            mocks.SignalRHubContext.Object,
            mocks.LanguageServices,
            mocks.TelemetryClient,
            mocks.CodeFileManager.Object,
            mocks.Configuration.Object,
            mocks.HttpClientFactory.Object,
            mocks.PollingJobQueueManager.Object,
            mocks.NotificationManager.Object,
            mocks.PullRequestsRepository.Object,
            mocks.CopilotAuth.Object,
            mocks.Logger.Object
        );
        return (reviewManager, mocks);
    }

    public class MockContainer
    {
        public Mock<IAuthorizationService> AuthorizationService { get; } = new();
        public Mock<ICosmosReviewRepository> ReviewsRepository { get; } = new();
        public Mock<IAPIRevisionsManager> ApiRevisionsManager { get; } = new();
        public Mock<ICommentsManager> CommentManager { get; } = new();
        public Mock<IBlobCodeFileRepository> CodeFileRepository { get; } = new();
        public Mock<ICosmosCommentsRepository> CommentsRepository { get; } = new();
        public Mock<ICosmosAPIRevisionsRepository> ApiRevisionsRepository { get; } = new();
        public Mock<IHubContext<SignalRHub>> SignalRHubContext { get; } = new();
        public TelemetryClient TelemetryClient { get; } = new(new TelemetryConfiguration());
        public Mock<ICodeFileManager> CodeFileManager { get; } = new();
        public Mock<IConfiguration> Configuration { get; } = new();
        public Mock<IHttpClientFactory> HttpClientFactory { get; } = new();
        public Mock<IPollingJobQueueManager> PollingJobQueueManager { get; } = new();
        public Mock<INotificationManager> NotificationManager { get; } = new();
        public Mock<ICosmosPullRequestsRepository> PullRequestsRepository { get; } = new();
        public Mock<ICopilotAuthenticationService> CopilotAuth { get; } = new();
        public Mock<ILogger<ReviewManager>> Logger { get; } = new();
        public IEnumerable<LanguageService> LanguageServices { get; } = new List<LanguageService>();
    }
}
