// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using ColorCode;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;


namespace APIViewWeb
{
    public class CosmosReviewRepository : ICosmosReviewRepository
    {
        private readonly Container _reviewsContainer;
        private readonly Container _reviewContainerNew;

        public CosmosReviewRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _reviewsContainer = cosmosClient.GetContainer("APIView", "Reviews");
            _reviewContainerNew = cosmosClient.GetContainer("APIViewV2", "Reviews");
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

        public async Task<IEnumerable<ReviewModel>> GetRequestedReviews(string userName)
        {
            var query = $"SELECT * FROM Reviews r WHERE IS_DEFINED(r.RequestedReviewers) AND ARRAY_CONTAINS(r.RequestedReviewers, @userName)";
            var allReviews = new List<ReviewModel>();
            var queryDefinition = new QueryDefinition(query).WithParameter("@userName", userName);
            var itemQueryIterator = _reviewsContainer.GetItemQueryIterator<ReviewModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                allReviews.AddRange(result.Resource);
            }

            return allReviews.OrderByDescending(r => r.LastUpdated);
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
            IEnumerable<string> search, IEnumerable<string> languages, bool? isClosed, IEnumerable<int> filterTypes, bool? isApproved, int offset, int limit, string orderBy)
        {
            (IEnumerable<ReviewModel> Reviews, int TotalCount) result = (Reviews: new List<ReviewModel>(), TotalCount: 0);

            // Build up Query
            var queryStringBuilder = new StringBuilder("SELECT * FROM Reviews r");
            queryStringBuilder.Append(" WHERE IS_DEFINED(r.id)"); // Allows for appending the other query parts as AND's in any order

            if (search != null && search.Any())
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
                        queryStringBuilder.Append($" OR CONTAINS(ARRAY_SLICE(r.Revisions, -1)[0].Label, {query}, true)");
                    }
                    queryStringBuilder.Append($")");
                }
            }

            if (languages != null && languages.Any())
            {
                var languagesAsQueryStr = ArrayToQueryString<string>(languages);
                queryStringBuilder.Append($" AND r.Revisions[0].Files[0].Language IN {languagesAsQueryStr}");
            }

            if (isClosed != null)
            {
                queryStringBuilder.Append(" AND r.IsClosed = @isClosed");
            }

            if (filterTypes != null && filterTypes.Any())
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

        /// <summary>
        /// Retrieve Reviews from the Reviews container in CosmosDb after applying filter to the query
        /// Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public async Task<PagedList<ReviewListItemModel>> GetReviewsAsync(PageParams pageParams, ReviewFilterAndSortParams filterAndSortParams)
        {
            var queryStringBuilder = new StringBuilder(@"
SELECT VALUE {
    Id: c.id,
    PackageName: c.PackageName,
    PackageDisplayName: c.PackageDisplayName,
    ServiceName: c.ServiceName,
    Language: c.Language,
    ReviewRevisions: c.ReviewRevisions,
    Subscribers: c.Subscribers,
    ChangeHistory: c.ChangeHistory,
    State: c.State,
    Status: c.Status,
    IsDeleted: c.IsDeleted
} FROM Reviews c");
            queryStringBuilder.Append(" WHERE c.IsDeleted = false");

            if (!string.IsNullOrEmpty(filterAndSortParams.Name)){
                var hasExactMatchQuery = filterAndSortParams.Name.StartsWith("package:") ||
                    filterAndSortParams.Name.StartsWith("service:");

                if (hasExactMatchQuery)
                {
                    if (filterAndSortParams.Name.StartsWith("package:"))
                    {
                        var query = '"' + $"{filterAndSortParams.Name.Replace("package:", "")}" + '"';
                        queryStringBuilder.Append($" AND STRINGEQUALS(c.PackageName, {query}, true)");
                    }
                    else if (filterAndSortParams.Name.StartsWith("service:"))
                    {
                        var query = '"' + $"{filterAndSortParams.Name.Replace("service:", "")}" + '"';
                        queryStringBuilder.Append($" AND STRINGEQUALS(c.ServiceName, {query}, true)");
                    }
                    else
                    {
                        var query = '"' + $"{filterAndSortParams.Name}" + '"';
                        queryStringBuilder.Append($" AND CONTAINS(c.PackageName, {query}, true)");
                    }
                }
                else
                {
                    var query = '"' + $"{filterAndSortParams.Name}" + '"';
                    queryStringBuilder.Append($" AND (CONTAINS(c.PackageName, {query}, true)");
                    queryStringBuilder.Append($" OR CONTAINS(c.PackageDisplayName, {query}, true)");
                    queryStringBuilder.Append($" OR CONTAINS(c.ServiceName, {query}, true)");
                    queryStringBuilder.Append($")");
                }
            }

            if (filterAndSortParams.Languages != null && filterAndSortParams.Languages.Count() > 0) 
            {
                var languagesAsQueryStr = ArrayToQueryString<string>(filterAndSortParams.Languages);
                queryStringBuilder.Append($" AND c.Language IN {languagesAsQueryStr}");
            }

            if (filterAndSortParams.Details != null && filterAndSortParams.Details.Count() > 0)
            {
                foreach (var item in filterAndSortParams.Details)
                {
                    switch (item)
                    {
                        case "Open":
                                queryStringBuilder.Append($" AND c.State = Open");
                            break;
                        case "Closed":
                                queryStringBuilder.Append($" AND c.State = Closed");
                            break;
                        case "Pending":
                            queryStringBuilder.Append($" AND c.Status = Pending");
                            break;
                        case "Approved":
                            queryStringBuilder.Append($" AND c.Status = Approved");
                            break;
                    }
                }
            }

            int totalCount = 0;
            var countQuery = $"SELECT VALUE COUNT(1) FROM({queryStringBuilder})";
            QueryDefinition countQueryDefinition = new QueryDefinition(countQuery);
            using FeedIterator<int> countFeedIterator = _reviewContainerNew.GetItemQueryIterator<int>(countQueryDefinition);
            while (countFeedIterator.HasMoreResults)
            {
                totalCount = (await countFeedIterator.ReadNextAsync()).SingleOrDefault();
            }

            switch (filterAndSortParams.SortField)
            {
                case "name":
                    queryStringBuilder.Append($" ORDER BY c.PackageName");
                    break;
                case "noOfRevisions":
                    queryStringBuilder.Append($" ORDER BY c.cp_NumberOfReviewRevisions");
                    break;
                default:
                    queryStringBuilder.Append($" ORDER BY c.PackageName");
                    break;
            }

            if(filterAndSortParams.SortOrder == 1)
            {
                queryStringBuilder.Append(" DESC");
            }
            else 
            {
                queryStringBuilder.Append(" ASC");
            }

            queryStringBuilder.Append(" OFFSET @offset LIMIT @limit");
            var reviews = new List<ReviewListItemModel>();
            QueryDefinition queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@offset", pageParams.NoOfItemsRead)
                .WithParameter("@limit", pageParams.PageSize)
                .WithParameter("@sortField", filterAndSortParams.SortField);

            using FeedIterator<ReviewListItemModel> feedIterator = _reviewContainerNew.GetItemQueryIterator<ReviewListItemModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ReviewListItemModel> response = await feedIterator.ReadNextAsync();
                reviews.AddRange(response);
            }
            var noOfItemsRead = pageParams.NoOfItemsRead + reviews.Count();
            return new PagedList<ReviewListItemModel>((IEnumerable<ReviewListItemModel>)reviews, noOfItemsRead, totalCount, pageParams.PageSize);
        }

        public async Task<IEnumerable<ReviewModel>> GetApprovedForFirstReleaseReviews(string language, string packageName)
        {
            var query = $"SELECT * FROM Reviews r WHERE r.IsClosed = false AND IS_DEFINED(r.IsApprovedForFirstRelease) AND r.IsApprovedForFirstRelease = true AND " +
                        $"EXISTS (SELECT VALUE revision FROM revision in r.Revisions WHERE EXISTS (SELECT VALUE files from files in revision.Files WHERE files.Language = @language AND files.PackageName = @packageName))";
            var allReviews = new List<ReviewModel>();
            var queryDefinition = new QueryDefinition(query).WithParameter("@packageName", packageName).WithParameter("@language", language);
            var itemQueryIterator = _reviewsContainer.GetItemQueryIterator<ReviewModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                allReviews.AddRange(result.Resource);
            }

            return allReviews;
        }

        public async Task<IEnumerable<ReviewModel>> GetApprovedReviews(string language, string packageName)
        {
            var query = $"SELECT * FROM Reviews r WHERE  EXISTS (SELECT VALUE revision FROM revision in r.Revisions WHERE revision.IsApproved = true AND EXISTS (SELECT VALUE files from files in revision.Files WHERE files.Language = @language AND files.PackageName = @packageName))";
            var allReviews = new List<ReviewModel>();
            var queryDefinition = new QueryDefinition(query).WithParameter("@packageName", packageName).WithParameter("@language", language);
            var itemQueryIterator = _reviewsContainer.GetItemQueryIterator<ReviewModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                allReviews.AddRange(result.Resource);
            }

            return allReviews;
        }

        public async Task<IEnumerable<RevisionListItemModel>> GetRevisionsAsync(string reviewId) 
        {
            var revisions = new List<RevisionListItemModel>();
            var query = @"
SELECT VALUE { 
    Id : rv.id,
    Name : rv.Name,
    CreationDate : rv.CreationDate,
    Files : rv.Files
}
FROM r
JOIN rv IN r.Revisions
WHERE r.id = @reviewId";
            var queryDefinition = new QueryDefinition(query).WithParameter("@reviewId", reviewId);
            var itemQueryIterator = _reviewsContainer.GetItemQueryIterator<RevisionListItemModel>(queryDefinition);
            using FeedIterator<RevisionListItemModel> feedIterator = _reviewsContainer.GetItemQueryIterator<RevisionListItemModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<RevisionListItemModel> response = await feedIterator.ReadNextAsync();
                revisions.AddRange(response);
            }
            return revisions;
        }

        private static string ArrayToQueryString<T>(IEnumerable<T> items)
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
