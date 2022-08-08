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

namespace APIViewWeb
{
    public class CosmosUsageSampleRepository
    {
        private readonly Container _samplesContainer;

        public CosmosUsageSampleRepository(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _samplesContainer = client.GetContainer("APIView", "usagesamples");
        }

        public async Task<UsageSampleModel> GetUsageSampleAsync(string sampleId)
        {
            return await _samplesContainer.ReadItemAsync<UsageSampleModel>(sampleId, new PartitionKey(sampleId));
        }
        
        public async Task DeleteUsageSampleAsync(UsageSampleModel Sample)
        {
            await _samplesContainer.DeleteItemAsync<UsageSampleModel>(Sample.SampleId, new PartitionKey(Sample.SampleId));
        }
        
        public async Task UpsertUsageSampleAsync(UsageSampleModel sampleModel)
        {
            await _samplesContainer.UpsertItemAsync(sampleModel, new PartitionKey(sampleModel.SampleId));
        }

    }
}
