using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories
{
    public interface ICosmosReviewRepository
    {
        public Task UpsertReviewAsync(ReviewListItemModel reviewModel);
        public Task<ReviewListItemModel> GetReviewAsync(string reviewId);
        public Task<LegacyReviewModel> GetLegacyReviewAsync(string reviewId);
        public Task<ReviewListItemModel> GetReviewAsync(string language, string packageName, bool? isClosed = false);
        public Task<IEnumerable<ReviewListItemModel>> GetReviewsAsync(string language, bool? isClosed = false);
        public Task<IEnumerable<ReviewListItemModel>> GetReviewsAssignedToUser(string userName);
        public Task<(IEnumerable<ReviewListItemModel> Reviews, int TotalCount)> GetReviewsAsync(
            IEnumerable<string> search, IEnumerable<string> languages, bool? isClosed, bool? isApproved, int offset, int limit, string orderBy);
    }
}
