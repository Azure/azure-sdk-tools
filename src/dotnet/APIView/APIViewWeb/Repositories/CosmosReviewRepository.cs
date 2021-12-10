// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosReviewRepository
    {
        private readonly Container _reviewsContainer;

        public CosmosReviewRepository(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _reviewsContainer = client.GetContainer("APIView", "Reviews");
        }

        public async Task UpsertReviewAsync(ReviewModel reviewModel)
        {
            await _reviewsContainer.UpsertItemAsync(reviewModel, new PartitionKey(reviewModel.ReviewId));
        }

        public async Task DeleteReviewAsync(ReviewModel reviewModel)
        {
            await _reviewsContainer.DeleteItemAsync<ReviewModel>(reviewModel.ReviewId, new PartitionKey(reviewModel.ReviewId));
        }

        public async Task<ReviewModel> GetReviewAsync(string reviewId)
        {
            return await _reviewsContainer.ReadItemAsync<ReviewModel>(reviewId, new PartitionKey(reviewId));
        }

        public async Task<ReviewModel> GetMasterReviewForPackageAsync(string language, string packageName)
        {
            var reviews = await GetReviewsAsync(false, language, packageName, ReviewType.Automatic);
            return reviews.FirstOrDefault();
        }

        public async Task<IEnumerable<ReviewModel>> GetReviewsAsync(bool isClosed, string language, string packageName = null, ReviewType? filterType = null)
        {
            var queryStringBuilder = new StringBuilder("SELECT * FROM Reviews r WHERE (IS_DEFINED(r.IsClosed) ? r.IsClosed : false) = @isClosed ");

            //Add filter if looking for automatic, manual or PR reviews
            if (filterType != null)
            {
               queryStringBuilder.Append("AND (IS_DEFINED(r.FilterType) ? r.FilterType : 0) = @filterType ");
            }

            // Add language and package name clause
            // Currently we don't have any use case of searching for a package name across languages
            if (language != "All")
            {
                queryStringBuilder.Append("AND EXISTS (SELECT VALUE revision FROM revision in r.Revisions WHERE EXISTS (SELECT VALUE files from files in revision.Files WHERE files.Language = @language");
                if (!String.IsNullOrEmpty(packageName))
                {
                    queryStringBuilder.Append(" AND files.PackageName = @packageName");
                }
                queryStringBuilder.Append("))");
            }

            var allReviews = new List<ReviewModel>();
            var queryDefinition = new QueryDefinition(queryStringBuilder.ToString())
                .WithParameter("@language", language)
                .WithParameter("@packageName", packageName)
                .WithParameter("@filterType", filterType)
                .WithParameter("@isClosed", isClosed);

            var itemQueryIterator = _reviewsContainer.GetItemQueryIterator<ReviewModel>(queryDefinition);
            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                allReviews.AddRange(result.Resource);
            }
            return allReviews.OrderByDescending(r => r.LastUpdated);
        }
    }
}
