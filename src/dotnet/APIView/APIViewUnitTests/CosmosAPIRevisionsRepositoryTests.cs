using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.LeanModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class CosmosAPIRevisionsRepositoryTests
{
    private const string TestReviewId = "test-review-id";
    
    private readonly Mock<Container> _mockApiRevisionContainer;
    private readonly Mock<Container> _mockReviewsContainer;
    private readonly CosmosAPIRevisionsRepository _repository;

    public CosmosAPIRevisionsRepositoryTests()
    {
        _mockApiRevisionContainer = new Mock<Container>();
        _mockReviewsContainer = new Mock<Container>();
        
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(x => x["CosmosDBName"]).Returns("TestDB");

        var mockCosmosClient = new Mock<CosmosClient>();
        mockCosmosClient.Setup(x => x.GetContainer("TestDB", "APIRevisions")).Returns(_mockApiRevisionContainer.Object);
        mockCosmosClient.Setup(x => x.GetContainer("TestDB", "Reviews")).Returns(_mockReviewsContainer.Object);

        _repository = new CosmosAPIRevisionsRepository(mockConfiguration.Object, mockCosmosClient.Object);
    }

    private static APIRevisionListItemModel CreateTestRevision(string reviewId = TestReviewId)
    {
        return new APIRevisionListItemModel
        {
            Id = "test-revision-id",
            ReviewId = reviewId,
            PackageName = "TestPackage",
            Language = "C#"
        };
    }

    private static ReviewListItemModel CreateTestReview(string reviewId, DateTime lastUpdatedOn)
    {
        return new ReviewListItemModel
        {
            Id = reviewId,
            PackageName = "TestPackage",
            Language = "C#",
            LastUpdatedOn = lastUpdatedOn
        };
    }

    private void SetupRevisionUpsert()
    {
        _mockApiRevisionContainer
            .Setup(x => x.UpsertItemAsync(
                It.IsAny<APIRevisionListItemModel>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((APIRevisionListItemModel item, PartitionKey pk, ItemRequestOptions opt, CancellationToken ct) =>
                CreateMockResponse(item));
    }

    private void SetupReviewRead(ReviewListItemModel review)
    {
        _mockReviewsContainer
            .Setup(x => x.ReadItemAsync<ReviewListItemModel>(
                review.Id,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockResponse(review));
    }

    private void SetupReviewUpsert()
    {
        _mockReviewsContainer
            .Setup(x => x.UpsertItemAsync(
                It.IsAny<ReviewListItemModel>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewListItemModel item, PartitionKey pk, ItemRequestOptions opt, CancellationToken ct) =>
                CreateMockResponse(item));
    }

    private static ItemResponse<T> CreateMockResponse<T>(T resource)
    {
        var response = new Mock<ItemResponse<T>>();
        response.Setup(x => x.Resource).Returns(resource);
        return response.Object;
    }

    [Fact]
    public async Task UpsertRevision_UpdatesParentReview_WhenNewer()
    {
        // Arrange
        var revision = CreateTestRevision();
        var review = CreateTestReview(TestReviewId, DateTime.UtcNow.AddDays(-5)); // 5 days old

        SetupRevisionUpsert();
        SetupReviewRead(review);
        SetupReviewUpsert();

        // Act
        await _repository.UpsertAPIRevisionAsync(revision);

        // Assert
        VerifyRevisionUpserted();
        VerifyReviewRead(TestReviewId);
        VerifyReviewUpdated(TestReviewId, review.LastUpdatedOn);
    }

    [Fact]
    public async Task UpsertRevision_SkipsParentReview_WhenOlder()
    {
        // Arrange
        var revision = CreateTestRevision();
        var review = CreateTestReview(TestReviewId, DateTime.UtcNow); // Current time (newer)

        SetupRevisionUpsert();
        SetupReviewRead(review);

        // Act
        await _repository.UpsertAPIRevisionAsync(revision);

        // Assert
        VerifyRevisionUpserted();
        VerifyReviewRead(TestReviewId);
        VerifyReviewNotUpdated();
    }

    [Fact]
    public async Task UpsertRevision_HandlesReviewNotFound()
    {
        // Arrange
        var reviewId = "non-existent-review";
        var revision = CreateTestRevision(reviewId);

        SetupRevisionUpsert();
        _mockReviewsContainer
            .Setup(x => x.ReadItemAsync<ReviewListItemModel>(
                reviewId,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not Found", HttpStatusCode.NotFound, 0, "", 0));

        // Act & Assert - should not throw
        await _repository.UpsertAPIRevisionAsync(revision);

        VerifyRevisionUpserted();
        VerifyReviewRead(reviewId);
        VerifyReviewNotUpdated();
    }

    private void VerifyRevisionUpserted() =>
        _mockApiRevisionContainer.Verify(
            x => x.UpsertItemAsync(
                It.IsAny<APIRevisionListItemModel>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyReviewRead(string reviewId) =>
        _mockReviewsContainer.Verify(
            x => x.ReadItemAsync<ReviewListItemModel>(
                reviewId,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyReviewUpdated(string reviewId, DateTime oldLastUpdatedOn) =>
        _mockReviewsContainer.Verify(
            x => x.UpsertItemAsync(
                It.Is<ReviewListItemModel>(r => r.Id == reviewId && r.LastUpdatedOn > oldLastUpdatedOn),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyReviewNotUpdated() =>
        _mockReviewsContainer.Verify(
            x => x.UpsertItemAsync(
                It.IsAny<ReviewListItemModel>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
}
