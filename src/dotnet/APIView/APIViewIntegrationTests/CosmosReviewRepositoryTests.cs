using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace APIViewIntegrationTests
{
    public class CosmosReviewRepositoryTests
    {
        private readonly CosmosClient _cosmosClient;
        private CosmosReviewRepository _cosmosReviewRepository;
        private readonly string _cosmosEmulatorConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

        public CosmosReviewRepositoryTests()
        {
            _cosmosClient = new CosmosClient(_cosmosEmulatorConnectionString);
            _cosmosReviewRepository = new CosmosReviewRepository(null, _cosmosClient);
        }

        [Theory]
        [InlineData("Name", 64)]
        [InlineData("Revisions[0].Files[0].Language", 2)]
        public async Task GetReviewFirstLevelProprties_Test(string propertyName, int count)
        {
            await CreateTestDataIfNotExistAsync();
            var property = await _cosmosReviewRepository.GetReviewFirstLevelProprtiesAsync(propertyName);
            Assert.Equal(count, property.Count());
        }

        [Theory]
        [InlineData("All")]
        [InlineData("DummyLanguageOne")]
        public async Task GetReviewsOverload1_WithNoPackageName_Test(string language)
        {
            await CreateTestDataIfNotExistAsync();
            var result = await _cosmosReviewRepository.GetReviewsAsync(false, language, null, ReviewType.Manual);
            Assert.All(result, item => Assert.Equal(ReviewType.Manual, item.FilterType));
        }

        [Fact]
        public async Task GetReviewsOverload2_WithServiceAndPackageDisplayName_Test()
        {
            await CreateTestDataIfNotExistAsync();
            var result = await _cosmosReviewRepository.GetReviewsAsync("DummyServiceName1", "DummyPackageDisplayName1", null);
            Assert.Equal(16, result.Count());
        }

        [Fact]
        public async Task GetReviewsOverload2_WithServiceAndPackageDisplayNameAndFilterType_Test()
        {
            await CreateTestDataIfNotExistAsync();
            List<ReviewType> filterTypes = new List<ReviewType> {
                ReviewType.Manual
            };
            var result = await _cosmosReviewRepository.GetReviewsAsync("DummyServiceName1", "DummyPackageDisplayName1", filterTypes);
            Assert.Equal(4, result.Count());
            Assert.All(result, item => Assert.Equal(ReviewType.Manual, item.FilterType));
        }

        [Fact]
        public async Task GetReviewsOverload3_WithOnlySearch_Test()
        {
            await CreateTestDataIfNotExistAsync();
            List<string> searchQueries = new List<string> {
                "Dummy"
            };
            var result = await _cosmosReviewRepository.GetReviewsAsync(searchQueries, null, false, null, false, 0, 50, "LastUpdated");
            Assert.Equal(16, result.Reviews.Count());
            Assert.Equal(16, result.TotalCount);
        }

        [Fact]
        public async Task GetReviewsOverload3_WithOnlyLanguage_Test()
        {
            await CreateTestDataIfNotExistAsync();
            List<string> language = new List<string> {
                "DummyLanguageOne"
            };
            var result = await _cosmosReviewRepository.GetReviewsAsync(null, language, false, null, false, 0, 50, "LastUpdated");
            Assert.Equal(8, result.Reviews.Count());
        }

        [Fact]
        public async Task GetReviewsOverload3_WithOnlyFilterType_Test()
        {
            await CreateTestDataIfNotExistAsync();
            List<int> filterTypes = new List<int> {
                1
            };
            var result = await _cosmosReviewRepository.GetReviewsAsync(null, null, false, filterTypes, false, 0, 50, "LastUpdated");
            Assert.All(result.Reviews, item => Assert.Equal(ReviewType.Automatic, item.FilterType));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetReviewsOverload3_WithOnlyIsClosed_Test(bool boolVal)
        {
            await CreateTestDataIfNotExistAsync();
            var result = await _cosmosReviewRepository.GetReviewsAsync(null, null, boolVal, null, false, 0, 50, "LastUpdated");
            Assert.All(result.Reviews, item => Assert.Equal(item.IsClosed, boolVal));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetReviewsOverload3_WithOnlyIsApproved_Test(bool boolVal)
        {
            await CreateTestDataIfNotExistAsync();
            var result = await _cosmosReviewRepository.GetReviewsAsync(null, null, null, null, boolVal, 0, 50, "LastUpdated");
            Assert.All(result.Reviews, item => Assert.Equal(item.IsApproved, boolVal));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetReviewsOverload3_WithSearchIsClosedAndIsApproved_Test(bool boolVal)
        {
            await CreateTestDataIfNotExistAsync();
            List<string> searchQueries = new List<string> {
                "Dummy"
            };
            var result = await _cosmosReviewRepository.GetReviewsAsync(searchQueries, null, boolVal, null, boolVal, 0, 50, "LastUpdated");
            Assert.Equal(16, result.Reviews.Count());
            Assert.Equal(16, result.TotalCount);
            Assert.All(result.Reviews, item => Assert.Equal(item.IsClosed, boolVal));
            Assert.All(result.Reviews, item => Assert.Equal(item.IsApproved, boolVal));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetReviewsOverload3_WithSearchLanguageAndFilterType_Test(bool boolVal)
        {
            await CreateTestDataIfNotExistAsync();
            List<string> searchQueries = new List<string> {
                "Dummy"
            };
            List<string> language = new List<string> {
                "DummyLanguageOne"
            };
            List<int> filterTypes = new List<int> {
                1
            };
            var result = await _cosmosReviewRepository.GetReviewsAsync(searchQueries, language, boolVal, filterTypes, boolVal, 0, 50, "LastUpdated");
            Assert.Equal(2, result.Reviews.Count());
            Assert.All(result.Reviews, item => Assert.Equal(ReviewType.Automatic, item.FilterType));
        }

        private async Task CreateTestDataIfNotExistAsync()
        {
            var dataBaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync("APIView");
            var containerResponse = await dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id");

            if (containerResponse.StatusCode.Equals(HttpStatusCode.Created))
            {
                foreach (int value in Enumerable.Range(1, 2))
                {
                    string[] languages = new String[] { "DummyLanguageOne", "DummyLanguageTwo" };
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
                                        Name = $"DummyReview{value}_For_{language}_For_{reviewType.ToString()}_For{boolValue}_For{boolVal}",
                                        Author = $"DummyReviewAuthor{value}",
                                        CreationDate = DateTime.Now,
                                        IsClosed = boolValue,
                                        FilterType = reviewType,
                                        ServiceName = $"DummyServiceName{value}",
                                        PackageDisplayName = $"DummyPackageDisplayName{value}"
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
                                                Name = $"DummyFile{value}_{val}_{v}",
                                                Language = language
                                            };
                                            revision.Files.Add(file);
                                        }
                                        revisions.Add(revision);
                                    }
                                    review.Revisions = revisions;
                                    await _cosmosReviewRepository.UpsertReviewAsync(review);
                                }
                            }
                        }
                    }
                }
            }
        }

    }
}
