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
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;


namespace APIViewWeb
{
    public class CosmosAPIRevisionsRepository : ICosmosAPIRevisionsRepository
    {
        private readonly Container _apiRevisionContainer;

        public CosmosAPIRevisionsRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _apiRevisionContainer = cosmosClient.GetContainer("APIViewV2", "APIRevisions");
        }

        /// <summary>
        /// Add new Revisionto database
        /// </summary>
        /// <param name="revision"></param>
        /// <returns></returns>
        public async Task UpsertAPIRevisionAsync(APIRevisionListItemModel revision)
        {
            revision.LastUpdatedOn = DateTime.UtcNow;
            await _apiRevisionContainer.UpsertItemAsync(revision, new PartitionKey(revision.ReviewId));
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query
        /// Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public async Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(PageParams pageParams, APIRevisionsFilterAndSortParams filterAndSortParams)
        {
            var queryStringBuilder = new StringBuilder(@"SELECT * FROM Revisions c");
            queryStringBuilder.Append(" WHERE c.IsDeleted = false");

            if (!string.IsNullOrEmpty(filterAndSortParams.ReviewId))
            {
                queryStringBuilder.Append($" AND c.ReviewId = '{filterAndSortParams.ReviewId}'");
            }   

            if (!string.IsNullOrEmpty(filterAndSortParams.Name))
            {
                var hasExactMatchQuery = filterAndSortParams.Name.StartsWith("package:") ||
                    filterAndSortParams.Name.StartsWith("pr:");

                if (hasExactMatchQuery)
                {
                    if (filterAndSortParams.Name.StartsWith("package:"))
                    {
                        var query = '"' + $"{filterAndSortParams.Name.Replace("package:", "")}" + '"';
                        queryStringBuilder.Append($" AND STRINGEQUALS(c.PackageName, {query}, true)");
                    }
                    else if (filterAndSortParams.Name.StartsWith("pr:"))
                    {
                        var query = '"' + $"{filterAndSortParams.Name.Replace("pr:", "")}" + '"';
                        queryStringBuilder.Append($" AND ENDSWITH(c.Label, {query}, true)");
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
                    queryStringBuilder.Append($" OR CONTAINS(c.Label, {query}, true)");
                    queryStringBuilder.Append($")");
                }
            }

            if (!string.IsNullOrEmpty(filterAndSortParams.Author))
            {
                queryStringBuilder.Append($" AND STRINGEQUALS(c.ChangeHistory[0].User, '{filterAndSortParams.Author}')");
            }

            if (filterAndSortParams.Languages != null && filterAndSortParams.Languages.Count() > 0)
            {
                var languagesAsQueryStr = CosmosQueryHelpers.ArrayToQueryString<string>(filterAndSortParams.Languages);
                queryStringBuilder.Append($" AND c.Language IN {languagesAsQueryStr}");
            }

            if (filterAndSortParams.Details != null && filterAndSortParams.Details.Count() > 0)
            {
                foreach (var item in filterAndSortParams.Details)
                {
                    switch (item)
                    {
                        case "Approved":
                            queryStringBuilder.Append($" AND c.Status = Approved");
                            break;
                        case "Pending":
                            queryStringBuilder.Append($" AND c.Status = Pending");
                            break;
                        case "Manual":
                            queryStringBuilder.Append($" AND c.ReviewRevisionType = Manual");
                            break;
                        case "Automatic":
                            queryStringBuilder.Append($" AND c.ReviewRevisionType = Automatic");
                            break;
                        case "PullRequest":
                            queryStringBuilder.Append($" AND c.ReviewRevisionType = PullRequest");
                            break;
                    }
                }
            }

            int totalCount = 0;
            var countQuery = $"SELECT VALUE COUNT(1) FROM({queryStringBuilder})";
            QueryDefinition countQueryDefinition = new QueryDefinition(countQuery);
            using FeedIterator<int> countFeedIterator = _apiRevisionContainer.GetItemQueryIterator<int>(countQueryDefinition);
            while (countFeedIterator.HasMoreResults)
            {
                totalCount = (await countFeedIterator.ReadNextAsync()).SingleOrDefault();
            }

            switch (filterAndSortParams.SortField)
            {
                case "name":
                    queryStringBuilder.Append($" ORDER BY c.PackageName");
                    break;
                default:
                    queryStringBuilder.Append($" ORDER BY c.PackageName");
                    break;
            }

            if (filterAndSortParams.SortOrder == 1)
            {
                queryStringBuilder.Append(" DESC");
            }
            else
            {
                queryStringBuilder.Append(" ASC");
            }

            queryStringBuilder.Append(" OFFSET @offset LIMIT @limit");
            var revisions = new List<APIRevisionListItemModel>();
            QueryDefinition queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@offset", pageParams.NoOfItemsRead)
                .WithParameter("@limit", pageParams.PageSize)
                .WithParameter("@sortField", filterAndSortParams.SortField);

            using FeedIterator<APIRevisionListItemModel> feedIterator = _apiRevisionContainer.GetItemQueryIterator<APIRevisionListItemModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<APIRevisionListItemModel> response = await feedIterator.ReadNextAsync();
                revisions.AddRange(response);
            }
            var noOfItemsRead = pageParams.NoOfItemsRead + revisions.Count();
            return new PagedList<APIRevisionListItemModel>((IEnumerable<APIRevisionListItemModel>)revisions, noOfItemsRead, totalCount, pageParams.PageSize);
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb for a given reviewId
        /// </summary>
        /// <param name="reviewId"></param> The reviewId
        /// <returns></returns>
        public async Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(string reviewId)
        {
            var query = $"SELECT * FROM Revisions c WHERE c.IsDeleted = false AND c.ReviewId = '{reviewId}'";
            var revisions = new List<APIRevisionListItemModel>();
            QueryDefinition queryDefinition = new QueryDefinition(query);
            using FeedIterator<APIRevisionListItemModel> feedIterator = _apiRevisionContainer.GetItemQueryIterator<APIRevisionListItemModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<APIRevisionListItemModel> response = await feedIterator.ReadNextAsync();
                revisions.AddRange(response);
            }
            return revisions;
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb
        /// </summary>
        /// <param name="revisionId"></param> The revisionId
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> GetAPIRevisionAsync(string revisionId)
        {
            var query = $"SELECT * FROM Revisions c WHERE c.id = '{revisionId}'";
            QueryDefinition queryDefinition = new QueryDefinition(query);
            using FeedIterator<APIRevisionListItemModel> feedIterator = _apiRevisionContainer.GetItemQueryIterator<APIRevisionListItemModel>(queryDefinition);
            try
            {
                FeedResponse<APIRevisionListItemModel> response = await feedIterator.ReadNextAsync();
                return response.Single();
            }
            catch (Exception)
            {
                return default(APIRevisionListItemModel);
            }
        }

        /// <summary>
        /// Get Revisions by LastUpdatedOn Date
        /// </summary>
        /// <param name="lastUpdatedOn"></param>
        /// <param name="apiRevisionType"></param>
        /// <returns></returns>
        public async Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(DateTime lastUpdatedOn, APIRevisionType apiRevisionType = APIRevisionType.All)
        {
            var queryStringBuilder = new StringBuilder($"SELECT * FROM Revisions c WHERE c.LastUpdatedOn < '{lastUpdatedOn.ToString("yyyy-MM-dd")}'");
            if (apiRevisionType != APIRevisionType.All)
            {
                queryStringBuilder.Append(" AND c.APIRevisionType = @apiRevisionType");
            }

            var revisions = new List<APIRevisionListItemModel>();
            QueryDefinition queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@apiRevisionType", apiRevisionType.ToString());

            using FeedIterator<APIRevisionListItemModel> feedIterator = _apiRevisionContainer.GetItemQueryIterator<APIRevisionListItemModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<APIRevisionListItemModel> response = await feedIterator.ReadNextAsync();
                revisions.AddRange(response);
            }
            return revisions;
        }
    }
}
