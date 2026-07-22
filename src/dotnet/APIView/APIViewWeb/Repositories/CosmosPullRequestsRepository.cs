// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosPullRequestsRepository : ICosmosPullRequestsRepository
    {
        private readonly Container _pullRequestsContainer;
        private ICosmosReviewRepository _reviewsRepository;

        public CosmosPullRequestsRepository(IConfiguration configuration, ICosmosReviewRepository reviewsRepository, CosmosClient cosmosClient)
        {
            _pullRequestsContainer = cosmosClient.GetContainer(configuration["CosmosDBName"], "PullRequests");
            _reviewsRepository = reviewsRepository;
        }

        public async Task<PullRequestModel> GetPullRequestAsync(int pullRequestNumber, string repoName, string packageName, string language = null)
        {
            var queryBuilder = new StringBuilder("SELECT * FROM PullRequests c WHERE c.PullRequestNumber = @pullRequestNumber AND c.RepoName = @repoName AND c.PackageName = @packageName AND c.IsDeleted = false");
            if (language != null)
            {
                queryBuilder.Append(" AND IS_DEFINED(c.Language) AND c.Language = @language");
            }

            var queryDefinition = new QueryDefinition(queryBuilder.ToString())
                .WithParameter("@pullRequestNumber", pullRequestNumber)
                .WithParameter("@repoName", repoName)
                .WithParameter("@packageName", packageName);
            if (language != null)
            {
                queryDefinition = queryDefinition.WithParameter("@language", language);
            }

            var requests = await GetPullRequestFromQueryAsync(queryDefinition);
            return requests.Count > 0 ? requests[0] : null;
        }

        public async Task UpsertPullRequestAsync(PullRequestModel pullRequestModel)
        {
            await _pullRequestsContainer.UpsertItemAsync(pullRequestModel, new PartitionKey(pullRequestModel.ReviewId));
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsAsync(bool isOpen)
        {
            var queryDefinition = new QueryDefinition("SELECT * FROM PullRequests c WHERE c.IsOpen = @isOpen AND c.IsDeleted = false")
                .WithParameter("@isOpen", isOpen);
            return await GetPullRequestFromQueryAsync(queryDefinition);
        }

        public async Task<List<PullRequestModel>> GetPullRequestsAsync(int pullRequestNumber, string repoName)
        {
            var queryDefinition = new QueryDefinition("SELECT * FROM PullRequests c WHERE c.PullRequestNumber = @pullRequestNumber AND c.RepoName = @repoName AND c.IsDeleted = false")
                .WithParameter("@pullRequestNumber", pullRequestNumber)
                .WithParameter("@repoName", repoName);
            return await GetPullRequestFromQueryAsync(queryDefinition);
        }

        public async Task<IEnumerable<PullRequestModel>> GetPullRequestsAsync(string reviewId, string apiRevisionId = null)
        {
            var queryBuilder = new StringBuilder("SELECT * FROM PullRequests c WHERE c.ReviewId = @reviewId AND c.IsDeleted = false");
            if (!string.IsNullOrEmpty(apiRevisionId))
            {
                queryBuilder.Append(" AND c.APIRevisionId = @apiRevisionId");
            }

            var queryDefinition = new QueryDefinition(queryBuilder.ToString())
                .WithParameter("@reviewId", reviewId);
            if (!string.IsNullOrEmpty(apiRevisionId))
            {
                queryDefinition = queryDefinition.WithParameter("@apiRevisionId", apiRevisionId);
            }

            return await GetPullRequestFromQueryAsync(queryDefinition);
        }

        private async Task<List<PullRequestModel>> GetPullRequestFromQueryAsync(QueryDefinition queryDefinition)
        {
            var allRequests = new List<PullRequestModel>();
            var itemQueryIterator = _pullRequestsContainer.GetItemQueryIterator<PullRequestModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                allRequests.AddRange(result.Resource);
            }

            // Cosmos doesn't allow cross join of two containers so we need to filter closed API reviews
            var filtered = new List<PullRequestModel>();
            Dictionary<string, List<PullRequestModel>> kvp = new Dictionary<string, List<PullRequestModel>>();
            foreach (var pr in allRequests)
            {
                if (!string.IsNullOrEmpty(pr.ReviewId))
                {
                    if (kvp.ContainsKey(pr.ReviewId))
                    {
                        kvp[pr.ReviewId].Add(pr);
                    }
                    else
                    {
                        kvp.Add(pr.ReviewId, new List<PullRequestModel> { pr });    
                    }
                }
            }

            if (kvp.Any())
            {
                var reviews = await _reviewsRepository.GetReviewsAsync(reviewIds: new List<string>(kvp.Keys), isClosed: false);
                var reviewIds = reviews.Select(r => r.Id).ToList();

                foreach (var kv in kvp)
                {
                    if (reviewIds.Contains(kv.Key))
                    {
                        filtered.AddRange(kv.Value);
                    }
                }
            }
            return filtered;
        }
    }
}
