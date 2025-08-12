using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb;
using APIViewWeb.DTOs;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace APIViewUnitTests;

public class CommentsManagerTests
{
    private CommentsManager CreateManager(
        out Mock<ICosmosCommentsRepository> commentsRepoMock,
        out Mock<IHubContext<SignalRHub>> hubContextMock)
    {
        commentsRepoMock = new Mock<ICosmosCommentsRepository>();
        hubContextMock = new Mock<IHubContext<SignalRHub>>();

        Mock<IConfiguration> configMock = new();
        configMock.Setup(c => c["approvers"]).Returns("architect1");
        configMock.Setup(c => c["CopilotServiceEndpoint"]).Returns("https://dummy.api/endpoint");

        Mock<IOptions<OrganizationOptions>> orgOptionsMock = new();
        orgOptionsMock.Setup(o => o.Value)
            .Returns(new OrganizationOptions { RequiredOrganization = [] });

        APIRevisionListItemModel apiRevision = new() { Id = "rev1" };
        Mock<IBlobCodeFileRepository> codeFileRepoMock = new();
        Mock<IAPIRevisionsManager> apiRevisionsManagerMock = new();
        apiRevisionsManagerMock.Setup(m => m.GetAPIRevisionAsync("rev1")).ReturnsAsync(apiRevision);
        codeFileRepoMock.Setup(r => r.GetCodeFileAsync(apiRevision, false))
            .ReturnsAsync(new RenderedCodeFile(new CodeFile()));

        Mock<ICosmosReviewRepository> reviewRepoMock = new();
        reviewRepoMock.Setup(r => r.GetReviewAsync("review1"))
            .ReturnsAsync(new ReviewListItemModel { Id = "review1", Language = "CSharp" });

        Mock<IMemoryCache> memoryCacheMock = new();
        Mock<IUserProfileManager> userProfileManagerMock = new();
        Mock<ILogger<UserProfileCache>> userProfileCacheLoggerMock = new();
        UserProfileCache userProfileCache = new(
            memoryCacheMock.Object,
            userProfileManagerMock.Object,
            userProfileCacheLoggerMock.Object
        );

        Mock<IAuthorizationService> authServiceMock = new();
        Mock<INotificationManager> notificationManagerMock = new();
        Mock<IHttpClientFactory> httpClientFactoryMock = new();
        Mock<ILogger<CommentsManager>> logger = new();

        Mock<HttpMessageHandler> handlerMock = new();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK, Content = new StringContent("{}")
            });

        Mock<IBackgroundTaskQueue> backgroundTaskQueueMock = new();
        backgroundTaskQueueMock.Setup(q => q.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<Func<CancellationToken, Task>>(workItem =>
            {
                _ = workItem.Invoke(CancellationToken.None);
            });

        HttpClient httpClient = new(handlerMock.Object);
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        return new CommentsManager(
            apiRevisionsManagerMock.Object,
            authServiceMock.Object,
            commentsRepoMock.Object,
            reviewRepoMock.Object,
            notificationManagerMock.Object,
            codeFileRepoMock.Object,
            hubContextMock.Object,
            httpClientFactoryMock.Object,
            userProfileCache,
            configMock.Object,
            orgOptionsMock.Object,
            backgroundTaskQueueMock.Object,
            logger.Object
        );
    }

    private ClaimsPrincipal CreateUser(string githubLogin)
    {
        ClaimsIdentity identity = new();
        identity.AddClaim(new Claim("urn:github:login", githubLogin));
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task RequestAgentReply_UserNotArchitect_SendsRestrictedNotification()
    {
        CommentsManager manager = CreateManager(out _, out Mock<IHubContext<SignalRHub>> hubContextMock);
        ClaimsPrincipal user = CreateUser("not-architect");
        CommentItemModel comment = new() { ReviewId = "review1", APIRevisionId = "rev1", ElementId = "el1" };

        SiteNotificationDto sentPayload = null;
        string calledMethod = string.Empty;
        hubContextMock.Setup(g =>
                g.Clients.Group(It.IsAny<string>())
                    .SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                calledMethod = method;
                sentPayload = args[0] as SiteNotificationDto;
            })
            .Returns(Task.CompletedTask);

        await manager.RequestAgentReply(user, comment, "rev1");

        Assert.Equal(SiteNotificationStatus.Error, sentPayload.Status);
        Assert.Equal("ReceiveNotification", calledMethod);
    }

    [Fact]
    public async Task RequestAgentReply_UserIsArchitect_AddsAgentComment()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock,
            out Mock<IHubContext<SignalRHub>> hubContextMock);
        ClaimsPrincipal user = CreateUser("architect1");
        CommentItemModel comment = new() { ReviewId = "review1", APIRevisionId = "rev1", ElementId = "el1" };
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "el1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        CommentUpdatesDto commentUpdate = null;
        string calledMethod = string.Empty;
        hubContextMock.Setup(g =>
                g.Clients.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), CancellationToken.None))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                calledMethod = method;
                commentUpdate = args[0] as CommentUpdatesDto;
            })
            .Returns(Task.CompletedTask);

        await manager.RequestAgentReply(user, comment, "rev1");

        Assert.Equal("ReceiveCommentUpdates", calledMethod);
        Assert.Equal("review1", commentUpdate.ReviewId);
    }
}
