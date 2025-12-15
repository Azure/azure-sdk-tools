using System;
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

public class CosmosAPIRevisionsRepositoryTests
{
    private const string TestReviewId = "test-review-id";
    
    private readonly Mock<Container> _mockApiRevisionContainer;
    private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
    private readonly CosmosAPIRevisionsRepository _repository;

    public CosmosAPIRevisionsRepositoryTests()
    {
        _mockApiRevisionContainer = new Mock<Container>();
        _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
        
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(x => x["CosmosDBName"]).Returns("TestDB");

        var mockCosmosClient = new Mock<CosmosClient>();
        mockCosmosClient.Setup(x => x.GetContainer("TestDB", "APIRevisions")).Returns(_mockApiRevisionContainer.Object);

        _repository = new CosmosAPIRevisionsRepository(mockConfiguration.Object, mockCosmosClient.Object, _mockReviewsRepository.Object);
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

    private void SetupReviewUpdate()
    {
        _mockReviewsRepository
            .Setup(x => x.UpdateReviewLastUpdatedOnAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>()));
    }

    private static ItemResponse<T> CreateMockResponse<T>(T resource)
    {
        var response = new Mock<ItemResponse<T>>();
        response.Setup(x => x.Resource).Returns(resource);
        return response.Object;
    }

    [Fact]
    public async Task UpsertRevision_CallsUpdateReviewLastUpdatedOn()
    {
        // Arrange
        var revision = CreateTestRevision();
        DateTime capturedTimestamp = default;

        SetupRevisionUpsert();
        _mockReviewsRepository
            .Setup(x => x.UpdateReviewLastUpdatedOnAsync(TestReviewId, It.IsAny<DateTime>()))
            .Callback<string, DateTime>((id, timestamp) => capturedTimestamp = timestamp);

        // Act
        await _repository.UpsertAPIRevisionAsync(revision);

        // Assert
        VerifyRevisionUpserted();
        
        // Verify UpdateReviewLastUpdatedOnAsync was called with current timestamp
        _mockReviewsRepository.Verify(
            x => x.UpdateReviewLastUpdatedOnAsync(TestReviewId, It.IsAny<DateTime>()),
            Times.Once);
        
        // Verify timestamp is current (between 2020 and now)
        Assert.True(capturedTimestamp > new DateTime(2020, 1, 1));
        Assert.True(capturedTimestamp <= DateTime.UtcNow.AddSeconds(1));
    }

    private void VerifyRevisionUpserted() =>
        _mockApiRevisionContainer.Verify(
            x => x.UpsertItemAsync(
                It.IsAny<APIRevisionListItemModel>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
}
