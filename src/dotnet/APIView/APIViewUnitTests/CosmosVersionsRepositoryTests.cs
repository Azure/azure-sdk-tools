using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class CosmosVersionsRepositoryTests
{
    private readonly Mock<Container> _mockContainer;
    private readonly CosmosVersionsRepository _repository;

    public CosmosVersionsRepositoryTests()
    {
        _mockContainer = new Mock<Container>();

        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(x => x["CosmosDBName"]).Returns("TestDB");

        var mockCosmosClient = new Mock<CosmosClient>();
        mockCosmosClient.Setup(x => x.GetContainer("TestDB", "APIVersions")).Returns(_mockContainer.Object);

        _repository = new CosmosVersionsRepository(mockConfiguration.Object, mockCosmosClient.Object);
    }

    #region Helpers

    private static ItemResponse<T> MockItemResponse<T>(T resource)
    {
        var response = new Mock<ItemResponse<T>>();
        response.Setup(x => x.Resource).Returns(resource);
        return response.Object;
    }

    private static FeedIterator<T> MockFeedIterator<T>(IEnumerable<T> items)
    {
        var page = new Mock<FeedResponse<T>>();
        page.Setup(x => x.Resource).Returns(items);

        var iterator = new Mock<FeedIterator<T>>();
        iterator.SetupSequence(x => x.HasMoreResults).Returns(true).Returns(false);
        iterator.Setup(x => x.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(page.Object);
        return iterator.Object;
    }

    private static FeedIterator<T> EmptyFeedIterator<T>()
    {
        var iterator = new Mock<FeedIterator<T>>();
        iterator.Setup(x => x.HasMoreResults).Returns(false);
        return iterator.Object;
    }

    #endregion

    #region GetVersionAsync

    [Fact]
    public async Task GetVersionAsync_Found_ReturnsVersion()
    {
        var version = new APIVersionModel { Id = "v1", ReviewId = "r1", VersionIdentifier = "1.0.0" };
        _mockContainer
            .Setup(x => x.ReadItemAsync<APIVersionModel>("v1", new PartitionKey("r1"),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockItemResponse(version));

        var result = await _repository.GetVersionAsync("r1", "v1");

        Assert.Same(version, result);
    }

    [Fact]
    public async Task GetVersionAsync_NotFound_ReturnsNull()
    {
        _mockContainer
            .Setup(x => x.ReadItemAsync<APIVersionModel>("missing", new PartitionKey("r1"),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("not found", HttpStatusCode.NotFound, 0, "", 0));

        var result = await _repository.GetVersionAsync("r1", "missing");

        Assert.Null(result);
    }

    #endregion

    #region UpsertVersionAsync

    [Fact]
    public async Task UpsertVersionAsync_SetsLastUpdatedAndUpsertsWithCorrectPartitionKey()
    {
        var version = new APIVersionModel { Id = "v1", ReviewId = "r1" };
        PartitionKey capturedPk = default;

        _mockContainer
            .Setup(x => x.UpsertItemAsync(It.IsAny<APIVersionModel>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<APIVersionModel, PartitionKey?, ItemRequestOptions, CancellationToken>(
                (_, pk, _, _) => capturedPk = pk ?? default)
            .ReturnsAsync(MockItemResponse(version));

        var before = DateTime.UtcNow;
        await _repository.UpsertVersionAsync(version);

        Assert.True(version.LastUpdated >= before);
        Assert.True(version.LastUpdated <= DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(new PartitionKey("r1"), capturedPk);
    }

    #endregion

    #region DeleteVersionAsync

    [Fact]
    public async Task DeleteVersionAsync_CallsDeleteItemWithCorrectArgs()
    {
        _mockContainer
            .Setup(x => x.DeleteItemAsync<APIVersionModel>("v1", new PartitionKey("r1"),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockItemResponse(default(APIVersionModel)));

        await _repository.DeleteVersionAsync("v1", "r1");

        _mockContainer.Verify(
            x => x.DeleteItemAsync<APIVersionModel>("v1", new PartitionKey("r1"),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetVersionsAsync (by reviewId)

    [Fact]
    public async Task GetVersionsAsync_UsesPartitionKeyAndReturnsResults()
    {
        var versions = new List<APIVersionModel>
        {
            new() { Id = "v1", ReviewId = "r1", VersionIdentifier = "1.0.0" },
            new() { Id = "v2", ReviewId = "r1", VersionIdentifier = "2.0.0" }
        };
        _mockContainer
            .Setup(x => x.GetItemQueryIterator<APIVersionModel>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.Is<QueryRequestOptions>(o => o.PartitionKey == new PartitionKey("r1"))))
            .Returns(MockFeedIterator(versions));

        var result = await _repository.GetVersionsAsync("r1");

        Assert.Equal(2, ((List<APIVersionModel>)result).Count);
    }

    #endregion

    #region GetVersionsAsync (by kind)

    [Fact]
    public async Task GetVersionsAsync_ByKind_UsesPartitionKey()
    {
        _mockContainer
            .Setup(x => x.GetItemQueryIterator<APIVersionModel>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.Is<QueryRequestOptions>(o => o.PartitionKey == new PartitionKey("r1"))))
            .Returns(EmptyFeedIterator<APIVersionModel>());

        var result = await _repository.GetVersionsAsync("r1", VersionKind.Stable);

        Assert.Empty(result);
    }

    #endregion

    #region GetVersionByIdentifierAsync

    [Fact]
    public async Task GetVersionByIdentifierAsync_Found_ReturnsVersion()
    {
        var version = new APIVersionModel { Id = "v1", ReviewId = "r1", VersionIdentifier = "1.0.0" };
        _mockContainer
            .Setup(x => x.GetItemQueryIterator<APIVersionModel>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.Is<QueryRequestOptions>(o => o.PartitionKey == new PartitionKey("r1"))))
            .Returns(MockFeedIterator(new[] { version }));

        var result = await _repository.GetVersionByIdentifierAsync("r1", "1.0.0");

        Assert.Same(version, result);
    }

    [Fact]
    public async Task GetVersionByIdentifierAsync_NotFound_ReturnsNull()
    {
        _mockContainer
            .Setup(x => x.GetItemQueryIterator<APIVersionModel>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.Is<QueryRequestOptions>(o => o.PartitionKey == new PartitionKey("r1"))))
            .Returns(EmptyFeedIterator<APIVersionModel>());

        var result = await _repository.GetVersionByIdentifierAsync("r1", "nonexistent");

        Assert.Null(result);
    }

    #endregion

    #region GetVersionsEligibleForSoftDeleteAsync

    [Fact]
    public async Task GetVersionsEligibleForSoftDeleteAsync_UsesCrossPartitionQuery()
    {
        // requestOptions == null means cross-partition (no PartitionKey set)
        _mockContainer
            .Setup(x => x.GetItemQueryIterator<APIVersionModel>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.Is<QueryRequestOptions>(o => o == null)))
            .Returns(EmptyFeedIterator<APIVersionModel>());

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _repository.GetVersionsEligibleForSoftDeleteAsync(now);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVersionsEligibleForSoftDeleteAsync_ReturnsExpiredVersions()
    {
        var expiredVersion = new APIVersionModel
        {
            Id = "v1", ReviewId = "r1", RetainUntil = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        _mockContainer
            .Setup(x => x.GetItemQueryIterator<APIVersionModel>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.Is<QueryRequestOptions>(o => o == null)))
            .Returns(MockFeedIterator(new[] { expiredVersion }));

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _repository.GetVersionsEligibleForSoftDeleteAsync(now);

        Assert.Single(result);
    }

    #endregion

    #region GetVersionsEligibleForHardDeleteAsync

    [Fact]
    public async Task GetVersionsEligibleForHardDeleteAsync_UsesCrossPartitionQuery()
    {
        _mockContainer
            .Setup(x => x.GetItemQueryIterator<APIVersionModel>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.Is<QueryRequestOptions>(o => o == null)))
            .Returns(EmptyFeedIterator<APIVersionModel>());

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _repository.GetVersionsEligibleForHardDeleteAsync(now);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVersionsEligibleForHardDeleteAsync_ReturnsSoftDeletedExpiredVersions()
    {
        var softDeletedVersion = new APIVersionModel
        {
            Id = "v1", ReviewId = "r1", IsDeleted = true,
            RetainUntil = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        _mockContainer
            .Setup(x => x.GetItemQueryIterator<APIVersionModel>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.Is<QueryRequestOptions>(o => o == null)))
            .Returns(MockFeedIterator(new[] { softDeletedVersion }));

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _repository.GetVersionsEligibleForHardDeleteAsync(now);

        Assert.Single(result);
    }

    #endregion
}
