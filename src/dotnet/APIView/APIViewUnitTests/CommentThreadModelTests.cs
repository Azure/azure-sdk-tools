using System;
using System.Collections.Generic;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using Xunit;

namespace APIViewUnitTests;

public class CommentThreadModelTests
{
    [Fact]
    public void ResolvedBy_ShouldBeSetToResolverNotCreator()
    {
        // Arrange
        var comment1 = new CommentItemModel
        {
            Id = "comment1",
            ElementId = "element1",
            CreatedBy = "PersonA",
            IsResolved = true,
            ChangeHistory = new List<CommentChangeHistoryModel>
            {
                new CommentChangeHistoryModel
                {
                    ChangeAction = CommentChangeAction.Created,
                    ChangedBy = "PersonA",
                    ChangedOn = DateTime.UtcNow.AddHours(-2)
                },
                new CommentChangeHistoryModel
                {
                    ChangeAction = CommentChangeAction.Resolved,
                    ChangedBy = "PersonB",
                    ChangedOn = DateTime.UtcNow.AddHours(-1)
                }
            }
        };

        var comments = new List<CommentItemModel> { comment1 };

        // Act
        var threadModel = new CommentThreadModel("review1", "element1", comments);

        // Assert
        Assert.True(threadModel.IsResolved);
        Assert.Equal("PersonB", threadModel.ResolvedBy); // Should be PersonB who resolved it, not PersonA who created it
    }

    [Fact]
    public void ResolvedBy_WithMultipleComments_ShouldUseMostRecentResolution()
    {
        // Arrange
        var comment1 = new CommentItemModel
        {
            Id = "comment1",
            ElementId = "element1",
            CreatedBy = "PersonA",
            IsResolved = true,
            ChangeHistory = new List<CommentChangeHistoryModel>
            {
                new CommentChangeHistoryModel
                {
                    ChangeAction = CommentChangeAction.Resolved,
                    ChangedBy = "PersonB",
                    ChangedOn = DateTime.UtcNow.AddHours(-3)
                }
            }
        };

        var comment2 = new CommentItemModel
        {
            Id = "comment2",
            ElementId = "element1",
            CreatedBy = "PersonC",
            IsResolved = true,
            ChangeHistory = new List<CommentChangeHistoryModel>
            {
                new CommentChangeHistoryModel
                {
                    ChangeAction = CommentChangeAction.Resolved,
                    ChangedBy = "PersonD",
                    ChangedOn = DateTime.UtcNow.AddHours(-1) // Most recent
                }
            }
        };

        var comments = new List<CommentItemModel> { comment1, comment2 };

        // Act
        var threadModel = new CommentThreadModel("review1", "element1", comments);

        // Assert
        Assert.True(threadModel.IsResolved);
        Assert.Equal("PersonD", threadModel.ResolvedBy); // Should be PersonD who has the most recent resolution
    }

    [Fact]
    public void ResolvedBy_WithNoChangeHistory_ShouldFallbackToCreatedBy()
    {
        // Arrange
        var comment1 = new CommentItemModel
        {
            Id = "comment1",
            ElementId = "element1",
            CreatedBy = "PersonA",
            IsResolved = true,
            ChangeHistory = new List<CommentChangeHistoryModel>()
        };

        var comments = new List<CommentItemModel> { comment1 };

        // Act
        var threadModel = new CommentThreadModel("review1", "element1", comments);

        // Assert
        Assert.True(threadModel.IsResolved);
        Assert.Equal("PersonA", threadModel.ResolvedBy); // Should fallback to CreatedBy
    }

    [Fact]
    public void ResolvedBy_WhenNotResolved_ShouldBeNull()
    {
        // Arrange
        var comment1 = new CommentItemModel
        {
            Id = "comment1",
            ElementId = "element1",
            CreatedBy = "PersonA",
            IsResolved = false,
            ChangeHistory = new List<CommentChangeHistoryModel>()
        };

        var comments = new List<CommentItemModel> { comment1 };

        // Act
        var threadModel = new CommentThreadModel("review1", "element1", comments);

        // Assert
        Assert.False(threadModel.IsResolved);
        Assert.Null(threadModel.ResolvedBy);
    }

    [Fact]
    public void ResolvedBy_WithResolveAndUnresolveActions_ShouldUseMostRecentResolve()
    {
        // Arrange
        var comment1 = new CommentItemModel
        {
            Id = "comment1",
            ElementId = "element1",
            CreatedBy = "PersonA",
            IsResolved = true,
            ChangeHistory = new List<CommentChangeHistoryModel>
            {
                new CommentChangeHistoryModel
                {
                    ChangeAction = CommentChangeAction.Resolved,
                    ChangedBy = "PersonB",
                    ChangedOn = DateTime.UtcNow.AddHours(-3)
                },
                new CommentChangeHistoryModel
                {
                    ChangeAction = CommentChangeAction.UnResolved,
                    ChangedBy = "PersonC",
                    ChangedOn = DateTime.UtcNow.AddHours(-2)
                },
                new CommentChangeHistoryModel
                {
                    ChangeAction = CommentChangeAction.Resolved,
                    ChangedBy = "PersonD",
                    ChangedOn = DateTime.UtcNow.AddHours(-1) // Most recent resolution
                }
            }
        };

        var comments = new List<CommentItemModel> { comment1 };

        // Act
        var threadModel = new CommentThreadModel("review1", "element1", comments);

        // Assert
        Assert.True(threadModel.IsResolved);
        Assert.Equal("PersonD", threadModel.ResolvedBy); // Should be PersonD who has the most recent resolution
    }
}
