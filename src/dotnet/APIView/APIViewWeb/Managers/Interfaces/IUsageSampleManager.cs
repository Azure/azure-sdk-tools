using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface IUsageSampleManager
    {
        public Task<List<UsageSampleModel>> GetReviewUsageSampleAsync(string reviewId);
        public Task<string> GetUsageSampleContentAsync(string fileId);
        public Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, string sample, int revisionNum, string revisionTitle, string FileName = null);
        public Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, Stream fileStream, int revisionNum, string revisionTitle, string FileName);
        public Task DeleteUsageSampleAsync(ClaimsPrincipal user, string reviewId, string FileId, string sampleId);
    }
}
