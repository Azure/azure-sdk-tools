using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Retrieve Revisions for a particular Review from the Revisions container in CosmosDb
        /// </summary>
        /// <param name="reviewId"></param> The Reviewid for which the revisions are to be retrieved
        /// <returns></returns>

        public async Task<IEnumerable<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(string reviewId)
        {
            return await _reviewsRevisionsRepository.GetReviewRevisionsAsync(reviewId);
        }

        /// <summary>
        /// Retrieve the latest Revisions for a particular Review from the Revisions container in CosmosDb
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="reviewRevision"></param> The list of revisions can be supplied if available to avoid another call to the database
        /// <returns></returns>

        public async Task<ReviewRevisionListItemModel> GetLatestReviewRevisionsAsync(string reviewId, IEnumerable<ReviewRevisionListItemModel> reviewRevision = null)
        {
            var revisions = (reviewRevision == null) ? await _reviewsRevisionsRepository.GetReviewRevisionsAsync(reviewId) : reviewRevision;
            return revisions.OrderBy(
                x => x.ChangeHistory.Where(y => y.ChangeAction == ReviewRevisionChangeAction.Created).ToList().First().ChangeDateTime
                ).ToList().First();
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb.
        /// </summary>
        /// <param name="revisionId"></param> The RevisionId for which the revision is to be retrieved
        /// <returns></returns>

        public async Task<ReviewRevisionListItemModel> GetReviewRevisionAsync(string revisionId)
        {
            return await _reviewsRevisionsRepository.GetReviewRevisionAsync(revisionId);
        }
    }
}
