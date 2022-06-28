using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using APIViewIntegrationTests.TestsHelpers;
using APIViewWeb;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace APIViewIntegrationTests
{
    public class CosmosReviewRepositoryTests
    {
        private readonly Container _reviewsContainer;
        private readonly CosmosClient _cosmosClient;
        private IConfiguration _configuration;
        private CosmosReviewRepository _cosmosReviewRepositorpy;

        public CosmosReviewRepositoryTests()
        {
            _configuration = CosmosTestsHelpers.GetIConfigurationRoot();
            _cosmosClient = new CosmosClient(_configuration["Cosmos:EmulatorConnectionString"]);
            _cosmosReviewRepositorpy = new CosmosReviewRepository(_configuration, _cosmosClient);
        }

        [Theory]
        [InlineData("Name")]
        [InlineData("Language")]
        public async Task GetReviewFirstLevelProprtiesTest(string propertyName)
        {
            await CreateTestDataIfNotExistAsync();
            var property = await _cosmosReviewRepositorpy.GetReviewFirstLevelProprtiesAsync(propertyName);
        }

        private async Task CreateTestDataIfNotExistAsync()
        {
            var dataBaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync("APIView");
            var containerResponse = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id");

            if (containerResponse.StatusCode.Equals(HttpStatusCode.Created))
            {
                foreach (int value in Enumerable.Range(1, 2))
                {
                    string[] languages = new String[] { "C", "C#", "C++", "Go", "Java", "JavaScript", "Json", "Protocol", "Python", "Swagger", "Xml" };
                    foreach (string language in languages)
                    {
                        ReviewType[] reviewTypes = new ReviewType[] { ReviewType.Manual, ReviewType.Automatic, ReviewType.PullRequest, ReviewType.All };
                        foreach (ReviewType reviewType in reviewTypes)
                        {
                            bool[] boolvalues = new bool[] { true, false };
                            foreach (var boolValue in boolvalues)
                            {
                                foreach (var boolVal in boolvalues)
                                {
                                    var container = containerResponse.Container;
                                    ReviewModel review = new ReviewModel
                                    {
                                        Name = $"DummyReview{value}",
                                        Author = $"DummyReviewAuthor{value}",
                                        CreationDate = DateTime.Now,
                                        IsClosed = boolValue
                                    };
                                    var revisions = new ReviewRevisionModelList(review);
                                    foreach (int val in Enumerable.Range(1, 2))
                                    {
                                        var revision = new ReviewRevisionModel
                                        {
                                            Name = $"DummyRevision{value}_{val}",
                                        };

                                        if (boolVal)
                                        {
                                            revision.Approvers.Add($"DummyApprover{value}_{val}");
                                        }

                                        foreach (int v in Enumerable.Range(1, 2))
                                        {
                                            var file = new ReviewCodeFileModel
                                            {
                                                Name = $"DummuFile{value}_{val}_{v}",
                                                Language = language
                                            };
                                            revision.Files.Add(file);
                                        }
                                        revisions.Add(revision);
                                    }
                                    review.Revisions = revisions;
                                    await _cosmosReviewRepositorpy.UpsertReviewAsync(review);
                                }
                            }
                        }
                    }
                }
            }
        }

    }
}
