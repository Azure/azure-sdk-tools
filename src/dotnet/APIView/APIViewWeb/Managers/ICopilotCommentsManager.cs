using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface ICopilotCommentsManager
    {
        public Task<string> CreateDocumentAsync(string language, string badCode, string goodCode, string comment, string guidelineIds, string user);
        public Task<string> UpdateDocumentAsync(string id, string language, string badCode, string goodCode, string comment, string guidelineIds, string user);
        public Task<string> GetDocumentAsync(string id, string language);
        public Task DeleteDocumentAsync(string id, string language, string user);
        public Task<string> SearchDocumentsAsync(string language, string badCode, float threshold, int limit);
    }
}
