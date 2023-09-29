using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories
{
    public interface ICosmosReviewRepository
    {
        public Task UpsertReviewAsync(ReviewModel reviewModel);
        public Task DeleteReviewAsync(ReviewModel reviewModel);
        public Task<ReviewModel> GetReviewAsync(string reviewId);
        public Task<ReviewModel> GetMasterReviewForPackageAsync(string language, string packageName);
        public Task<IEnumerable<ReviewModel>> GetReviewsAsync(bool isClosed, string language, string packageName = null, ReviewType? filterType = null, bool fetchAllPages = false);
        public Task<IEnumerable<ReviewModel>> GetReviewsAsync(string serviceName, string packageDisplayName, IEnumerable<ReviewType> filterTypes = null);
        public Task<IEnumerable<ReviewModel>> GetRequestedReviews(string userName);
        public Task<IEnumerable<string>> GetReviewFirstLevelPropertiesAsync(string propertyName);
        public Task<(IEnumerable<ReviewModel> Reviews, int TotalCount)> GetReviewsAsync(
            IEnumerable<string> search, IEnumerable<string> languages, bool? isClosed, IEnumerable<int> filterTypes, bool? isApproved, int offset, int limit, string orderBy);
        public Task<IEnumerable<ReviewModel>> GetApprovedForFirstReleaseReviews(string language, string packageName);
        public Task<IEnumerable<ReviewModel>> GetApprovedReviews(string language, string packageName);

        /// <summary>
        /// Retrieve Reviews from the Reviews container in CosmosDb after applying filter to the query
        /// Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public Task<PagedList<ReviewListItemModel>> GetReviewsAsync(PageParams pageParams, ReviewFilterAndSortParams filterAndSortParams);

        /// <summary>
        /// Retrieve Reviews from the Reviews container in CosmosDb using only the revewId
        /// </summary>
        /// <param name="reviewId"></param> Contains paginationinfo
        /// <returns></returns>
        public Task<ReviewListItemModel> GetReviewNewAsync(string reviewId);
    }
}
