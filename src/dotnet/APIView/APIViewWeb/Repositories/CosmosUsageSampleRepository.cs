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

namespace APIViewWeb
{
    public class CosmosUsageSampleRepository
    {
        private readonly Container _samplesContainer;

        public CosmosUsageSampleRepository(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _samplesContainer = client.GetContainer("APIView", "UsageSamples");
        }

        public async Task<UsageSampleModel> GetUsageSampleAsync(string reviewId)
        {
            return await GetUsageSamplesFromQueryAsync($"SELECT * FROM UsageSamples c WHERE c.ReviewId = '{reviewId}'", reviewId);
        }
        
        public async Task DeleteUsageSampleAsync(UsageSampleModel Sample)
        {
            await _samplesContainer.DeleteItemAsync<UsageSampleModel>(Sample.SampleId, new PartitionKey(Sample.ReviewId));
        }
        
        public async Task UpsertUsageSampleAsync(UsageSampleModel sampleModel)
        {
            await _samplesContainer.UpsertItemAsync(sampleModel, new PartitionKey(sampleModel.ReviewId));
        }

        private async Task<UsageSampleModel> GetUsageSamplesFromQueryAsync(string query, string reviewId)
        {
            var itemQueryIterator = _samplesContainer.GetItemQueryIterator<UsageSampleModel>(query);
            var result = await itemQueryIterator.ReadNextAsync();
            try
            {
                return result.Resource.First();
            }
            catch
            {
                return new UsageSampleModel(null, reviewId);
            }
        }

    }
}
