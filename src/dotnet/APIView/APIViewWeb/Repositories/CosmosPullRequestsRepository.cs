// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosPullRequestsRepository
    {
        private readonly Container _pullRequestsContainer;

        public CosmosPullRequestsRepository(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _pullRequestsContainer = client.GetContainer("APIView", "PullRequests");
        }

        public async Task<PullRequestModel> GetPullRequestAsync(int pullRequestNumber, string repoName, string packageName)
        {
            var query = $"SELECT * FROM PullRequests c WHERE c.PullRequestNumber = {pullRequestNumber} and c.RepoName = '{repoName}' and c.PackageName = '{packageName}'";
            return await GetPullRequestFromQueryAsync(query);
        }

        public async Task UpsertPullRequestAsync(PullRequestModel pullRequestModel)
        {
            await _pullRequestsContainer.UpsertItemAsync(pullRequestModel, new PartitionKey(pullRequestModel.PullRequestNumber));
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsAsync(bool isOpen)
        {
            var pullRequests = new List<PullRequestModel>();
            var queryDefinition = new QueryDefinition("SELECT * FROM PullRequests c WHERE c.IsOpen = @isOpen").WithParameter("@isClosed", isOpen);
            var itemQueryIterator = _pullRequestsContainer.GetItemQueryIterator<PullRequestModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                pullRequests.AddRange(result.Resource);
            }
            return pullRequests;
        }

        private async Task<PullRequestModel> GetPullRequestFromQueryAsync(string query)
        {
            var itemQueryIterator = _pullRequestsContainer.GetItemQueryIterator<PullRequestModel>(query);
            if (itemQueryIterator.HasMoreResults)
            {
                var allRequests = new List<PullRequestModel>();
                var result = await itemQueryIterator.ReadNextAsync();
                allRequests.AddRange(result.Resource);
                if (allRequests.Count > 0)
                {
                   return allRequests[0];
                }
            }

            return null;
        }
    }
}