// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
            _apiRevisionContainer = cosmosClient.GetContainer(configuration["CosmosDBName"], "APIRevisions");
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
        /// <param name="user"></param>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public async Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(ClaimsPrincipal user, PageParams pageParams, FilterAndSortParams filterAndSortParams)
        {
            var queryStringBuilder = new StringBuilder(@"SELECT * FROM Revisions c");
            queryStringBuilder.Append($" WHERE c.IsDeleted = {filterAndSortParams.IsDeleted.ToString().ToLower()}");

            if (!string.IsNullOrEmpty(filterAndSortParams.ReviewId))
            {
                queryStringBuilder.Append($" AND c.ReviewId = '{filterAndSortParams.ReviewId}'");
            }   

            if (!string.IsNullOrEmpty(filterAndSortParams.Label))
            {
                var query = '"' + $"{filterAndSortParams.Label }" + '"';
                queryStringBuilder.Append($" AND CONTAINS(c.Label, {query}, true)");
            }

            if (!string.IsNullOrEmpty(filterAndSortParams.Author))
            {
                queryStringBuilder.Append($" AND CONTAINS(c.CreatedBy, '{filterAndSortParams.Author}')");
            }

            if (filterAndSortParams.AssignedToMe)
            {
                queryStringBuilder.Append($" AND ARRAY_CONTAINS(c.AssignedReviewers, {{ 'AssingedTo': '{ user.GetGitHubLogin() }' }}, true)");
            }

            if (filterAndSortParams.WithTreeStyleTokens)
            {
                queryStringBuilder.Append(" AND c.Files[0].ParserStyle = 'Tree'");
            }

            if (filterAndSortParams.Details != null && filterAndSortParams.Details.Count() > 0)
            {
                queryStringBuilder.Append(" AND (");

                var approvalFilters = filterAndSortParams.Details.Where(x => x == "Approved" || x == "Pending").ToList();
                var apiRevisionTypeFilters = filterAndSortParams.Details.Where(x => x == "Manual" || x == "Automatic" || x == "PullRequest").ToList();

                if (approvalFilters.Count() == 2)
                {
                    queryStringBuilder.Append($"c.IsApproved = true OR c.IsApproved = false");
                }
                else if (approvalFilters.Contains("Approved"))
                {
                    queryStringBuilder.Append($"c.IsApproved = true");
                }
                else if (approvalFilters.Contains("Pending"))
                {
                    queryStringBuilder.Append($"c.IsApproved = false");
                }

                if (approvalFilters.Count > 0 && apiRevisionTypeFilters.Count() > 0)
                    queryStringBuilder.Append(" AND ");

                foreach (var item in apiRevisionTypeFilters)
                {
                    switch (item)
                    {
                        case "Manual":
                            queryStringBuilder.Append($"c.APIRevisionType = 'Manual'");
                            break;
                        case "Automatic":
                            queryStringBuilder.Append($"c.APIRevisionType = 'Automatic'");
                            break;
                        case "PullRequest":
                            queryStringBuilder.Append($"c.APIRevisionType = 'PullRequest'");
                            break;
                    }
                    if (item != apiRevisionTypeFilters.Last())
                    {
                        queryStringBuilder.Append(" OR ");
                    }
                }
                queryStringBuilder.Append(")");
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
                case "createdOn":
                    queryStringBuilder.Append($" ORDER BY c.CreatedOn");
                    break;
                case "lastUpdatedOn":
                    queryStringBuilder.Append($" ORDER BY c.LastUpdatedOn");
                    break;
                default:
                    queryStringBuilder.Append($" ORDER BY c.LastUpdatedOn");
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

        /// <summary>
        /// Get APIRevisions assigned to a user for review
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public async Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAssignedToUser(string userName)
        {
            var query = "SELECT * FROM Revisions r WHERE r.IsDeleted = false and ARRAY_CONTAINS(r.AssignedReviewers, { 'AssingedTo': '" + userName + "' }, true)";

            var apiRevisions = new List<APIRevisionListItemModel>();
            var queryDefinition = new QueryDefinition(query).WithParameter("@userName", userName);
            var itemQueryIterator = _apiRevisionContainer.GetItemQueryIterator<APIRevisionListItemModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                apiRevisions.AddRange(result.Resource);
            }

            return apiRevisions.OrderByDescending(r => r.LastUpdatedOn);
        }

        /// <summary>
        /// Get ReviewIds for review that are linked by crossLanguagePackageId
        /// </summary>
        /// <param name="crossLanguagePackageId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<string>> GetReviewIdsOfLanguageCorrespondingReviewAsync(string crossLanguagePackageId)
        {
            var query = $"SELECT DISTINCT VALUE c.ReviewId FROM c WHERE ARRAY_LENGTH(c.Files) > 0 AND c.Files[0].CrossLanguagePackageId = '{crossLanguagePackageId}'";

            var reviewIds = new List<string>();
            var queryDefinition = new QueryDefinition(query);
            var itemQueryIterator = _apiRevisionContainer.GetItemQueryIterator<string>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                reviewIds.AddRange(result.Resource);
            }
            return reviewIds;
        }
    }
}
