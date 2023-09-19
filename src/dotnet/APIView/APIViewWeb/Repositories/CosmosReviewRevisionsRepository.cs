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
    public class CosmosReviewRevisionsRepository : ICosmosReviewRevisionsRepository
    {
        private readonly Container _reviewRevisionContainer;

        public CosmosReviewRevisionsRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _reviewRevisionContainer = cosmosClient.GetContainer("APIViewV2", "Revisions");
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query
        /// Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public async Task<PagedList<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(PageParams pageParams, ReviewRevisionsFilterAndSortParams filterAndSortParams)
        {
            var queryStringBuilder = new StringBuilder(@"SELECT * FROM Revisions c");
            queryStringBuilder.Append(" WHERE c.IsDeleted = false");

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
            using FeedIterator<int> countFeedIterator = _reviewRevisionContainer.GetItemQueryIterator<int>(countQueryDefinition);
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
            var reviews = new List<ReviewRevisionListItemModel>();
            QueryDefinition queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@offset", pageParams.NoOfItemsRead)
                .WithParameter("@limit", pageParams.PageSize)
                .WithParameter("@sortField", filterAndSortParams.SortField);

            using FeedIterator<ReviewRevisionListItemModel> feedIterator = _reviewRevisionContainer.GetItemQueryIterator<ReviewRevisionListItemModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ReviewRevisionListItemModel> response = await feedIterator.ReadNextAsync();
                reviews.AddRange(response);
            }
            var noOfItemsRead = pageParams.NoOfItemsRead + reviews.Count();
            return new PagedList<ReviewRevisionListItemModel>((IEnumerable<ReviewRevisionListItemModel>)reviews, noOfItemsRead, totalCount, pageParams.PageSize);
        }
    }
}
