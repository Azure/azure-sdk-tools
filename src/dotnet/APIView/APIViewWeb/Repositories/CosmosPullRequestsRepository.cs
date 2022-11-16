// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosPullRequestsRepository
    {
        private readonly Container _pullRequestsContainer;
        private CosmosReviewRepository _reviewsRepository;

        public CosmosPullRequestsRepository(IConfiguration configuration, CosmosReviewRepository reviewsRepository)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _pullRequestsContainer = client.GetContainer("APIView", "PullRequests");
            _reviewsRepository = reviewsRepository;
        }

        public async Task<PullRequestModel> GetPullRequestAsync(int pullRequestNumber, string repoName, string packageName, string language = null)
        {
            var queryBuilder  =  new StringBuilder($"SELECT * FROM PullRequests c WHERE c.PullRequestNumber = {pullRequestNumber} and c.RepoName = '{repoName}' and c.PackageName = '{packageName}'");
            if (language != null)
            {
                queryBuilder.Append($" and IS_DEFINED(c.Language) and c.Language = '{language}'");

            }
            var requests = await GetPullRequestFromQueryAsync(queryBuilder.ToString());
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

            // Cosmos doesn't allow cross join of two containers so we need to filter closed API reviews
            var filtered = new List<PullRequestModel>();
            foreach(var pr in allRequests)
            {
                if(!await IsApiReviewClosed(pr.ReviewId))
                    filtered.Add(pr);
            }

            return filtered;
        }

        private async Task<bool> IsApiReviewClosed(string reviewId)
        {
            var review = await _reviewsRepository.GetReviewAsync(reviewId);
            return review?.IsClosed ?? true;
        }

        public async Task<List<PullRequestModel>> GetPullRequestsAsync(int pullRequestNumber, string repoName)
        {
            var query = $"SELECT * FROM PullRequests c WHERE c.PullRequestNumber = {pullRequestNumber} and c.RepoName = '{repoName}'";
            return await GetPullRequestFromQueryAsync(query);
        }
    }
}
