using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories
{
    public interface ICosmosSamplesRevisionsRepository
    {
        public Task<SamplesRevisionModel> GetSamplesRevisionAsync(string reviewId, string sampleId);
        public Task<IEnumerable<SamplesRevisionModel>> GetSamplesRevisionsAsync(string reviewId);
        public Task<PagedList<SamplesRevisionModel>> GetSamplesRevisionsAsync(ClaimsPrincipal user, PageParams pageParams, FilterAndSortParams filterAndSortParams);
        public Task UpsertSamplesRevisionAsync(SamplesRevisionModel sampleModel);
    }
}
