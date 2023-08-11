using System.Threading.Tasks;
using MongoDB.Driver;

namespace APIViewWeb.Managers
{
    public interface ICopilotCommentsManager
    {
        public Task<string> CreateDocumentAsync(string user, string badCode, string goodCode, string language, string comment, string[] guidelineIds);
        public Task<UpdateResult> UpdateDocumentAsync(string user, string id, string badCode, string goodCode, string language, string comment, string[] guidelineIds);
        public Task<string> GetDocumentAsync(string id);
        public Task DeleteDocumentAsync(string user, string id);
    }
}
