using System;
using System.Collections.Generic;
using System.Linq;
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

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        hubContextMock.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
        mockClientProxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
        Mock<ICopilotAuthenticationService> copilotAuthService = new();
        backgroundTaskQueueMock.Setup(q => q.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<Func<CancellationToken, Task>>(workItem =>
            {
                _ = workItem.Invoke(CancellationToken.None);
            });

        HttpClient httpClient = new(handlerMock.Object);
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        authServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

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
            copilotAuthService.Object,
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

    #region ToggleVoteAsync Tests

    [Fact]
    public async Task ToggleVoteAsync_UpvoteNotPresent_AddsUpvoteAndRemovesDownvote()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            Upvotes = new List<string>(),
            Downvotes = new List<string> { "test-user" }
        };
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        await manager.ToggleUpvoteAsync(user, "review1", "comment1");

        Assert.Single(comment.Upvotes);
        Assert.Contains("test-user", comment.Upvotes);
        Assert.Empty(comment.Downvotes);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Once);
    }

    [Fact]
    public async Task ToggleVoteAsync_UpvotePresent_RemovesUpvote()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            Upvotes = new List<string> { "test-user" },
            Downvotes = new List<string>()
        };
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        await manager.ToggleUpvoteAsync(user, "review1", "comment1");

        Assert.Empty(comment.Upvotes);
        Assert.Empty(comment.Downvotes);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Once);
    }

    [Fact]
    public async Task ToggleVoteAsync_DownvoteNotPresent_AddsDownvoteAndRemovesUpvote()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            Upvotes = new List<string> { "test-user" },
            Downvotes = new List<string>()
        };
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        await manager.ToggleDownvoteAsync(user, "review1", "comment1");

        Assert.Empty(comment.Upvotes);
        Assert.Single(comment.Downvotes);
        Assert.Contains("test-user", comment.Downvotes);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Once);
    }

    [Fact]
    public async Task ToggleVoteAsync_DownvotePresent_RemovesDownvote()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            Upvotes = new List<string>(),
            Downvotes = new List<string> { "test-user" }
        };
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        await manager.ToggleDownvoteAsync(user, "review1", "comment1");

        Assert.Empty(comment.Upvotes);
        Assert.Empty(comment.Downvotes);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Once);
    }

    #endregion

    #region CommentsBatchOperationAsync Tests

    [Fact]
    public async Task ResolveBatchConversationAsync_WithUpvote_AppliesUpvoteAndResolves()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment1 = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            Severity = CommentSeverity.ShouldFix,
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };
        CommentItemModel comment2 = new()
        {
            ReviewId = "review1",
            Id = "comment2",
            ElementId = "element2",
            Severity = CommentSeverity.ShouldFix,
            Upvotes = new List<string>(),
            Downvotes = new List<string>(),
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment1);
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment2")).ReturnsAsync(comment2);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment1 });
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element2"))
            .ReturnsAsync(new List<CommentItemModel> { comment2 });

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1", "comment2" },
            Vote = FeedbackVote.Up,
            Severity = CommentSeverity.ShouldFix, 
            Disposition = ConversationDisposition.Resolve
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Single(comment1.Upvotes);
        Assert.Contains("test-user", comment1.Upvotes);
        Assert.Single(comment2.Upvotes);
        Assert.Contains("test-user", comment2.Upvotes);
        Assert.True(comment1.IsResolved);
        Assert.True(comment2.IsResolved);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Exactly(4)); // 2 votes + 2 resolves
    }

    [Fact]
    public async Task ResolveBatchConversationAsync_WithDownvote_AppliesDownvoteAndResolves()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            Severity = CommentSeverity.ShouldFix,
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        ResolveBatchConversationRequest request = new()
        {
            Severity = CommentSeverity.ShouldFix,
            CommentIds = new List<string> { "comment1" },
            Vote = FeedbackVote.Down,
            Disposition = ConversationDisposition.Resolve
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Single(comment.Downvotes);
        Assert.Contains("test-user", comment.Downvotes);
        Assert.True(comment.IsResolved);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Exactly(2)); // 1 vote + 1 resolve
    }

    [Fact]
    public async Task ResolveBatchConversationAsync_WithNoVote_OnlyResolves()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            Severity = CommentSeverity.ShouldFix,
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Vote = FeedbackVote.None,
            Severity = CommentSeverity.ShouldFix,
            Disposition = ConversationDisposition.Resolve
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Empty(comment.Upvotes);
        Assert.Empty(comment.Downvotes);
        Assert.True(comment.IsResolved);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Once); // Only resolve, no vote
    }

    [Fact]
    public async Task ResolveBatchConversationAsync_WithCommentReply_AddsReplyAndResolves()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            APIRevisionId = "rev1",
            CommentType = CommentType.APIRevision,
            Upvotes = new List<string>(),
            Downvotes = new List<string>(),
            Severity = CommentSeverity.ShouldFix
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Vote = FeedbackVote.None,
            Severity = CommentSeverity.ShouldFix,
            CommentReply = "This is resolved now",
            Disposition = ConversationDisposition.Resolve
        };

        CommentItemModel capturedReply = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c =>
            {
                if (c.CommentText == "This is resolved now")
                {
                    capturedReply = c;
                    c.Id = "new-comment-id"; // Simulate ID being set by repository
                }
            })
            .Returns(Task.CompletedTask);

        var response = await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.NotNull(capturedReply);
        Assert.Equal("This is resolved now", capturedReply.CommentText);
        Assert.Equal("element1", capturedReply.ElementId);
        Assert.Equal("test-user", capturedReply.CreatedBy);
        Assert.True(comment.IsResolved);
        Assert.Single(response);
        Assert.Equal("new-comment-id", response.FirstOrDefault()?.Id);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Exactly(2)); // 1 reply + 1 resolve
    }

    [Fact]
    public async Task ResolveBatchConversationAsync_WithVoteAndReply_AppliesBothAndResolves()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            APIRevisionId = "rev1",
            CommentType = CommentType.APIRevision,
            Severity = CommentSeverity.ShouldFix,
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Vote = FeedbackVote.Up,
            CommentReply = "Fixed this issue",
            Severity = CommentSeverity.ShouldFix,
            Disposition = ConversationDisposition.Resolve
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Single(comment.Upvotes);
        Assert.Contains("test-user", comment.Upvotes);
        Assert.True(comment.IsResolved);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Exactly(3)); // 1 vote + 1 reply + 1 resolve
    }

    [Fact]
    public async Task ResolveBatchConversationAsync_MultipleComments_ProcessesAllInOrder()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        
        List<CommentItemModel> comments = new()
        {
            new() { ReviewId = "review1", Id = "comment1", ElementId = "element1", Upvotes = new List<string>(), Downvotes = new List<string>(), Severity = CommentSeverity.ShouldFix},
            new() { ReviewId = "review1", Id = "comment2", ElementId = "element2", Upvotes = new List<string>(), Downvotes = new List<string>(), Severity = CommentSeverity.ShouldFix },
            new() { ReviewId = "review1", Id = "comment3", ElementId = "element3", Upvotes = new List<string>(), Downvotes = new List<string>(), Severity = CommentSeverity.ShouldFix }
        };

        foreach (var comment in comments)
        {
            commentsRepoMock.Setup(r => r.GetCommentAsync("review1", comment.Id)).ReturnsAsync(comment);
            commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", comment.ElementId))
                .ReturnsAsync(new List<CommentItemModel> { comment });
        }

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1", "comment2", "comment3" },
            Vote = FeedbackVote.Up,
            Severity = CommentSeverity.ShouldFix,
            Disposition = ConversationDisposition.Resolve
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        foreach (var comment in comments)
        {
            Assert.Single(comment.Upvotes);
            Assert.Contains("test-user", comment.Upvotes);
            Assert.True(comment.IsResolved);
        }
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Exactly(6)); // 3 votes + 3 resolves
    }

    [Fact]
    public async Task ResolveBatchConversationAsync_EmptyCommentIds_DoesNothing()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string>(),
            Vote = FeedbackVote.Up
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        commentsRepoMock.Verify(r => r.GetCommentAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Never);
    }

    [Fact]
    public async Task CommentsBatchOperationAsync_KeepOpen_AppliesVoteAndReplyWithoutResolving()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            APIRevisionId = "rev1",
            CommentType = CommentType.APIRevision,
            Severity = CommentSeverity.ShouldFix,
            Upvotes = new List<string>(),
            Downvotes = new List<string>(),
            IsResolved = false
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Vote = FeedbackVote.Up,
            CommentReply = "Thanks for the feedback",
            Severity = CommentSeverity.ShouldFix,
            Disposition = ConversationDisposition.KeepOpen
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Single(comment.Upvotes);
        Assert.Contains("test-user", comment.Upvotes);
        Assert.False(comment.IsResolved); // Should NOT be resolved
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Exactly(2)); // 1 vote + 1 reply
    }

    [Fact]
    public async Task CommentsBatchOperationAsync_KeepOpen_WithoutReply_OnlyAppliesVote()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            Severity = CommentSeverity.Question,
            Upvotes = new List<string>(),
            Downvotes = new List<string>(),
            IsResolved = false
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Vote = FeedbackVote.Down,
            Severity = CommentSeverity.Question,
            Disposition = ConversationDisposition.KeepOpen
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Single(comment.Downvotes);
        Assert.Contains("test-user", comment.Downvotes);
        Assert.False(comment.IsResolved);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Once); // Only vote, no reply
    }

    [Fact]
    public async Task CommentsBatchOperationAsync_Delete_DeletesCommentsAndReplies()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment1 = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            APIRevisionId = "rev1",
            CommentType = CommentType.APIRevision,
            Severity = CommentSeverity.Suggestion,
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };
        CommentItemModel comment2 = new()
        {
            ReviewId = "review1",
            Id = "comment2",
            ElementId = "element2",
            APIRevisionId = "rev1",
            CommentType = CommentType.APIRevision,
            Severity = CommentSeverity.Suggestion,
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment1);
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment2")).ReturnsAsync(comment2);

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1", "comment2" },
            CommentReply = "Removing invalid comments",
            Severity = CommentSeverity.Suggestion,
            Disposition = ConversationDisposition.Delete
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.True(comment1.IsDeleted);
        Assert.True(comment2.IsDeleted);
    }


    [Fact]
    public async Task CommentsBatchOperationAsync_UpdateSeverity_UpdatesCommentSeverity()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            Severity = CommentSeverity.Question,
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Severity = CommentSeverity.MustFix,
            Disposition = ConversationDisposition.KeepOpen
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Equal(CommentSeverity.MustFix, comment.Severity);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Once);
    }

   
    [Fact]
    public async Task CommentsBatchOperationAsync_ComplexScenario_UpdatesSeverityVoteReplyAndResolves()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            APIRevisionId = "rev1",
            CommentType = CommentType.APIRevision,
            Severity = CommentSeverity.Question,
            Upvotes = new List<string>(),
            Downvotes = new List<string>(),
            IsResolved = false
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Vote = FeedbackVote.Up,
            CommentReply = "Changed severity and resolved",
            Severity = CommentSeverity.MustFix,
            Disposition = ConversationDisposition.Resolve
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Equal(CommentSeverity.MustFix, comment.Severity);
        Assert.Single(comment.Upvotes);
        Assert.Contains("test-user", comment.Upvotes);
        Assert.True(comment.IsResolved);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Exactly(4)); // 1 severity + 1 vote + 1 reply + 1 resolve
    }

    #endregion

    #region  CommentsFeedback

    [Fact]
    public async Task AddCommentFeedbackAsync_AddsFeedbackToComment()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            Feedback = new List<CommentFeedback>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        CommentFeedbackRequest feedbackRequest = new()
        {
            Reasons = new List<AICommentFeedbackReason> { AICommentFeedbackReason.FactuallyIncorrect },
            Comment = "This is incorrect because...",
            IsDelete = false
        };

        await manager.AddCommentFeedbackAsync(user, "review1", "comment1", feedbackRequest);

        Assert.Single(comment.Feedback);
        Assert.Contains("FactuallyIncorrect", comment.Feedback[0].Reasons);
        Assert.Equal("This is incorrect because...", comment.Feedback[0].Comment);
        Assert.False(comment.Feedback[0].IsDelete);
        Assert.Equal("test-user", comment.Feedback[0].SubmittedBy);
        Assert.NotNull(comment.Feedback[0].SubmittedOn);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment), Times.Once);
    }

    [Fact]
    public async Task AddCommentFeedbackAsync_MultipleReasons_StoresAllReasons()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            Feedback = new List<CommentFeedback>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        CommentFeedbackRequest feedbackRequest = new()
        {
            Reasons = new List<AICommentFeedbackReason>
                {
                    AICommentFeedbackReason.FactuallyIncorrect,
                    AICommentFeedbackReason.RenderingBug,
                    AICommentFeedbackReason.OutdatedGuideline
                },
            Comment = "Multiple issues found",
            IsDelete = false
        };

        await manager.AddCommentFeedbackAsync(user, "review1", "comment1", feedbackRequest);

        Assert.Single(comment.Feedback);
        Assert.Equal(3, comment.Feedback[0].Reasons.Count);
        Assert.Contains("FactuallyIncorrect", comment.Feedback[0].Reasons);
        Assert.Contains("RenderingBug", comment.Feedback[0].Reasons);
        Assert.Contains("OutdatedGuideline", comment.Feedback[0].Reasons);
    }

    [Fact]
    public async Task AddCommentFeedbackAsync_DeletionFeedback_MarksAsDelete()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            Feedback = new List<CommentFeedback>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        CommentFeedbackRequest feedbackRequest = new()
        {
            Reasons = new List<AICommentFeedbackReason>(),
            Comment = "This comment is egregiously wrong",
            IsDelete = true
        };

        await manager.AddCommentFeedbackAsync(user, "review1", "comment1", feedbackRequest);

        Assert.Single(comment.Feedback);
        Assert.True(comment.Feedback[0].IsDelete);
        Assert.Equal("This comment is egregiously wrong", comment.Feedback[0].Comment);
        Assert.Empty(comment.Feedback[0].Reasons);
    }

    [Fact]
    public async Task CommentsBatchOperationAsync_WithFeedback_AddsFeedbackToComment()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            Feedback = new List<CommentFeedback>(),
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Feedback = new CommentFeedbackRequest
            {
                Reasons = new List<AICommentFeedbackReason> { AICommentFeedbackReason.FactuallyIncorrect },
                Comment = "Batch feedback",
                IsDelete = false
            },
            Disposition = ConversationDisposition.KeepOpen
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Single(comment.Feedback);
        Assert.Contains("FactuallyIncorrect", comment.Feedback[0].Reasons);
        Assert.Equal("Batch feedback", comment.Feedback[0].Comment);
        Assert.False(comment.Feedback[0].IsDelete);
    }

    [Fact]
    public async Task CommentsBatchOperationAsync_DeleteWithFeedback_PreservesFeedback()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            Feedback = new List<CommentFeedback>(),
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Feedback = new CommentFeedbackRequest
            {
                Reasons = new List<AICommentFeedbackReason>(),
                Comment = "Deleting because it's egregiously wrong",
                IsDelete = true
            },
            Disposition = ConversationDisposition.Delete
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Single(comment.Feedback);
        Assert.True(comment.Feedback[0].IsDelete);
        Assert.Equal("Deleting because it's egregiously wrong", comment.Feedback[0].Comment);
        Assert.True(comment.IsDeleted);
    }

    [Fact]
    public async Task CommentsBatchOperationAsync_DownvoteWithFeedback_AddsBoth()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");
        CommentItemModel comment = new()
        {
            ReviewId = "review1",
            Id = "comment1",
            ElementId = "element1",
            Feedback = new List<CommentFeedback>(),
            Upvotes = new List<string>(),
            Downvotes = new List<string>()
        };

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        ResolveBatchConversationRequest request = new()
        {
            CommentIds = new List<string> { "comment1" },
            Vote = FeedbackVote.Down,
            Feedback = new CommentFeedbackRequest
            {
                Reasons = new List<AICommentFeedbackReason> { AICommentFeedbackReason.FactuallyIncorrect },
                Comment = "Downvoting with reason",
                IsDelete = false
            },
            Disposition = ConversationDisposition.KeepOpen
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Single(comment.Downvotes);
        Assert.Contains("test-user", comment.Downvotes);
        Assert.Single(comment.Feedback);
        Assert.Contains("FactuallyIncorrect", comment.Feedback[0].Reasons);
        Assert.Equal("Downvoting with reason", comment.Feedback[0].Comment);
    }

    #endregion
}
