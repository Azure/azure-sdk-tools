// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using APIViewWeb.LeanModels;

namespace APIViewWeb
{
    public class CosmosSamplesRevisionsRepository : ICosmosSamplesRevisionsRepository
    {
        private readonly Container _samplesRevisionsContainer;

        public CosmosSamplesRevisionsRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _samplesRevisionsContainer = cosmosClient.GetContainer("APIViewV2", "SamplesRevisions");
        }

        public async Task<IEnumerable<SamplesRevisionModel>> GetSamplesRevisionsAsync(string reviewId)
        {
            return await GetSamplesRevisionsFromQueryAsync($"SELECT * FROM UsageSamples c WHERE c.ReviewId = '{reviewId}' AND c.IsDeleted = false");
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
