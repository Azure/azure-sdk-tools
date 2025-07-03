using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.DTOs;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb;
using ApiView;

namespace APIViewUnitTests
{
    public class CommentsManagerTests
    {
        private CommentsManager CreateManager(
            out Mock<ICosmosCommentsRepository> commentsRepoMock,
            out Mock<IHubContext<SignalRHub>> hubContextMock)
        {
            commentsRepoMock = new Mock<ICosmosCommentsRepository>();
            hubContextMock = new Mock<IHubContext<SignalRHub>>();

            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Architects:CSharp"]).Returns("architect1");

            var orgOptionsMock = new Mock<IOptions<OrganizationOptions>>();
            orgOptionsMock.Setup(o => o.Value)
                .Returns(new OrganizationOptions { RequiredOrganization = [] });

            var apiRevision = new APIRevisionListItemModel { Id = "rev1" };
            var codeFileRepoMock = new Mock<IBlobCodeFileRepository>();
            var apiRevisionsManagerMock = new Mock<IAPIRevisionsManager>();
            apiRevisionsManagerMock.Setup(m => m.GetAPIRevisionAsync("rev1")).ReturnsAsync(apiRevision);
            codeFileRepoMock.Setup(r => r.GetCodeFileAsync(apiRevision, false))
                .ReturnsAsync(new RenderedCodeFile(new CodeFile()));

            var reviewRepoMock = new Mock<ICosmosReviewRepository>();
            reviewRepoMock.Setup(r => r.GetReviewAsync("review1"))
                .ReturnsAsync(new ReviewListItemModel { Id = "review1", Language = "CSharp" });

            var authServiceMock = new Mock<IAuthorizationService>();
            var notificationManagerMock = new Mock<INotificationManager>();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            return new CommentsManager(
                apiRevisionsManagerMock.Object,
                authServiceMock.Object,
                commentsRepoMock.Object,
                reviewRepoMock.Object,
                notificationManagerMock.Object,
                codeFileRepoMock.Object,
                hubContextMock.Object,
                httpClientFactoryMock.Object,
                configMock.Object,
                orgOptionsMock.Object
            );
        }

        private ClaimsPrincipal CreateUser(string githubLogin)
        {
            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim("urn:github:login", githubLogin));
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public async Task RequestAgentReply_UserNotArchitect_SendsRestrictedNotification()
        {
            CommentsManager manager = CreateManager(out _, out var hubContextMock);
            ClaimsPrincipal user = CreateUser("not-architect");
            var comment = new CommentItemModel { ReviewId = "review1", APIRevisionId = "rev1", ElementId = "el1" };

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
            CommentsManager manager = CreateManager(out var commentsRepoMock, out var hubContextMock);
            ClaimsPrincipal user = CreateUser("architect1");
            var comment = new CommentItemModel { ReviewId = "review1", APIRevisionId = "rev1", ElementId = "el1" };
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
}
