using APIViewWeb.LeanModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface IRevisionManager
    {
        public Task<IEnumerable<RevisionListItemModel>> GetRevisionsAsync(string reviewId);
        public Task<RevisionListItemModel> GetRevisionsAsync(string reviewId, string revisionId);
        public Task<RevisionListItemModel> GetLatestRevisionsAsync(string reviewId);
    }
}
