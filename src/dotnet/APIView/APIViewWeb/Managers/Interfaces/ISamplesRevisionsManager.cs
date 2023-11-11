using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers
{
    public interface ISamplesRevisionsManager
    {
        public Task<IEnumerable<SamplesRevisionModel>> GetSamplesRevisionsAsync(string reviewId);
        public Task<string> GetSamplesRevisionContentAsync(string fileId);
        public Task<SamplesRevisionModel> UpsertSamplesRevisionsAsync(ClaimsPrincipal user, string reviewId, string sample, string revisionTitle, string FileName = null);
        public Task<SamplesRevisionModel> UpsertSamplesRevisionsAsync(ClaimsPrincipal user, string reviewId, Stream fileStream, string revisionTitle, string FileName);
        public Task DeleteSamplesRevisionAsync(ClaimsPrincipal user, string reviewId, string sampleId);
    }
}
