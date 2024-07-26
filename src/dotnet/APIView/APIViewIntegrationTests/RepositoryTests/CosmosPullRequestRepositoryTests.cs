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
    public class CosmosPullRequestRepositoryTestsBaseFixture : IDisposable
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _cosmosDBname;
        public CosmosPullRequestsRepository PullRequestRepositopry { get; private set; }
        public CosmosReviewRepository ReviewRepository { get; private set; }

        public CosmosPullRequestRepositoryTestsBaseFixture()
        {
            var config = new ConfigurationBuilder()
               .AddEnvironmentVariables(prefix: "APIVIEW_")
               .AddUserSecrets(typeof(TestsBaseFixture).Assembly)
               .Build();

            _cosmosDBname = "CosmosPullRequestRepositoryTestsDB";
            config["CosmosDBName"] = _cosmosDBname;

            _cosmosClient = new CosmosClient(config["CosmosEndpoint"], new DefaultAzureCredential());
            var dataBaseResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync(config["CosmosDBName"]).Result;
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id").Wait();
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("PullRequests", "/ReviewId").Wait();

            ReviewRepository = new CosmosReviewRepository(config, _cosmosClient);
            PullRequestRepositopry = new CosmosPullRequestsRepository(config, ReviewRepository, _cosmosClient);
            PopulateDBWithDummyPullRequestData().Wait();
            PopulateDBWithDummyReviewData().Wait();
        }

        private async Task PopulateDBWithDummyPullRequestData()
        {
            List<PullRequestModel> testPullRequests = new List<PullRequestModel>
            {
                new PullRequestModel { Id = "1", ReviewId = "1", IsDeleted = false},
                new PullRequestModel { Id = "2", ReviewId = "1", IsDeleted = false},
                new PullRequestModel { Id = "3", ReviewId = "2", IsDeleted = false},
                new PullRequestModel { Id = "4", ReviewId = "3", IsDeleted = true},
            };
            foreach (var pr in testPullRequests)
            {
                await PullRequestRepositopry.UpsertPullRequestAsync(pr);
            }
        }

        private async Task PopulateDBWithDummyReviewData()
        {
            List<ReviewListItemModel> testReviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel { Id = "1", IsClosed = false},
                new ReviewListItemModel { Id = "2", IsClosed = true},
                new ReviewListItemModel { Id = "3", IsClosed = false}
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

    [CollectionDefinition("CosmosPullRequestRepositoryTestsCollection")]
    public class CosmosPullRequestRepositoryTestsCollection : ICollectionFixture<CosmosPullRequestRepositoryTestsBaseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    [Collection("CosmosPullRequestRepositoryTestsCollection")]
    public class CosmosPullRequestRepositoryTests
    {
        private readonly CosmosPullRequestRepositoryTestsBaseFixture _fixture;

        public CosmosPullRequestRepositoryTests(CosmosPullRequestRepositoryTestsBaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetPullRequestAsync_By_ReviewId_ReturnsCorrectNumberOfPullRequests()
        {
            var pullRequests = await _fixture.PullRequestRepositopry.GetPullRequestsAsync(reviewId: "1");
            Assert.Equal(2, pullRequests.Count());

            pullRequests = await _fixture.PullRequestRepositopry.GetPullRequestsAsync(reviewId: "2");
            Assert.False(pullRequests.Any());

            pullRequests = await _fixture.PullRequestRepositopry.GetPullRequestsAsync(reviewId: "3");
            Assert.False(pullRequests.Any());
        } 
    }
}
