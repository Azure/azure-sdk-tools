// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using APIViewWeb.Models;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace APIViewWeb
{
    public class CosmosUsageSampleRepository : ICosmosUsageSampleRepository
    {
        private readonly Container _samplesContainer;

        public CosmosUsageSampleRepository(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _samplesContainer = client.GetContainer("APIView", "UsageSamples");
        }

        public async Task<List<UsageSampleModel>> GetUsageSampleAsync(string reviewId)
        {
            return await GetUsageSamplesFromQueryAsync($"SELECT * FROM UsageSamples c WHERE c.ReviewId = '{reviewId}'");
        }
        
        public async Task DeleteUsageSampleAsync(UsageSampleModel Sample)
        {
            await _samplesContainer.DeleteItemAsync<UsageSampleModel>(Sample.SampleId, new PartitionKey(Sample.ReviewId));
        }
        
        public async Task UpsertUsageSampleAsync(UsageSampleModel sampleModel)
        {
            await _samplesContainer.UpsertItemAsync(sampleModel, new PartitionKey(sampleModel.ReviewId));
        }

        private async Task<List<UsageSampleModel>> GetUsageSamplesFromQueryAsync(string query)
        {
            var itemQueryIterator = _samplesContainer.GetItemQueryIterator<UsageSampleModel>(query);
            List<UsageSampleModel> samples = new List<UsageSampleModel>();
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                samples.AddRange(result.Resource);
            }

            return samples;
        }

    }
}
