using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers
{
    public interface ISamplesRevisionsManager
    {
        public Task<SamplesRevisionModel> GetSamplesRevisionAsync(string reviewId, string samplesRevisionId);
        public Task<IEnumerable<SamplesRevisionModel>> GetSamplesRevisionsAsync(string reviewId);
        public Task<SamplesRevisionModel> GetLatestSampleRevisionsAsync(string reviewId);
        public Task<PagedList<SamplesRevisionModel>> GetSamplesRevisionsAsync(ClaimsPrincipal user, PageParams pageParams, FilterAndSortParams filterAndSortParams);
        public Task<string> GetSamplesRevisionContentAsync(string fileId);
        public Task<SamplesRevisionModel> UpsertSamplesRevisionsAsync(ClaimsPrincipal user, string reviewId, string sample, string revisionTitle, string FileName = null);
        public Task<SamplesRevisionModel> UpsertSamplesRevisionsAsync(ClaimsPrincipal user, string reviewId, Stream fileStream, string revisionTitle, string FileName);
        public Task UpdateSamplesRevisionAsync(ClaimsPrincipal user, string reviewId, string sampleRevisionId, string newContent, string newTitle);
        public Task UpdateSamplesRevisionTitle(string reviewId, string sampleId, string newTitle);
        public Task DeleteSamplesRevisionAsync(ClaimsPrincipal user, string reviewId, string sampleId);
    }
}
