// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosReviewRepository
    {
        private readonly Container _reviewsContainer;
        private readonly CosmosClient _cosmosClient;

        public CosmosReviewRepository(IConfiguration configuration, CosmosClient cosmosClient = null)
        {
            _cosmosClient = cosmosClient ?? new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _reviewsContainer = _cosmosClient.GetContainer("APIView", "Reviews");
        }

        public async Task UpsertReviewAsync(ReviewModel reviewModel)
        {
            await _reviewsContainer.UpsertItemAsync(reviewModel, new PartitionKey(reviewModel.ReviewId));
        }

        public async Task DeleteReviewAsync(ReviewModel reviewModel)
        {
            await _reviewsContainer.DeleteItemAsync<ReviewModel>(reviewModel.ReviewId, new PartitionKey(reviewModel.ReviewId));
        }

        public async Task<ReviewModel> GetReviewAsync(string reviewId)
        {
            return await _reviewsContainer.ReadItemAsync<ReviewModel>(reviewId, new PartitionKey(reviewId));
        }

        public async Task<ReviewModel> GetMasterReviewForPackageAsync(string language, string packageName)
        {
            var reviews = await GetReviewsAsync(false, language, packageName, ReviewType.Automatic);
            return reviews.FirstOrDefault();
        }

        public async Task<IEnumerable<ReviewModel>> GetReviewsAsync(bool isClosed, string language, string packageName = null, ReviewType? filterType = null, bool fetchAllPages = false)
        {
            var queryStringBuilder = new StringBuilder("SELECT * FROM Reviews r WHERE (IS_DEFINED(r.IsClosed) ? r.IsClosed : false) = @isClosed ");

            //Add filter if looking for automatic, manual or PR reviews
            if (filterType != null && filterType != ReviewType.All)
            {
                queryStringBuilder.Append("AND (IS_DEFINED(r.FilterType) ? r.FilterType : 0) = @filterType ");
            }

            // Add language and package name clause
            // Currently we don't have any use case of searching for a package name across languages
            if (language != "All")
            {
                queryStringBuilder.Append("AND EXISTS (SELECT VALUE revision FROM revision in r.Revisions WHERE EXISTS (SELECT VALUE files from files in revision.Files WHERE files.Language = @language");
                if (!String.IsNullOrEmpty(packageName))
                {
                    queryStringBuilder.Append(" AND files.PackageName = @packageName");
                }
                queryStringBuilder.Append("))");
            }

            // Limit to top 50
            if (!fetchAllPages)
            {
                queryStringBuilder.Append("OFFSET 0 LIMIT 50");
            }

            var allReviews = new List<ReviewModel>();
            var queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@language", language)
                .WithParameter("@packageName", packageName)
                .WithParameter("@filterType", filterType)
                .WithParameter("@isClosed", isClosed);

            var itemQueryIterator = _reviewsContainer.GetItemQueryIterator<ReviewModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                allReviews.AddRange(result.Resource);
            }

            return allReviews.OrderByDescending(r => r.LastUpdated);
        }

        public async Task<IEnumerable<ReviewModel>> GetReviewsAsync(string serviceName, string packageDisplayName, IEnumerable<ReviewType> filterTypes = null)
        {
            var queryStringBuilder = new StringBuilder("SELECT * FROM Reviews r WHERE r.IsClosed = false");
            queryStringBuilder.Append(" AND r.ServiceName = @serviceName");
            queryStringBuilder.Append(" AND r.PackageDisplayName = @packageDisplayName");
            if (filterTypes != null && filterTypes.Count() > 0)
            {
                var filterTypesAsInts = filterTypes.Cast<int>().ToList();
                var filterTypeAsQueryStr = ArrayToQueryString<int>(filterTypesAsInts);
                queryStringBuilder.Append($" AND r.FilterType IN {filterTypeAsQueryStr} ");
            }

            var reviews = new List<ReviewModel>();
            var queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@serviceName", serviceName)
                .WithParameter("@packageDisplayName", packageDisplayName);

            var itemQueryIterator = _reviewsContainer.GetItemQueryIterator<ReviewModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                reviews.AddRange(result.Resource);
            }
            return reviews.OrderBy(r => r.Name).ThenByDescending(r => r.LastUpdated);
        }

        public async Task<IEnumerable<string>> GetReviewFirstLevelPropertiesAsync(string propertyName)
        {
            var query = $"SELECT DISTINCT VALUE r.{propertyName} FROM Reviews r";
            var properties = new List<string>();

            QueryDefinition queryDefinition = new QueryDefinition(query);
            using FeedIterator<string> feedIterator = _reviewsContainer.GetItemQueryIterator<string>(queryDefinition);

            while (feedIterator.HasMoreResults)
            {
                FeedResponse<string> response = await feedIterator.ReadNextAsync();
                properties.AddRange(response);
            }
            return properties;
        }

        public async Task<(IEnumerable<ReviewModel> Reviews, int TotalCount)> GetReviewsAsync(
            List<string> search, List<string> languages, bool? isClosed, List<int> filterTypes, bool? isApproved, int offset, int limit, string orderBy)
        {
            (IEnumerable<ReviewModel> Reviews, int TotalCount) result = (Reviews: new List<ReviewModel>(), TotalCount: 0);

            // Build up Query
            var queryStringBuilder = new StringBuilder("SELECT * FROM Reviews r");
            queryStringBuilder.Append(" WHERE IS_DEFINED(r.id)"); // Allows for appending the other query parts as AND's in any order

            if (search != null && search.Count > 0)
            {
                var searchAsQueryStr = ArrayToQueryString<string>(search);
                var searchAsSingleString = '"' + String.Join(' ', search) + '"';

                var hasExactMatchQuery = search.Any(
                    s => (
                    s.StartsWith("package:") || 
                    s.StartsWith("pr:") || 
                    s.StartsWith("author:") || 
                    s.StartsWith("service:") ||
                    s.StartsWith("name:")
                ));

                if (hasExactMatchQuery)
                {
                    foreach (var item in search)
                    {
                        if (item.StartsWith("package:"))
                        {
                            var query = '"' + $"{item.Replace("package:", "")}" + '"';
                            queryStringBuilder.Append($" AND STRINGEQUALS(ARRAY_SLICE(r.Revisions, -1)[0].Files[0].PackageName, {query}, true)");
                        }
                        else if (item.StartsWith("author:"))
                        {
                            var query = '"' + $"{item.Replace("author:", "")}" + '"';
                            queryStringBuilder.Append($" AND STRINGEQUALS(r.Author, {query}, true)");
                        }
                        else if (item.StartsWith("service:"))
                        {
                            var query = '"' + $"{item.Replace("service:", "")}" + '"';
                            queryStringBuilder.Append($" AND STRINGEQUALS(r.ServiceName, {query}, true)");
                        }
                        else if (item.StartsWith("pr:"))
                        {
                            var query = '"' + $"{item.Replace("pr:", "")}" + '"';
                            queryStringBuilder.Append($" AND ENDSWITH(ARRAY_SLICE(r.Revisions, -1)[0].Label, {query}, true)");
                        }
                        else if (item.StartsWith("name:"))
                        {
                            var query = '"' + $"{item.Replace("name:", "")}" + '"';
                            queryStringBuilder.Append($" AND CONTAINS(ARRAY_SLICE(r.Revisions, -1)[0].Name, {query}, true)");
                        }
                        else
                        {
                            var query = '"' + $"{item}" + '"';
                            queryStringBuilder.Append($" AND CONTAINS(ARRAY_SLICE(r.Revisions, -1)[0].Name, {query}, true)");
                        }
                    }
                }
                else
                {
                    queryStringBuilder.Append($" AND (r.Author IN {searchAsQueryStr}");
                    foreach (var item in search) 
                    {
                        var query = '"' + $"{item}" + '"';
                        queryStringBuilder.Append($" OR CONTAINS(ARRAY_SLICE(r.Revisions, -1)[0].Name, {query}, true)");
                        queryStringBuilder.Append($" OR CONTAINS(r.Name, {query}, true)");
                        queryStringBuilder.Append($" OR CONTAINS(r.ServiceName, {query}, true)");
                        queryStringBuilder.Append($" OR CONTAINS(r.PackageDisplayName, {query}, true)");
                    }
                    queryStringBuilder.Append($")");
                }
            }

            if (languages != null && languages.Count > 0)
            {
                var languagesAsQueryStr = ArrayToQueryString<string>(languages);
                queryStringBuilder.Append($" AND r.Revisions[0].Files[0].Language IN {languagesAsQueryStr}");
            }

            if (isClosed != null)
            {
                queryStringBuilder.Append(" AND r.IsClosed = @isClosed");
            }

            if (filterTypes != null && filterTypes.Count > 0)
            {
                var filterTypeAsQueryStr = ArrayToQueryString<int>(filterTypes);
                queryStringBuilder.Append($" AND r.FilterType IN {filterTypeAsQueryStr}");
            }

            if (isApproved != null)
            {
                queryStringBuilder.Append(" AND ARRAY_SLICE(r.Revisions, -1)[0].IsApproved = @isApproved");
            }

            // First get the total count to help with paging
            var countQuery = $"SELECT VALUE COUNT(1) FROM({queryStringBuilder.ToString()})";
            QueryDefinition countQueryDefinition = new QueryDefinition(countQuery)
                .WithParameter("@isClosed", isClosed)
                .WithParameter("@isApproved", isApproved);

            using FeedIterator<int> countFeedIterator = _reviewsContainer.GetItemQueryIterator<int>(countQueryDefinition);
            while (countFeedIterator.HasMoreResults)
            {
                result.TotalCount = (await countFeedIterator.ReadNextAsync()).SingleOrDefault();
            }

            queryStringBuilder.Append($" ORDER BY r.{orderBy} DESC");
            queryStringBuilder.Append(" OFFSET @offset LIMIT @limit");

            var reviews = new List<ReviewModel>();
            QueryDefinition queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@isClosed", isClosed)
                .WithParameter("@isApproved", isApproved)
                .WithParameter("@offset", offset)
                .WithParameter("@limit", limit);

            using FeedIterator<ReviewModel> feedIterator = _reviewsContainer.GetItemQueryIterator<ReviewModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ReviewModel> response = await feedIterator.ReadNextAsync();
                reviews.AddRange(response);
            }
            result.Reviews = reviews;
            return result;
        }

        private static string ArrayToQueryString<T>(IList<T> items)
        {
            var result = new StringBuilder();
            result.Append("(");
            foreach (var item in items)
            {
                if (item is int)
                {
                    result.Append($"{item},");
                }
                else
                {
                    result.Append($"\"{item}\",");
                }
                
            }
            result.Remove(result.Length - 1, 1);
            result.Append(")");
            return result.ToString();
        }
    }
}
