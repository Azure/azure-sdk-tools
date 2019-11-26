﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosCommentsRepository
    {
        private readonly Container _commentsContainer;

        public CosmosCommentsRepository(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _commentsContainer = client.GetContainer("APIView", "Comments");
        }

        public async Task<IEnumerable<CommentModel>> GetCommentsAsync(string reviewId)
        {
            return await GetCommentsFromQueryAsync($"SELECT * FROM Comments c WHERE c.ReviewId = '{reviewId}'");
        }

        public async Task UpsertCommentAsync(CommentModel commentModel)
        {
            await _commentsContainer.UpsertItemAsync(commentModel, new PartitionKey(commentModel.ReviewId));
        }

        public async Task DeleteCommentAsync(CommentModel commentModel)
        {
            await _commentsContainer.DeleteItemAsync<CommentModel>(commentModel.CommentId, new PartitionKey(commentModel.ReviewId));
        }

        public async Task DeleteCommentsAsync(string reviewId)
        {
            foreach (var commentModel in await GetCommentsAsync(reviewId))
            {
                await DeleteCommentAsync(commentModel);
            }
        }

        public async Task<CommentModel> GetCommentAsync(string reviewId, string commentId)
        {
            return await _commentsContainer.ReadItemAsync<CommentModel>(commentId, new PartitionKey(reviewId));
        }

        public async Task<IEnumerable<CommentModel>> GetCommentsAsync(string reviewId, string lineId)
        {
            return await GetCommentsFromQueryAsync($"SELECT * FROM Comments c WHERE c.ReviewId = '{reviewId}' AND c.ElementId = '{lineId}'");
        }

        private async Task<IEnumerable<CommentModel>> GetCommentsFromQueryAsync(string query)
        {
            var allReviews = new List<CommentModel>();
            var itemQueryIterator = _commentsContainer.GetItemQueryIterator<CommentModel>(query);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                allReviews.AddRange(result.Resource);
            }

            return allReviews;
        }

    }
}