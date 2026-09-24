using System.IO;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface IBlobUsageSampleRepository
    {
        public Task<Stream> GetUsageSampleAsync(string sampleFileId);
        public Task UploadUsageSampleAsync(string sampleFileId, Stream stream);
        public Task DeleteUsageSampleAsync(string sampleFileId);
    }
}
