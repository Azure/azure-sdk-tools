// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;


namespace APIViewWeb
{
    public class CosmosReviewRevisionsRepository : ICosmosReviewRevisionsRepository
    {
        private readonly Container _reviewRevisionContainer;

        public CosmosReviewRevisionsRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _reviewRevisionContainer = cosmosClient.GetContainer("APIViewV2", "Revisions");
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query
        /// Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public async Task<PagedList<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(PageParams pageParams, ReviewRevisionsFilterAndSortParams filterAndSortParams)
        {
            
        }

        /// <summary>
        /// Retrieve Revisions with a specific ReviewId
        /// </summary>
        /// <param name="reviewId"></param> reviewId
        /// <returns></returns>
        public async Task<IEnumerable<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(string reviewId)
        {
            
        }

    }
}
