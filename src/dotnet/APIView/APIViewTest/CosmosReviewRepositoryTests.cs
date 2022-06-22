using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using APIViewTest.TestsHelpers;
using APIViewWeb;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace APIViewTest
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
            var property = await _cosmosReviewRepositorpy.GetReviewFirstLevelProprtiesAsync(propertyName);
        }

        private void CreateTestDataIfNotExistAsync()
        {
            var dataBaseResponse = (_cosmosClient.CreateDatabaseIfNotExistsAsync("APIView")).Result;
            var containerResponse = (dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id")).Result;


            foreach (int value in Enumerable.Range(1, 2))
            {
                string [] languages = new String [] { "C", "C#", "C++", "Go", "Java", "JavaScript", "Json", "Protocol", "Python", "Swagger", "Xml" };
                foreach (string language in languages)
                {
                    ReviewType[] reviewTypes = new ReviewType[] { ReviewType.Manual, ReviewType.Automatic, ReviewType.PullRequest, ReviewType.All };
                    foreach (ReviewType reviewType in reviewTypes)
                    {
                        bool [] boolvalues = new bool [] { true , false};
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
                                    IsClosed = boolValue,
                                    IsApproved = boolVal,
                                    Language = language,

                                };

                            }
                        }
                    }
                }
            }
        }

    }
}
