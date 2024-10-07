using System;
using APIViewWeb;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Xunit;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Azure.Identity;

namespace APIViewIntegrationTests.RepositoryTests
{
    public class CosmosReviewRepositoryTestsBaseFixture : IDisposable
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _cosmosDBname;
        public CosmosReviewRepository ReviewRepository { get; private set; }

        public CosmosReviewRepositoryTestsBaseFixture()
        {
            var config = new ConfigurationBuilder()
               .AddEnvironmentVariables(prefix: "APIVIEW_")
               .AddUserSecrets(typeof(TestsBaseFixture).Assembly)
               .Build();

            _cosmosDBname = "CosmosReviewRepositoryTestsDB";
            config["CosmosDBName"] = _cosmosDBname;

            _cosmosClient = new CosmosClient(config["CosmosEndpoint"], new DefaultAzureCredential());
            var dataBaseResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync(config["CosmosDBName"]).Result;
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id").Wait();

            ReviewRepository = new CosmosReviewRepository(config, _cosmosClient);
            PopulateDBWithDummyReviewData().Wait();
        }

        private async Task PopulateDBWithDummyReviewData()
        {
            List<ReviewListItemModel> testReviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel { Id = "1", IsClosed = false},
                new ReviewListItemModel { Id = "2", IsClosed = true},
                new ReviewListItemModel { Id = "3", IsClosed = false},
                new ReviewListItemModel { Id = "4", IsClosed = false},
                new ReviewListItemModel { Id = "5", IsClosed = false},
            };
            foreach (var review in testReviews)
            {
                await ReviewRepository.UpsertReviewAsync(review);
            }
        }

        public void Dispose()
        {
            _cosmosClient.GetDatabase(_cosmosDBname).DeleteAsync().Wait();
            _cosmosClient.Dispose();
        }
    }

    [CollectionDefinition("CosmosReviewRepositoryTestsCollection")]
    public class CosmosReviewRepositoryTestsCollection : ICollectionFixture<CosmosReviewRepositoryTestsBaseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    [Collection("CosmosReviewRepositoryTestsCollection")]
    public class CosmosReviewRepositoryTests
    {
        private readonly CosmosReviewRepositoryTestsBaseFixture _fixture;

        public CosmosReviewRepositoryTests(CosmosReviewRepositoryTestsBaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetPullRequestAsync_By_ReviewId_ReturnsCorrectNumberOfPullRequests()
        {
            var reviews = await _fixture.ReviewRepository.GetReviewsAsync(reviewIds: new List<string> { "1", "2", "3", "4" });
            Assert.Equal(4, reviews.Count());

            reviews = await _fixture.ReviewRepository.GetReviewsAsync(reviewIds: new List<string> { "1", "2", "3", "4", "5" }, isClosed: true);
            Assert.Single(reviews);

            reviews = await _fixture.ReviewRepository.GetReviewsAsync(reviewIds: new List<string> { "1", "2", "3", "4", "5" }, isClosed: false);
            Assert.Equal(4, reviews.Count());
        } 
    }
}
