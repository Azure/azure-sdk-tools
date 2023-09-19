using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories
{
    public interface ICosmosReviewRevisionsRepository
    {
        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query
        /// Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public Task<PagedList<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(PageParams pageParams, ReviewRevisionsFilterAndSortParams filterAndSortParams);

        /// <summary>
        /// Retrieve Revisions with a specific ReviewId
        /// </summary>
        /// <param name="reviewId"></param> reviewId
        /// <returns></returns>
        public Task<IEnumerable<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(string reviewId);
    }
}
