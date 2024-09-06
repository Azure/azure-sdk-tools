using ApiView;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using Octokit;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface IBlobCodeFileRepository
    {
        public Task<RenderedCodeFile> GetCodeFileAsync(APIRevisionListItemModel revision, bool updateCache = true);
        public Task<RenderedCodeFile> GetCodeFileAsync(string revisionId, APICodeFileModel apiCodeFile, string language, bool updateCache = true);
        public Task<CodeFile> GetCodeFileWithCompressionAsync(string revisionId, string codeFileId);
        public Task UpsertCodeFileAsync(string revisionId, string codeFileId, CodeFile codeFile);
        public Task DeleteCodeFileAsync(string revisionId, string codeFileId);
    }
}
