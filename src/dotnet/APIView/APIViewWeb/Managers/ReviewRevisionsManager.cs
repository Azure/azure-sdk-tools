using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public class ReviewRevisionsManager : IReviewRevisionsManager
    {
        private readonly ICosmosReviewRevisionsRepository _reviewsRevisionsRepository;

        public ReviewRevisionsManager(ICosmosReviewRevisionsRepository reviewsRevisionsRepository)
        {
            _reviewsRevisionsRepository = reviewsRevisionsRepository;
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query.
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>

        public async Task<PagedList<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(PageParams pageParams, ReviewRevisionsFilterAndSortParams filterAndSortParams)
        {
             return await _reviewsRevisionsRepository.GetReviewRevisionsAsync(pageParams, filterAndSortParams);
        }
    }
}
