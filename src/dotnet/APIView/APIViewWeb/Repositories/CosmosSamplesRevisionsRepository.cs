// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using APIViewWeb.LeanModels;
using APIViewWeb.Helpers;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace APIViewWeb
{
    public class CosmosSamplesRevisionsRepository : ICosmosSamplesRevisionsRepository
    {
        private readonly Container _samplesRevisionsContainer;

        public CosmosSamplesRevisionsRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _samplesRevisionsContainer = cosmosClient.GetContainer(configuration["CosmosDBName"], "SamplesRevisions");
        }

        public async Task<IEnumerable<SamplesRevisionModel>> GetSamplesRevisionsAsync(string reviewId)
        {
            return await GetSamplesRevisionsFromQueryAsync($"SELECT * FROM UsageSamples c WHERE c.ReviewId = '{reviewId}' AND c.IsDeleted = false");
        }

        public async Task<PagedList<SamplesRevisionModel>> GetSamplesRevisionsAsync(ClaimsPrincipal user, PageParams pageParams, FilterAndSortParams filterAndSortParams)
        {
            var queryStringBuilder = new StringBuilder(@"SELECT * FROM Revisions c");
            queryStringBuilder.Append($" WHERE c.IsDeleted = {filterAndSortParams.IsDeleted.ToString().ToLower()}");

            if (!string.IsNullOrEmpty(filterAndSortParams.ReviewId))
            {
                queryStringBuilder.Append($" AND c.ReviewId = '{filterAndSortParams.ReviewId}'");
            }

            if (!string.IsNullOrEmpty(filterAndSortParams.Title))
            {
                var query = '"' + $"{filterAndSortParams.Title}" + '"';
                queryStringBuilder.Append($" AND CONTAINS(c.Title, {query}, true)");
            }

            if (!string.IsNullOrEmpty(filterAndSortParams.Author))
            {
                queryStringBuilder.Append($" AND CONTAINS(c.CreatedBy, '{filterAndSortParams.Author}')");
            }

            int totalCount = 0;
            var countQuery = $"SELECT VALUE COUNT(1) FROM({queryStringBuilder})";
            QueryDefinition countQueryDefinition = new QueryDefinition(countQuery);
            using FeedIterator<int> countFeedIterator = _samplesRevisionsContainer.GetItemQueryIterator<int>(countQueryDefinition);
            while (countFeedIterator.HasMoreResults)
            {
                totalCount = (await countFeedIterator.ReadNextAsync()).SingleOrDefault();
            }

            switch (filterAndSortParams.SortField)
            {
                case "createdOn":
                    queryStringBuilder.Append($" ORDER BY c.CreatedOn");
                    break;
                default:
                    queryStringBuilder.Append($" ORDER BY c.createdOn");
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
            var revisions = new List<SamplesRevisionModel>();
            QueryDefinition queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@offset", pageParams.NoOfItemsRead)
                .WithParameter("@limit", pageParams.PageSize)
                .WithParameter("@sortField", filterAndSortParams.SortField);

            using FeedIterator<SamplesRevisionModel> feedIterator = _samplesRevisionsContainer.GetItemQueryIterator<SamplesRevisionModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<SamplesRevisionModel> response = await feedIterator.ReadNextAsync();
                revisions.AddRange(response);
            }
            var noOfItemsRead = pageParams.NoOfItemsRead + revisions.Count();
            return new PagedList<SamplesRevisionModel>((IEnumerable<SamplesRevisionModel>)revisions, noOfItemsRead, totalCount, pageParams.PageSize);
        }

        public async Task<SamplesRevisionModel> GetSamplesRevisionAsync(string reviewId, string sampleId)
        {
            return await _samplesRevisionsContainer.ReadItemAsync<SamplesRevisionModel>(sampleId, new PartitionKey(reviewId));
        }

        public async Task UpsertSamplesRevisionAsync(SamplesRevisionModel samplesRevision)
        {
            await _samplesRevisionsContainer.UpsertItemAsync(samplesRevision, new PartitionKey(samplesRevision.ReviewId));
        }

        private async Task<IEnumerable<SamplesRevisionModel>> GetSamplesRevisionsFromQueryAsync(string query)
        {
            var itemQueryIterator = _samplesRevisionsContainer.GetItemQueryIterator<SamplesRevisionModel>(query);
            List<SamplesRevisionModel> samplesRevisions = new List<SamplesRevisionModel>();
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                samplesRevisions.AddRange(result.Resource);
            }
            return samplesRevisions;
        }

    }
}
