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
            var requests = await GetPullRequestFromQueryAsync(query);
            return requests.Count > 0 ? requests[0] : null;
        }

        public async Task UpsertPullRequestAsync(PullRequestModel pullRequestModel)
        {
            await _pullRequestsContainer.UpsertItemAsync(pullRequestModel, new PartitionKey(pullRequestModel.PullRequestNumber));
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsAsync(bool isOpen)
        {
            var query = $"SELECT * FROM PullRequests c WHERE c.IsOpen = {(isOpen? "true": "false")}";
            return await GetPullRequestFromQueryAsync(query);
        }

        private async Task<List<PullRequestModel>> GetPullRequestFromQueryAsync(string query)
        {
            var allRequests = new List<PullRequestModel>();
            var itemQueryIterator = _pullRequestsContainer.GetItemQueryIterator<PullRequestModel>(query);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                allRequests.AddRange(result.Resource);
            }
            return allRequests;
        }
    }
}