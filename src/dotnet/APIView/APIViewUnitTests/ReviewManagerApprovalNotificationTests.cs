using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView;
using APIView.Identity;
using APIViewWeb;
using APIViewWeb.Helpers;
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
using Xunit;

namespace APIViewUnitTests;

public class ReviewManagerApprovalNotificationTests
{
    [Fact]
    public async Task ToggleReviewApprovalAsync_SendsSubscriberEmailOnlyWhenTransitioningToApproved()
    {
        // Arrange
        (ReviewManager reviewManager, MockContainer mocks) = CreateTestSetup();
        var user = CreateTestUser("testapprover");
        var review = new ReviewListItemModel
        {
            Id = "review-1",
            Language = ApiViewConstants.TypeSpecLanguage,
            ChangeHistory = new List<ReviewChangeHistoryModel>()
        };
        var revision = new APIRevisionListItemModel
        {
            Id = "revision-1",
            ReviewId = review.Id
        };

        mocks.ReviewsRepository.Setup(r => r.GetReviewAsync(review.Id)).ReturnsAsync(review);
        mocks.ReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>())).Returns(Task.CompletedTask);
        mocks.ApiRevisionsRepository.Setup(r => r.GetAPIRevisionAsync("revision-1")).ReturnsAsync(revision);
        mocks.AuthorizationService
            .Setup(a => a.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        SetupSignalRMocks(mocks.SignalRHubContext);

        // Act + Assert
        await reviewManager.ToggleReviewApprovalAsync(user, review.Id, "revision-1"); // approve
        mocks.NotificationManager.Verify(
            n => n.NotifySubscribersOnApprovalAsync(review, revision, user, true),
            Times.Once);

        await reviewManager.ToggleReviewApprovalAsync(user, review.Id, "revision-1"); // revert

        // Still exactly one call, proving revert does not send notification.
        mocks.NotificationManager.Verify(
            n => n.NotifySubscribersOnApprovalAsync(review, revision, user, true),
            Times.Once);
    }

    private static void SetupSignalRMocks(Mock<IHubContext<SignalRHub>> hubContext)
    {
        var clients = new Mock<IHubClients>();
        var proxy = new Mock<IClientProxy>();

        proxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        clients.Setup(c => c.All).Returns(proxy.Object);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
    }

    private static ClaimsPrincipal CreateTestUser(string login)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimConstants.Login, login),
            new Claim(ClaimConstants.Name, login),
            new Claim(ClaimConstants.Email, $"{login}@contoso.com")
        });
        return new ClaimsPrincipal(identity);
    }

    private static (ReviewManager reviewManager, MockContainer mocks) CreateTestSetup()
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
            mocks.Logger.Object);

        return (reviewManager, mocks);
    }

    private sealed class MockContainer
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
