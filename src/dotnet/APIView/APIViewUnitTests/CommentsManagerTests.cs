using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApiView;
using APIView;
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
        return CreateManager(out commentsRepoMock, out hubContextMock, out _);
    }

    private CommentsManager CreateManager(
        out Mock<ICosmosCommentsRepository> commentsRepoMock,
        out Mock<IHubContext<SignalRHub>> hubContextMock,
        out Mock<IAPIRevisionsManager> apiRevisionsManagerMock)
    {
        commentsRepoMock = new Mock<ICosmosCommentsRepository>();
        hubContextMock = new Mock<IHubContext<SignalRHub>>();
        apiRevisionsManagerMock = new Mock<IAPIRevisionsManager>();

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        hubContextMock.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
        mockClientProxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConfiguration> configMock = new();
        configMock.Setup(c => c["CopilotServiceEndpoint"]).Returns("https://dummy.api/endpoint");

        Mock<IOptions<OrganizationOptions>> orgOptionsMock = new();
        orgOptionsMock.Setup(o => o.Value)
            .Returns(new OrganizationOptions { RequiredOrganization = [] });

        APIRevisionListItemModel apiRevision = new() { Id = "rev1" };
        Mock<IBlobCodeFileRepository> codeFileRepoMock = new();
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
        Mock<IPermissionsManager> permissionsManagerMock = new();
        permissionsManagerMock.Setup(p => p.GetEffectivePermissionsAsync("architect1")).ReturnsAsync(
            new EffectivePermissions()
            {
                Roles = [new GlobalRoleAssignment() { Role = GlobalRole.Admin }]
            });

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
        copilotAuthService.Setup(c => c.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");
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
            permissionsManagerMock.Object,
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

    private static CommentItemModel CreateComment(
        string id = "comment1",
        string elementId = "element1",
        string threadId = null,
        bool isResolved = false,
        string reviewId = "review1") => new()
    {
        ReviewId = reviewId,
        Id = id,
        ElementId = elementId,
        ThreadId = threadId,
        IsResolved = isResolved,
        Upvotes = new List<string>(),
        Downvotes = new List<string>(),
        ChangeHistory = new List<CommentChangeHistoryModel>()
    };

    private Action<string, object[], CancellationToken> CaptureSignalRUpdate(Action<CommentUpdatesDto> onCapture)
    {
        return (method, args, token) => onCapture(args[0] as CommentUpdatesDto);
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

        CommentItemModel capturedAgentComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => capturedAgentComment = c)
            .Returns(Task.CompletedTask);

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
        Assert.NotNull(capturedAgentComment);
        Assert.Equal(CommentSource.AIGenerated, capturedAgentComment.CommentSource);
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

        BatchConversationRequest request = new()
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

    #region ThreadId Tests

    [Fact]
    public async Task ResolveConversation_WithThreadId_ResolvesOnlyMatchingThread()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");

        var comment1 = CreateComment("c1", threadId: "thread-1");
        var comment2 = CreateComment("c2", threadId: "thread-2");
        var comment3 = CreateComment("c3", threadId: "thread-1");

        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment1, comment2, comment3 });

        await manager.ResolveConversation(user, "review1", "element1", "thread-1");

        Assert.True(comment1.IsResolved);
        Assert.False(comment2.IsResolved);
        Assert.True(comment3.IsResolved);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(comment2), Times.Never);
    }

    [Fact]
    public async Task ResolveConversation_WithNullThreadId_ResolvesOnlyLegacyComments()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");

        var legacyComment = CreateComment("c1", threadId: null);
        var newComment = CreateComment("c2", threadId: "thread-1");

        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { legacyComment, newComment });

        await manager.ResolveConversation(user, "review1", "element1", null);

        Assert.True(legacyComment.IsResolved);
        Assert.False(newComment.IsResolved);
    }

    [Fact]
    public async Task UnresolveConversation_WithThreadId_UnresolvesOnlyMatchingThread()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");

        var comment1 = CreateComment("c1", threadId: "thread-1", isResolved: true);
        var comment2 = CreateComment("c2", threadId: "thread-2", isResolved: true);

        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment1, comment2 });

        await manager.UnresolveConversation(user, "review1", "element1", "thread-1");

        Assert.False(comment1.IsResolved);
        Assert.True(comment2.IsResolved);
    }

    [Fact]
    public async Task UnresolveConversation_WithNullThreadId_UnresolvesLegacyComments()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");

        var comment1 = CreateComment("c1", threadId: "thread-1", isResolved: true);
        var comment2 = CreateComment("c2", threadId: null, isResolved: true);

        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment1, comment2 });

        await manager.UnresolveConversation(user, "review1", "element1", null);

        Assert.True(comment1.IsResolved);
        Assert.False(comment2.IsResolved);
    }

    [Fact]
    public async Task ResolveConversation_BroadcastsThreadId()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock,
            out Mock<IHubContext<SignalRHub>> hubContextMock);
        ClaimsPrincipal user = CreateUser("test-user");

        var comment = CreateComment(threadId: "thread-123");
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        CommentUpdatesDto capturedUpdate = null;
        hubContextMock.Setup(h => h.Clients.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(CaptureSignalRUpdate(dto => capturedUpdate = dto))
            .Returns(Task.CompletedTask);

        await manager.ResolveConversation(user, "review1", "element1", "thread-123");

        Assert.Equal("thread-123", capturedUpdate?.ThreadId);
        Assert.Equal(CommentThreadUpdateAction.CommentResolved, capturedUpdate?.CommentThreadUpdateAction);
    }

    [Fact]
    public async Task UnresolveConversation_BroadcastsThreadId()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock,
            out Mock<IHubContext<SignalRHub>> hubContextMock);
        ClaimsPrincipal user = CreateUser("test-user");

        var comment = CreateComment(threadId: "thread-456", isResolved: true);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment });

        CommentUpdatesDto capturedUpdate = null;
        hubContextMock.Setup(h => h.Clients.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(CaptureSignalRUpdate(dto => capturedUpdate = dto))
            .Returns(Task.CompletedTask);

        await manager.UnresolveConversation(user, "review1", "element1", "thread-456");

        Assert.Equal("thread-456", capturedUpdate?.ThreadId);
        Assert.Equal(CommentThreadUpdateAction.CommentUnResolved, capturedUpdate?.CommentThreadUpdateAction);
    }

    [Fact]
    public async Task CommentsBatchOperationAsync_WithReply_PreservesThreadId()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");

        CommentItemModel comment = CreateComment(threadId: "thread-abc");
        comment.APIRevisionId = "rev1";
        comment.CommentType = CommentType.APIRevision;
        comment.Severity = CommentSeverity.ShouldFix;

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        CommentItemModel capturedReply = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => { if (c.CommentText == "Reply in thread") capturedReply = c; })
            .Returns(Task.CompletedTask);

        var request = new BatchConversationRequest
        {
            CommentIds = ["comment1"],
            CommentReply = "Reply in thread",
            Severity = CommentSeverity.ShouldFix,
            Disposition = ConversationDisposition.KeepOpen
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.Equal("thread-abc", capturedReply?.ThreadId);
    }

    [Fact]
    public async Task CommentsBatchOperationAsync_Resolve_UsesThreadIdFromComment()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");

        CommentItemModel comment1 = CreateComment("comment1", threadId: "thread-1");
        comment1.Severity = CommentSeverity.ShouldFix;
        CommentItemModel comment2 = CreateComment("comment2", threadId: "thread-2");

        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment1);
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", "element1"))
            .ReturnsAsync(new List<CommentItemModel> { comment1, comment2 });

        var request = new BatchConversationRequest
        {
            CommentIds = new List<string> { "comment1" },
            Severity = CommentSeverity.ShouldFix,
            Disposition = ConversationDisposition.Resolve
        };

        await manager.CommentsBatchOperationAsync(user, "review1", request);

        Assert.True(comment1.IsResolved);
        Assert.False(comment2.IsResolved);
    }

    [Fact]
    public async Task AddCommentAsync_BroadcastsThreadId()
    {
        CommentsManager manager = CreateManager(out _, out Mock<IHubContext<SignalRHub>> hubContextMock);
        ClaimsPrincipal user = CreateUser("test-user");

        var comment = CreateComment(threadId: "new-thread-id");
        comment.CommentText = "Test comment";

        CommentUpdatesDto capturedUpdate = null;
        hubContextMock.Setup(h => h.Clients.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(CaptureSignalRUpdate(dto => capturedUpdate = dto))
            .Returns(Task.CompletedTask);

        await manager.AddCommentAsync(user, comment);

        Assert.Equal("new-thread-id", capturedUpdate?.ThreadId);
        Assert.Equal(CommentThreadUpdateAction.CommentCreated, capturedUpdate?.CommentThreadUpdateAction);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_BroadcastsThreadId()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock,
            out Mock<IHubContext<SignalRHub>> hubContextMock);
        ClaimsPrincipal user = CreateUser("test-user");

        CommentItemModel comment = CreateComment(threadId: "thread-to-delete");
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        CommentUpdatesDto capturedUpdate = null;
        hubContextMock.Setup(h => h.Clients.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(CaptureSignalRUpdate(dto => capturedUpdate = dto))
            .Returns(Task.CompletedTask);

        await manager.SoftDeleteCommentAsync(user, "review1", "comment1");

        Assert.Equal("thread-to-delete", capturedUpdate?.ThreadId);
        Assert.Equal(CommentThreadUpdateAction.CommentDeleted, capturedUpdate?.CommentThreadUpdateAction);
    }

    [Fact]
    public async Task ToggleVoteAsync_BroadcastsThreadId()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock,
            out Mock<IHubContext<SignalRHub>> hubContextMock);
        ClaimsPrincipal user = CreateUser("test-user");

        CommentItemModel comment = CreateComment(threadId: "voted-thread");
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        CommentUpdatesDto capturedUpdate = null;
        hubContextMock.Setup(h => h.Clients.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(CaptureSignalRUpdate(dto => capturedUpdate = dto))
            .Returns(Task.CompletedTask);

        await manager.ToggleUpvoteAsync(user, "review1", "comment1");

        Assert.Equal("voted-thread", capturedUpdate?.ThreadId);
        Assert.Equal(CommentThreadUpdateAction.CommentUpVoteToggled, capturedUpdate?.CommentThreadUpdateAction);
    }

    [Fact]
    public async Task UpdateCommentAsync_BroadcastsThreadId()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock,
            out Mock<IHubContext<SignalRHub>> hubContextMock);
        ClaimsPrincipal user = CreateUser("test-user");

        CommentItemModel comment = CreateComment(threadId: "updated-thread");
        comment.CommentText = "Original text";
        comment.TaggedUsers = new HashSet<string>();
        commentsRepoMock.Setup(r => r.GetCommentAsync("review1", "comment1")).ReturnsAsync(comment);

        CommentUpdatesDto capturedUpdate = null;
        hubContextMock.Setup(h => h.Clients.All.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(CaptureSignalRUpdate(dto => capturedUpdate = dto))
            .Returns(Task.CompletedTask);

        await manager.UpdateCommentAsync(user, "review1", "comment1", "Updated text", Array.Empty<string>());

        Assert.Equal("updated-thread", capturedUpdate?.ThreadId);
        Assert.Equal(CommentThreadUpdateAction.CommentTextUpdate, capturedUpdate?.CommentThreadUpdateAction);
    }

    [Fact]
    public async Task MultipleThreadsOnSameElement_ResolveOneThreadOnly()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _);
        ClaimsPrincipal user = CreateUser("test-user");

        CommentItemModel[] thread1 = [CreateComment("c1", threadId: "thread-1"), CreateComment("c2", threadId: "thread-1")];
        CommentItemModel[] thread2 = [CreateComment("c3", threadId: "thread-2")];
        CommentItemModel[] thread3 = [CreateComment("c4", threadId: "thread-3")];

        commentsRepoMock.Setup(r => r.GetCommentsAsync(It.IsAny<string>(), "element1"))
            .ReturnsAsync(thread1.Concat(thread2).Concat(thread3).ToList());

        await manager.ResolveConversation(user, "review1", "element1", "thread-2");

        Assert.All(thread1, c => Assert.False(c.IsResolved));
        Assert.All(thread2, c => Assert.True(c.IsResolved));
        Assert.All(thread3, c => Assert.False(c.IsResolved));
    }

    #endregion

    #region SyncDiagnosticCommentsAsync Tests

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WithNewDiagnostics_CreatesNewComments()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var apiRevision = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        var diagnostics = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "First diagnostic message", "https://help.com/diag001"),
            new CodeDiagnostic("DIAG002", "target2", "Second diagnostic message", null, CodeDiagnosticLevel.Warning)
        };

        var upsertedComments = new List<CommentItemModel>();
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => upsertedComments.Add(c))
            .Returns(Task.CompletedTask);

        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision, diagnostics, new List<CommentItemModel>());

        Assert.Equal(2, result.Count);
        Assert.Equal(2, upsertedComments.Count);
        Assert.All(upsertedComments, c =>
        {
            Assert.Equal(CommentSource.Diagnostic, c.CommentSource);
            Assert.Equal("review1", c.ReviewId);
            Assert.Equal("rev1", c.APIRevisionId);
            Assert.StartsWith("diag-rev1-", c.Id);
            Assert.False(c.IsResolved);
        });
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WithExistingUnchangedDiagnostics_SkipsSyncWhenHashMatches()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var diagnostics = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic message", null)
        };

        var apiRevision1 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Returns(Task.CompletedTask);

        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        // First sync to establish hash
        await manager.SyncDiagnosticCommentsAsync(apiRevision1, diagnostics, new List<CommentItemModel>());
        string establishedHash = apiRevision1.DiagnosticsHash;

        // Reset mocks for second call
        commentsRepoMock.Invocations.Clear();
        apiRevisionsManagerMock.Invocations.Clear();

        // Second call with same hash - should skip
        var apiRevision2 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = establishedHash
        };

        var existingComment = new CommentItemModel
        {
            Id = "diag-rev1-existing",
            ReviewId = "review1",
            APIRevisionId = "rev1",
            CommentSource = CommentSource.Diagnostic
        };

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision2, diagnostics, new List<CommentItemModel> { existingComment });

        Assert.Single(result);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Never);
        apiRevisionsManagerMock.Verify(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()), Times.Never);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenDiagnosticDisappears_ResolvesComment()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var apiRevision = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = "old-hash"
        };

        var existingDiagnosticComment = new CommentItemModel
        {
            Id = "diag-rev1-abc123",
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            IsResolved = false,
            ChangeHistory = new List<CommentChangeHistoryModel>()
        };

        CommentItemModel resolvedComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => resolvedComment = c)
            .Returns(Task.CompletedTask);

        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision, Array.Empty<CodeDiagnostic>(), new List<CommentItemModel> { existingDiagnosticComment });

        Assert.Empty(result);
        Assert.NotNull(resolvedComment);
        Assert.True(resolvedComment.IsResolved);
        Assert.Contains(resolvedComment.ChangeHistory, h => h.ChangeAction == CommentChangeAction.Resolved);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenDiagnosticReappears_UnresolvesIfBotResolved()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var diagnostics = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic message", null)
        };

        // First, create the diagnostic to get the correct ID
        var apiRevision1 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        string createdCommentId = null;
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", false, null))
            .ReturnsAsync(new List<CommentItemModel>());
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => createdCommentId = c.Id)
            .Returns(Task.CompletedTask);
        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await manager.SyncDiagnosticCommentsAsync(apiRevision1, diagnostics, new List<CommentItemModel>());

        // Now test unresolve - reset and set up bot-resolved comment with correct ID
        commentsRepoMock.Invocations.Clear();

        var apiRevision2 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = "old-hash"
        };

        var botResolvedComment = new CommentItemModel
        {
            Id = createdCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            IsResolved = true,
            Severity = CommentSeverity.ShouldFix,
            ChangeHistory = [new() { ChangeAction = CommentChangeAction.Resolved, ChangedBy = "azure-sdk" }]
        };

        CommentItemModel updatedComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => updatedComment = c)
            .Returns(Task.CompletedTask);

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision2, diagnostics, new List<CommentItemModel> { botResolvedComment });

        Assert.Single(result);
        Assert.NotNull(updatedComment);
        Assert.False(updatedComment.IsResolved);
        Assert.Contains(updatedComment.ChangeHistory, h => h.ChangeAction == CommentChangeAction.UnResolved);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenDiagnosticReappears_DoesNotUnresolveIfUserResolved()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var diagnostics = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic message", null)
        };

        var apiRevision1 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        string createdCommentId = null;
        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", false, null))
            .ReturnsAsync(new List<CommentItemModel>());
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => createdCommentId = c.Id)
            .Returns(Task.CompletedTask);
        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await manager.SyncDiagnosticCommentsAsync(apiRevision1, diagnostics, new List<CommentItemModel>());

        commentsRepoMock.Invocations.Clear();

        var apiRevision2 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = "old-hash"
        };

        // Simulate a user-resolved diagnostic comment with correct ID
        var userResolvedComment = new CommentItemModel
        {
            Id = createdCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            IsResolved = true,
            Severity = CommentSeverity.ShouldFix,
            ChangeHistory = new List<CommentChangeHistoryModel>
            {
                new() { ChangeAction = CommentChangeAction.Resolved, ChangedBy = "human-user" }
            }
        };

        commentsRepoMock.Setup(r => r.GetCommentsAsync("review1", false, null))
            .ReturnsAsync(new List<CommentItemModel> { userResolvedComment });

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision2, diagnostics, new List<CommentItemModel> { userResolvedComment });

        Assert.Single(result);
        Assert.True(result[0].IsResolved);
        Assert.DoesNotContain(result[0].ChangeHistory, h => h.ChangeAction == CommentChangeAction.UnResolved);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenSeverityChanges_UpdatesCommentSeverity()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var diagnosticsWarning = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic message", null, CodeDiagnosticLevel.Warning)
        };

        var apiRevision1 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        string createdCommentId = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => createdCommentId = c.Id)
            .Returns(Task.CompletedTask);
        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await manager.SyncDiagnosticCommentsAsync(apiRevision1, diagnosticsWarning, new List<CommentItemModel>());

        commentsRepoMock.Invocations.Clear();
        var apiRevision2 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = "old-hash"
        };

        var existingComment = new CommentItemModel
        {
            Id = createdCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            Severity = CommentSeverity.ShouldFix, // Was Warning
            IsResolved = false,
            ChangeHistory = new List<CommentChangeHistoryModel>()
        };

        CommentItemModel updatedComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => updatedComment = c)
            .Returns(Task.CompletedTask);

        var diagnosticsFatal = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic message", null, CodeDiagnosticLevel.Fatal)
        };

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision2, diagnosticsFatal, new List<CommentItemModel> { existingComment });

        Assert.Single(result);
        Assert.NotNull(updatedComment);
        Assert.Equal(CommentSeverity.MustFix, updatedComment.Severity);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_MapsLevelsToSeveritiesCorrectly()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var apiRevision = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        var diagnostics = new[]
        {
            new CodeDiagnostic("D1", "t1", "Fatal diag", null, CodeDiagnosticLevel.Fatal),
            new CodeDiagnostic("D2", "t2", "Error diag", null, CodeDiagnosticLevel.Error),
            new CodeDiagnostic("D3", "t3", "Warning diag", null, CodeDiagnosticLevel.Warning),
            new CodeDiagnostic("D4", "t4", "Info diag", null, CodeDiagnosticLevel.Info)
        };

        var upsertedComments = new List<CommentItemModel>();
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => upsertedComments.Add(c))
            .Returns(Task.CompletedTask);

        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await manager.SyncDiagnosticCommentsAsync(apiRevision, diagnostics, new List<CommentItemModel>());

        Assert.Equal(4, upsertedComments.Count);
        Assert.Equal(CommentSeverity.MustFix, upsertedComments.First(c => c.ElementId == "t1").Severity);
        Assert.Equal(CommentSeverity.MustFix, upsertedComments.First(c => c.ElementId == "t2").Severity);
        Assert.Equal(CommentSeverity.ShouldFix, upsertedComments.First(c => c.ElementId == "t3").Severity);
        Assert.Equal(CommentSeverity.Suggestion, upsertedComments.First(c => c.ElementId == "t4").Severity);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WithNullDiagnostics_HandlesGracefully()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var apiRevision = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision, null, new List<CommentItemModel>());

        Assert.Empty(result);
        Assert.Equal(string.Empty, apiRevision.DiagnosticsHash);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_UpdatesRevisionHashAfterSync()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var apiRevision = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        var diagnostics = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Test diagnostic", null)
        };

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Returns(Task.CompletedTask);

        APIRevisionListItemModel updatedRevision = null;
        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Callback<APIRevisionListItemModel>(r => updatedRevision = r)
            .Returns(Task.CompletedTask);

        await manager.SyncDiagnosticCommentsAsync(apiRevision, diagnostics, new List<CommentItemModel>());

        Assert.NotNull(updatedRevision);
        Assert.NotNull(updatedRevision.DiagnosticsHash);
        Assert.NotEmpty(updatedRevision.DiagnosticsHash);
        apiRevisionsManagerMock.Verify(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()), Times.Once);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_SameDiagnosticIdAcrossPageLoads_NoDuplicates()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var diagnostics = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic message", null)
        };

        var apiRevision1 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        string firstSyncCommentId = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => firstSyncCommentId = c.Id)
            .Returns(Task.CompletedTask);
        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await manager.SyncDiagnosticCommentsAsync(apiRevision1, diagnostics, new List<CommentItemModel>());

        commentsRepoMock.Invocations.Clear();

        var apiRevision2 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = "different-hash"
        };

        var existingComment = new CommentItemModel
        {
            Id = firstSyncCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            Severity = CommentSeverity.ShouldFix,
            IsResolved = false,
            ChangeHistory = new List<CommentChangeHistoryModel>()
        };

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision2, diagnostics, new List<CommentItemModel>());

        Assert.Single(result);
        Assert.Equal(existingComment.Id, result[0].Id);
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_IncludesHelpLinkInCommentText()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        var apiRevision = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        var diagnostics = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic with help link", "https://docs.microsoft.com/help/diag001")
        };

        CommentItemModel createdComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => createdComment = c)
            .Returns(Task.CompletedTask);

        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await manager.SyncDiagnosticCommentsAsync(apiRevision, diagnostics, new List<CommentItemModel>());

        Assert.NotNull(createdComment);
        Assert.Contains("Diagnostic with help link", createdComment.CommentText);
        Assert.Contains("[Details](https://docs.microsoft.com/help/diag001)", createdComment.CommentText);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenHelpLinkChanges_UpdatesCommentText()
    {
        CommentsManager manager = CreateManager(out Mock<ICosmosCommentsRepository> commentsRepoMock, out _, out Mock<IAPIRevisionsManager> apiRevisionsManagerMock);

        // First, create a diagnostic with an old help link
        var diagnosticsOldLink = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic message", "https://old-docs.com/help")
        };

        var apiRevision1 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = null
        };

        string createdCommentId = null;
        string originalCommentText = null;

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c =>
            {
                createdCommentId = c.Id;
                originalCommentText = c.CommentText;
            })
            .Returns(Task.CompletedTask);
        apiRevisionsManagerMock.Setup(m => m.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
            .Returns(Task.CompletedTask);

        await manager.SyncDiagnosticCommentsAsync(apiRevision1, diagnosticsOldLink, new List<CommentItemModel>());

        // Verify original comment has old link
        Assert.Contains("[Details](https://old-docs.com/help)", originalCommentText);

        // Reset mocks
        commentsRepoMock.Invocations.Clear();

        var apiRevision2 = new APIRevisionListItemModel
        {
            Id = "rev1",
            ReviewId = "review1",
            DiagnosticsHash = "old-hash"
        };

        // Existing comment with old help link
        var existingComment = new CommentItemModel
        {
            Id = createdCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            CommentText = originalCommentText,
            Severity = CommentSeverity.ShouldFix,
            IsResolved = false,
            ChangeHistory = new List<CommentChangeHistoryModel>()
        };

        CommentItemModel updatedComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => updatedComment = c)
            .Returns(Task.CompletedTask);

        // Same diagnostic text but with a new help link URL
        var diagnosticsNewLink = new[]
        {
            new CodeDiagnostic("DIAG001", "target1", "Diagnostic message", "https://new-docs.com/updated-help")
        };

        var result = await manager.SyncDiagnosticCommentsAsync(apiRevision2, diagnosticsNewLink, new List<CommentItemModel> { existingComment });

        Assert.Single(result);
        Assert.NotNull(updatedComment);
        Assert.Contains("[Details](https://new-docs.com/updated-help)", updatedComment.CommentText);
        Assert.DoesNotContain("old-docs.com", updatedComment.CommentText);
    }

    #endregion
}
