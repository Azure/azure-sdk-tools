using System.IO;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface IBlobOriginalsRepository
    {
        public string GetContainerUrl();
        public Task<Stream> GetOriginalAsync(string codeFileId);
        public Task UploadOriginalAsync(string codeFileId, Stream stream);
        public Task DeleteOriginalAsync(string codeFileId);
    }
}
