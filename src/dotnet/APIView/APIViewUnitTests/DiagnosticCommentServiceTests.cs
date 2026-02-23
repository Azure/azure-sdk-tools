using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class DiagnosticCommentServiceTests
{
    private DiagnosticCommentService CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock)
    {
        commentsRepoMock = new Mock<ICosmosCommentsRepository>();
        return new DiagnosticCommentService(commentsRepoMock.Object);
    }

    #region SyncDiagnosticCommentsAsync Tests

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WithNewDiagnostics_CreatesNewComments()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnostics =
        [
            new("DIAG001", "target1", "First diagnostic message", "https://help.com/diag001"),
            new("DIAG002", "target2", "Second diagnostic message", null, CodeDiagnosticLevel.Warning)
        ];

        List<CommentItemModel> upsertedComments = [];
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => upsertedComments.Add(c))
            .Returns(Task.CompletedTask);

        DiagnosticSyncResult result = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnostics,
            new List<CommentItemModel>());

        Assert.True(result.WasSynced);
        Assert.Equal(2, result.Comments.Count);
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
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnostics = [new("DIAG001", "target1", "Diagnostic message", null)];

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Returns(Task.CompletedTask);

        DiagnosticSyncResult firstResult = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnostics,
            new List<CommentItemModel>());

        string establishedHash = firstResult.DiagnosticsHash;

        commentsRepoMock.Invocations.Clear();

        CommentItemModel existingComment = new()
        {
            Id = firstResult.Comments[0].Id,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            CommentSource = CommentSource.Diagnostic
        };

        DiagnosticSyncResult secondResult = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            establishedHash,
            diagnostics,
            new List<CommentItemModel> { existingComment });

        Assert.False(secondResult.WasSynced);
        Assert.Single(secondResult.Comments);
        commentsRepoMock.Verify(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Never);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenDiagnosticDisappears_ResolvesComment()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CommentItemModel existingDiagnosticComment = new()
        {
            Id = "diag-rev1-abc123",
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            IsResolved = false,
            ChangeHistory = []
        };

        CommentItemModel resolvedComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => resolvedComment = c)
            .Returns(Task.CompletedTask);

        DiagnosticSyncResult result = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            "old-hash",
            [],
            new List<CommentItemModel> { existingDiagnosticComment });

        Assert.True(result.WasSynced);
        Assert.Empty(result.Comments);
        Assert.NotNull(resolvedComment);
        Assert.True(resolvedComment.IsResolved);
        Assert.Contains(resolvedComment.ChangeHistory, h => h.ChangeAction == CommentChangeAction.Resolved);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenDiagnosticReappears_UnresolvesIfBotResolved()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnostics = [new("DIAG001", "target1", "Diagnostic message", null)];

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Returns(Task.CompletedTask);

        DiagnosticSyncResult firstResult = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnostics,
            new List<CommentItemModel>());

        string createdCommentId = firstResult.Comments[0].Id;
        commentsRepoMock.Invocations.Clear();

        // Create bot-resolved comment with correct ID
        CommentItemModel botResolvedComment = new()
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

        DiagnosticSyncResult result = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            "old-hash",
            diagnostics,
            new List<CommentItemModel> { botResolvedComment });

        Assert.True(result.WasSynced);
        Assert.Single(result.Comments);
        Assert.NotNull(updatedComment);
        Assert.False(updatedComment.IsResolved);
        Assert.Contains(updatedComment.ChangeHistory, h => h.ChangeAction == CommentChangeAction.UnResolved);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenDiagnosticReappears_DoesNotUnresolveIfUserResolved()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnostics = [new("DIAG001", "target1", "Diagnostic message", null)];

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Returns(Task.CompletedTask);

        DiagnosticSyncResult firstResult = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnostics,
            new List<CommentItemModel>());

        string createdCommentId = firstResult.Comments[0].Id;
        commentsRepoMock.Invocations.Clear();

        CommentItemModel userResolvedComment = new()
        {
            Id = createdCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            IsResolved = true,
            Severity = CommentSeverity.ShouldFix,
            ChangeHistory = [new() { ChangeAction = CommentChangeAction.Resolved, ChangedBy = "human-user" }]
        };

        DiagnosticSyncResult result = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            "old-hash",
            diagnostics,
            new List<CommentItemModel> { userResolvedComment });

        Assert.True(result.WasSynced);
        Assert.Single(result.Comments);
        Assert.True(result.Comments[0].IsResolved);
        Assert.DoesNotContain(result.Comments[0].ChangeHistory, h => h.ChangeAction == CommentChangeAction.UnResolved);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenSeverityChanges_UpdatesCommentSeverity()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnosticsWarning =
        [
            new("DIAG001", "target1", "Diagnostic message", null, CodeDiagnosticLevel.Warning)
        ];

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Returns(Task.CompletedTask);

        DiagnosticSyncResult firstResult = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnosticsWarning,
            new List<CommentItemModel>());

        string createdCommentId = firstResult.Comments[0].Id;

        commentsRepoMock.Invocations.Clear();
        CommentItemModel existingComment = new()
        {
            Id = createdCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            Severity = CommentSeverity.ShouldFix, // Warning level
            IsResolved = false,
            ChangeHistory = []
        };

        CommentItemModel updatedComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => updatedComment = c)
            .Returns(Task.CompletedTask);

        CodeDiagnostic[] diagnosticsFatal =
        [
            new("DIAG001", "target1", "Diagnostic message", null, CodeDiagnosticLevel.Fatal)
        ];

        DiagnosticSyncResult result = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            "old-hash",
            diagnosticsFatal,
            new List<CommentItemModel> { existingComment });

        Assert.True(result.WasSynced);
        Assert.Single(result.Comments);
        Assert.NotNull(updatedComment);
        Assert.Equal(CommentSeverity.MustFix, updatedComment.Severity);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_MapsLevelsToSeveritiesCorrectly()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnostics =
        [
            new("D1", "t1", "Fatal diag", null, CodeDiagnosticLevel.Fatal),
            new("D2", "t2", "Error diag", null),
            new("D3", "t3", "Warning diag", null, CodeDiagnosticLevel.Warning),
            new("D4", "t4", "Info diag", null, CodeDiagnosticLevel.Info)
        ];

        List<CommentItemModel> upsertedComments = [];
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => upsertedComments.Add(c))
            .Returns(Task.CompletedTask);

        await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnostics,
            new List<CommentItemModel>());

        Assert.Equal(4, upsertedComments.Count);
        Assert.Equal(CommentSeverity.MustFix, upsertedComments.First(c => c.ElementId == "t1").Severity);
        Assert.Equal(CommentSeverity.MustFix, upsertedComments.First(c => c.ElementId == "t2").Severity);
        Assert.Equal(CommentSeverity.ShouldFix, upsertedComments.First(c => c.ElementId == "t3").Severity);
        Assert.Equal(CommentSeverity.Suggestion, upsertedComments.First(c => c.ElementId == "t4").Severity);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WithNullDiagnostics_HandlesGracefully()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        DiagnosticSyncResult result = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            null,
            new List<CommentItemModel>());

        Assert.True(result.WasSynced);
        Assert.Empty(result.Comments);
        Assert.Equal(string.Empty, result.DiagnosticsHash);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_ReturnsCorrectHash()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnostics = [new("DIAG001", "target1", "Test diagnostic", null)];

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Returns(Task.CompletedTask);

        DiagnosticSyncResult result = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnostics,
            new List<CommentItemModel>());

        Assert.True(result.WasSynced);
        Assert.NotNull(result.DiagnosticsHash);
        Assert.NotEmpty(result.DiagnosticsHash);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_SameDiagnosticIdAcrossPageLoads_NoDuplicates()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnostics = [new("DIAG001", "target1", "Diagnostic message", null)];

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Returns(Task.CompletedTask);

        DiagnosticSyncResult firstResult = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnostics,
            new List<CommentItemModel>());

        string firstSyncCommentId = firstResult.Comments[0].Id;
        commentsRepoMock.Invocations.Clear();

        CommentItemModel existingComment = new()
        {
            Id = firstSyncCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            Severity = CommentSeverity.ShouldFix,
            IsResolved = false,
            ChangeHistory = []
        };

        DiagnosticSyncResult secondResult = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            "different-hash", // Different hash to force sync
            diagnostics,
            new List<CommentItemModel> { existingComment });

        Assert.Single(secondResult.Comments);
        Assert.Equal(firstSyncCommentId, secondResult.Comments[0].Id);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_IncludesHelpLinkInCommentText()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnostics =
        [
            new("DIAG001", "target1", "Diagnostic with help link",
                "https://docs.microsoft.com/help/diag001")
        ];

        CommentItemModel createdComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => createdComment = c)
            .Returns(Task.CompletedTask);

        await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnostics,
            new List<CommentItemModel>());

        Assert.NotNull(createdComment);
        Assert.Contains("Diagnostic with help link", createdComment.CommentText);
        Assert.Contains("[Details](https://docs.microsoft.com/help/diag001)", createdComment.CommentText);
    }

    [Fact]
    public async Task SyncDiagnosticCommentsAsync_WhenHelpLinkChanges_UpdatesCommentText()
    {
        DiagnosticCommentService service = CreateService(out Mock<ICosmosCommentsRepository> commentsRepoMock);

        CodeDiagnostic[] diagnosticsOldLink =
        [
            new("DIAG001", "target1", "Diagnostic message", "https://old-docs.com/help")
        ];

        string createdCommentId = null;
        string originalCommentText = null;

        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c =>
            {
                createdCommentId = c.Id;
                originalCommentText = c.CommentText;
            })
            .Returns(Task.CompletedTask);

        await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            null,
            diagnosticsOldLink,
            new List<CommentItemModel>());

        Assert.Contains("[Details](https://old-docs.com/help)", originalCommentText);

        commentsRepoMock.Invocations.Clear();

        CommentItemModel existingComment = new()
        {
            Id = createdCommentId,
            ReviewId = "review1",
            APIRevisionId = "rev1",
            ElementId = "target1",
            CommentSource = CommentSource.Diagnostic,
            CommentText = originalCommentText,
            Severity = CommentSeverity.ShouldFix,
            IsResolved = false,
            ChangeHistory = []
        };

        CommentItemModel updatedComment = null;
        commentsRepoMock.Setup(r => r.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
            .Callback<CommentItemModel>(c => updatedComment = c)
            .Returns(Task.CompletedTask);

        CodeDiagnostic[] diagnosticsNewLink =
        [
            new("DIAG001", "target1", "Diagnostic message", "https://new-docs.com/updated-help")
        ];

        DiagnosticSyncResult result = await service.SyncDiagnosticCommentsAsync(
            "review1",
            "rev1",
            "old-hash",
            diagnosticsNewLink,
            new List<CommentItemModel> { existingComment });

        Assert.Single(result.Comments);
        Assert.NotNull(updatedComment);
        Assert.Contains("[Details](https://new-docs.com/updated-help)", updatedComment.CommentText);
        Assert.DoesNotContain("old-docs.com", updatedComment.CommentText);
    }

    #endregion
}
