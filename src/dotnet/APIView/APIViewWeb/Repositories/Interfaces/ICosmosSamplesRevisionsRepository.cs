using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Repositories
{
    public interface ICosmosSamplesRevisionsRepository
    {
        public Task<SamplesRevisionModel> GetSamplesRevisionAsync(string reviewId, string sampleId);
        public Task<IEnumerable<SamplesRevisionModel>> GetSamplesRevisionsAsync(string reviewId);
        public Task UpsertSamplesRevisionAsync(SamplesRevisionModel sampleModel);
    }
}
