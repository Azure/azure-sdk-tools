using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories
{
    public interface ICosmosAPIRevisionsRepository
    {
        /// <summary>
        /// Add new ReviewRevision
        /// </summary>
        /// <param name="revision"></param>
        /// <returns></returns>
        public Task UpsertAPIRevisionAsync(APIRevisionListItemModel revision);

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query
        /// Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(PageParams pageParams, APIRevisionsFilterAndSortParams filterAndSortParams);

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb for a given reviewId
        /// </summary>
        /// <param name="reviewId"></param> The reviewId
        /// <returns></returns>
        public Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(string reviewId);

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb
        /// </summary>
        /// <param name="revisionId"></param> The revisionId
        /// <returns></returns>
        public Task<APIRevisionListItemModel> GetReviewRevisionAsync(string revisionId);
    }
}
