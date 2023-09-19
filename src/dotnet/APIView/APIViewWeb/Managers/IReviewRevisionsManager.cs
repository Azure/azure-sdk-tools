using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface IReviewRevisionsManager
    {
        public Task<PagedList<ReviewRevisionListItemModel>> GetReviewRevisionsAsync(PageParams pageParams, ReviewRevisionsFilterAndSortParams filterAndSortParams);
    }
}
