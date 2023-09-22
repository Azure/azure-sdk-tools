using ApiView;
using APIViewWeb.Models;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface IBlobCodeFileRepository
    {
        public Task<RenderedCodeFile> GetCodeFileAsync(ReviewRevisionModel revision, bool updateCache = true);
        public Task<RenderedCodeFile> GetCodeFileAsync(string revisionId, string codeFileId, bool updateCache = true);
        public Task UpsertCodeFileAsync(string revisionId, string codeFileId, CodeFile codeFile);
        public Task DeleteCodeFileAsync(string revisionId, string codeFileId);
    }
}
