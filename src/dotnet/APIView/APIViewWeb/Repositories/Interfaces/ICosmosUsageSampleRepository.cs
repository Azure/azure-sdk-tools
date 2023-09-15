using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Repositories
{
    public interface ICosmosUsageSampleRepository
    {
        public Task<List<UsageSampleModel>> GetUsageSampleAsync(string reviewId);
        public Task DeleteUsageSampleAsync(UsageSampleModel Sample);
        public Task UpsertUsageSampleAsync(UsageSampleModel sampleModel);
    }
}
