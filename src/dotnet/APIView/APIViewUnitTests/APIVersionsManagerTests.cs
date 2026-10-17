using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Repositories;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class APIVersionsManagerTests
{
    private readonly Mock<ICosmosVersionsRepository> _mockRepo;
    private readonly APIVersionsManager _manager;

    public APIVersionsManagerTests()
    {
        _mockRepo = new Mock<ICosmosVersionsRepository>();
        _manager = new APIVersionsManager(_mockRepo.Object);
    }

    [Fact]
    public async Task GetOrCreateVersionAsync_ExistingVersion_ReturnsExistingWithoutUpsert()
    {
        var existing = new APIVersionModel
        {
            Id = "existing-id", ReviewId = "review-1", VersionIdentifier = "1.0.0", Kind = VersionKind.Stable
        };
        _mockRepo
            .Setup(x => x.GetVersionByIdentifierAsync("review-1", "1.0.0", It.IsAny<VersionKind?>()))
            .ReturnsAsync(existing);

        APIVersionModel result = await _manager.GetOrCreateVersionAsync("review-1", "1.0.0");

        Assert.Same(existing, result);
        _mockRepo.Verify(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateVersionAsync_NewVersion_UpsertsAndReturnsNewModel()
    {
        _mockRepo
            .Setup(x => x.GetVersionByIdentifierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VersionKind?>()))
            .ReturnsAsync((APIVersionModel)null);
        _mockRepo
            .Setup(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>()))
            .Returns(Task.CompletedTask);

        APIVersionModel result = await _manager.GetOrCreateVersionAsync("review-1", "2.0.0-beta.1");

        Assert.NotNull(result);
        Assert.Equal("review-1", result.ReviewId);
        Assert.Equal("2.0.0-beta.1", result.VersionIdentifier);
        Assert.Equal(VersionKind.Preview, result.Kind);
        Assert.Single(result.ChangeHistory);
        Assert.Equal(APIVersionChangeAction.Created, result.ChangeHistory[0].ChangeAction);
        _mockRepo.Verify(x => x.UpsertVersionAsync(result), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateVersionAsync_PullRequestRevision_SetsPRIdentifierAndKind()
    {
        _mockRepo
            .Setup(x => x.GetVersionByIdentifierAsync("review-1", "PR#42", It.IsAny<VersionKind?>()))
            .ReturnsAsync((APIVersionModel)null);
        _mockRepo
            .Setup(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>()))
            .Returns(Task.CompletedTask);

        var result = await _manager.GetOrCreateVersionAsync("review-1", "1.0.0-alpha.20260101.1",
            pullRequestNo: 42, sourceBranch: "feature/foo");

        Assert.Equal("PR#42", result.VersionIdentifier);
        Assert.Equal(VersionKind.PullRequest, result.Kind);
        Assert.Equal(42, result.PullRequestNumber);
        Assert.Equal("feature/foo", result.SourceBranch);
        Assert.Equal(PullRequestStatus.Open, result.PrStatus);
    }

    [Fact]
    public async Task GetOrCreateVersionAsync_PR_SubsequentPushWithDifferentPackageVersion_ReturnsSameEntity()
    {
        // First push created PR#42 with packageVersion "1.0.0-beta.1".
        var existing = new APIVersionModel
        {
            Id = "pr-version-id", ReviewId = "review-1",
            VersionIdentifier = "PR#42", Kind = VersionKind.PullRequest, PullRequestNumber = 42
        };
        _mockRepo
            .Setup(x => x.GetVersionByIdentifierAsync("review-1", "PR#42", It.IsAny<VersionKind?>()))
            .ReturnsAsync(existing);

        // Second push: version bumped to "1.0.0" (e.g. beta.1 → stable candidate).
        var result = await _manager.GetOrCreateVersionAsync("review-1", "1.0.0",
            pullRequestNo: 42, sourceBranch: "feature/foo");

        // Must return the same entity — identity is PR#42, not the version string.
        Assert.Same(existing, result);
        Assert.Equal("PR#42", result.VersionIdentifier);
        _mockRepo.Verify(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateVersionAsync_StableVersion_KindIsStable()
    {
        _mockRepo
            .Setup(x => x.GetVersionByIdentifierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VersionKind?>()))
            .ReturnsAsync((APIVersionModel)null);
        _mockRepo.Setup(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>())).Returns(Task.CompletedTask);

        var result = await _manager.GetOrCreateVersionAsync("review-1", "3.2.1");

        Assert.Equal(VersionKind.Stable, result.Kind);
    }

    [Fact]
    public async Task GetOrCreateVersionAsync_RollingVersion_KindIsRolling()
    {
        _mockRepo
            .Setup(x => x.GetVersionByIdentifierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VersionKind?>()))
            .ReturnsAsync((APIVersionModel)null);
        _mockRepo.Setup(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>())).Returns(Task.CompletedTask);

        var result =
            await _manager.GetOrCreateVersionAsync("review-1", "2.0.0-alpha.20231015.3");

        Assert.Equal(VersionKind.RollingPrerelease, result.Kind);
    }
    #region AutoSoftDeleteExpiredVersionsAsync

    [Fact]
    public async Task AutoSoftDeleteExpiredVersionsAsync_MarksEligibleVersionsAsDeleted()
    {
        var now = DateTime.UtcNow;
        var v1 = new APIVersionModel
        {
            Id = "v1", ReviewId = "review-1", IsDeleted = false,
            ChangeHistory = new List<APIVersionChangeHistoryModel>()
        };
        var v2 = new APIVersionModel
        {
            Id = "v2", ReviewId = "review-2", IsDeleted = false,
            ChangeHistory = new List<APIVersionChangeHistoryModel>()
        };
        _mockRepo
            .Setup(x => x.GetVersionsEligibleForSoftDeleteAsync(now))
            .ReturnsAsync(new[] { v1, v2 });
        _mockRepo.Setup(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>())).Returns(Task.CompletedTask);

        await _manager.AutoSoftDeleteExpiredVersionsAsync(now);

        Assert.True(v1.IsDeleted);
        Assert.True(v2.IsDeleted);
        Assert.Equal(APIVersionChangeAction.Deleted, v1.ChangeHistory[0].ChangeAction);
        Assert.Equal(APIVersionChangeAction.Deleted, v2.ChangeHistory[0].ChangeAction);
        _mockRepo.Verify(x => x.UpsertVersionAsync(v1), Times.Once);
        _mockRepo.Verify(x => x.UpsertVersionAsync(v2), Times.Once);
    }

    [Fact]
    public async Task AutoSoftDeleteExpiredVersionsAsync_NoEligible_DoesNothing()
    {
        var now = DateTime.UtcNow;
        _mockRepo
            .Setup(x => x.GetVersionsEligibleForSoftDeleteAsync(now))
            .ReturnsAsync(Array.Empty<APIVersionModel>());

        await _manager.AutoSoftDeleteExpiredVersionsAsync(now);

        _mockRepo.Verify(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>()), Times.Never);
    }

    #endregion

    #region AutoHardDeleteExpiredVersionsAsync

    [Fact]
    public async Task AutoHardDeleteExpiredVersionsAsync_DeletesEligibleVersionsFromDb()
    {
        var now = DateTime.UtcNow;
        var v1 = new APIVersionModel { Id = "v1", ReviewId = "review-1", IsDeleted = true };
        var v2 = new APIVersionModel { Id = "v2", ReviewId = "review-2", IsDeleted = true };
        _mockRepo
            .Setup(x => x.GetVersionsEligibleForHardDeleteAsync(now))
            .ReturnsAsync(new[] { v1, v2 });
        _mockRepo.Setup(x => x.DeleteVersionAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        await _manager.AutoHardDeleteExpiredVersionsAsync(now);

        _mockRepo.Verify(x => x.DeleteVersionAsync("v1", "review-1"), Times.Once);
        _mockRepo.Verify(x => x.DeleteVersionAsync("v2", "review-2"), Times.Once);
        _mockRepo.Verify(x => x.UpsertVersionAsync(It.IsAny<APIVersionModel>()), Times.Never);
    }

    [Fact]
    public async Task AutoHardDeleteExpiredVersionsAsync_NoEligible_DoesNothing()
    {
        var now = DateTime.UtcNow;
        _mockRepo
            .Setup(x => x.GetVersionsEligibleForHardDeleteAsync(now))
            .ReturnsAsync(Array.Empty<APIVersionModel>());

        await _manager.AutoHardDeleteExpiredVersionsAsync(now);

        _mockRepo.Verify(x => x.DeleteVersionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion
}
