// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosCommentsRepository : ICosmosCommentsRepository
    {
        private readonly Container _commentsContainer;

        public CosmosCommentsRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _commentsContainer = cosmosClient.GetContainer("APIViewV2", "Comments");
        }

        public async Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId)
        {
            return await GetCommentsFromQueryAsync($"SELECT * FROM Comments c WHERE c.ReviewId = '{reviewId}' AND c.IsDeleted = false");
        }

        public async Task UpsertCommentAsync(CommentItemModel commentModel)
        {
            await _commentsContainer.UpsertItemAsync(commentModel, new PartitionKey(commentModel.ReviewId));
        }

        public async Task<CommentItemModel> GetCommentAsync(string reviewId, string commentId)
        {
            return await _commentsContainer.ReadItemAsync<CommentItemModel>(commentId, new PartitionKey(reviewId));
        }

        public async Task<IEnumerable<CommentItemModel>> GetCommentsAsync(string reviewId, string lineId)
        {
            return await GetCommentsFromQueryAsync($"SELECT * FROM Comments c WHERE c.ReviewId = '{reviewId}' AND c.ElementId = '{lineId}' AND c.IsDeleted = false");
        }

        private async Task<IEnumerable<CommentItemModel>> GetCommentsFromQueryAsync(string query)
        {
            var comments = new List<CommentItemModel>();
            var itemQueryIterator = _commentsContainer.GetItemQueryIterator<CommentItemModel>(query);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                comments.AddRange(result.Resource);
            }

            return comments;
        }

    }
}
