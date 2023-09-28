using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.Managers.Interfaces
{
    public interface IReviewRevisionsManager
    {
        public Task<PagedList<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(PageParams pageParams, ReviewRevisionsFilterAndSortParams filterAndSortParams);

        public Task<IEnumerable<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(string reviewId);

        public Task<ReviewRevisionListItemModel> GetLatestReviewRevisionsAsync(string reviewId, IEnumerable<ReviewRevisionListItemModel> reviewRevision = null);

        public Task<ReviewRevisionListItemModel> GetReviewRevisionAsync(string revisionId);
    }
}
