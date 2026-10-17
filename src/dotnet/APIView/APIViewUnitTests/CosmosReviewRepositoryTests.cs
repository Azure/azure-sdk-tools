using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class CosmosReviewRepositoryTests
{
    private const string TestReviewId = "test-review-id";
    private static readonly DateTime OldDate = new DateTime(2020, 1, 1);
    private static readonly DateTime MiddleDate = new DateTime(2024, 6, 1);
    private static readonly DateTime FutureDate = new DateTime(2099, 12, 31);
    
    private readonly Mock<Container> _mockReviewsContainer;
    private readonly Mock<ILogger<CosmosReviewRepository>> _mockLogger;
    private readonly CosmosReviewRepository _repository;

    public CosmosReviewRepositoryTests()
    {
        _mockReviewsContainer = new Mock<Container>();
        _mockLogger = new Mock<ILogger<CosmosReviewRepository>>();
        
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(x => x["CosmosDBName"]).Returns("TestDB");

        var mockCosmosClient = new Mock<CosmosClient>();
        mockCosmosClient.Setup(x => x.GetContainer("TestDB", "Reviews")).Returns(_mockReviewsContainer.Object);

        _repository = new CosmosReviewRepository(mockConfiguration.Object, mockCosmosClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task UpdateReviewLastUpdatedOn_UpdatesReview_WhenRevisionIsNewer()
    {
        // Arrange
        var review = new ReviewListItemModel
        {
            Id = TestReviewId,
            LastUpdatedOn = OldDate
        };

        var mockResponse = CreateMockResponse(review, "etag123");
        
        _mockReviewsContainer
            .Setup(x => x.ReadItemAsync<ReviewListItemModel>(
                TestReviewId,
                new PartitionKey(TestReviewId),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockReviewsContainer
            .Setup(x => x.UpsertItemAsync(
                It.IsAny<ReviewListItemModel>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewListItemModel item, PartitionKey pk, ItemRequestOptions opt, CancellationToken ct) =>
                CreateMockResponse(item, "etag456").Object);

        // Act
        await _repository.UpdateReviewLastUpdatedOnAsync(TestReviewId, MiddleDate);

        // Assert
        _mockReviewsContainer.Verify(
            x => x.UpsertItemAsync(
                It.Is<ReviewListItemModel>(r => 
                    r.Id == TestReviewId && 
                    r.LastUpdatedOn == MiddleDate),
                new PartitionKey(TestReviewId),
                It.Is<ItemRequestOptions>(opt => opt.IfMatchEtag == "etag123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateReviewLastUpdatedOn_DoesNotUpdate_WhenRevisionIsOlder()
    {
        // Arrange
        var review = new ReviewListItemModel
        {
            Id = TestReviewId,
            LastUpdatedOn = FutureDate
        };

        var mockResponse = CreateMockResponse(review, "etag123");
        
        _mockReviewsContainer
            .Setup(x => x.ReadItemAsync<ReviewListItemModel>(
                TestReviewId,
                new PartitionKey(TestReviewId),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        await _repository.UpdateReviewLastUpdatedOnAsync(TestReviewId, OldDate);

        // Assert - Upsert should NOT be called
        _mockReviewsContainer.Verify(
            x => x.UpsertItemAsync(
                It.IsAny<ReviewListItemModel>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateReviewLastUpdatedOn_DoesNotUpdate_WhenRevisionIsSame()
    {
        // Arrange
        var review = new ReviewListItemModel
        {
            Id = TestReviewId,
            LastUpdatedOn = MiddleDate
        };

        var mockResponse = CreateMockResponse(review, "etag123");
        
        _mockReviewsContainer
            .Setup(x => x.ReadItemAsync<ReviewListItemModel>(
                TestReviewId,
                new PartitionKey(TestReviewId),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        await _repository.UpdateReviewLastUpdatedOnAsync(TestReviewId, MiddleDate);

        // Assert - Upsert should NOT be called
        _mockReviewsContainer.Verify(
            x => x.UpsertItemAsync(
                It.IsAny<ReviewListItemModel>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateReviewLastUpdatedOn_HandlesNotFound_Gracefully()
    {
        // Arrange
        var reviewId = "non-existent-review";
        
        const int subStatusCode = 0;
        const string activityId = "";
        const double requestCharge = 0;
        _mockReviewsContainer
            .Setup(x => x.ReadItemAsync<ReviewListItemModel>(
                reviewId,
                new PartitionKey(reviewId),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not Found", HttpStatusCode.NotFound, subStatusCode, activityId, requestCharge));

        // Act & Assert - should not throw
        await _repository.UpdateReviewLastUpdatedOnAsync(reviewId, MiddleDate);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(reviewId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    private static Mock<ItemResponse<ReviewListItemModel>> CreateMockResponse(ReviewListItemModel resource, string etag)
    {
        var response = new Mock<ItemResponse<ReviewListItemModel>>();
        response.Setup(x => x.Resource).Returns(resource);
        response.Setup(x => x.ETag).Returns(etag);
        return response;
    }
}
