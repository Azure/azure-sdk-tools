using System.IO;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;

namespace APIViewWeb.Managers.Interfaces
{
    public interface ICodeFileManager
    {
        public Task<CodeFile> GetCodeFileAsync(string repoName, string buildId, string artifactName, string packageName, string originalFileName, string codeFileName,
            MemoryStream originalFileStream, string baselineCodeFileName = "", MemoryStream baselineStream = null, string project = "public");
        public Task<APICodeFileModel> CreateCodeFileAsync(string apiRevisionId, string originalName, bool runAnalysis, Stream fileStream = null, string language = null);
        public Task<CodeFile> CreateCodeFileAsync(string originalName, bool runAnalysis, MemoryStream memoryStream, Stream fileStream = null, string language = null);
        public Task<APICodeFileModel> CreateReviewCodeFileModel(string apiRevisionId, MemoryStream memoryStream, CodeFile codeFile);
        public bool AreAPICodeFilesTheSame(RenderedCodeFile codeFileA, RenderedCodeFile codeFileB);
        public bool AreCodeFilesTheSame(CodeFile codeFileA, CodeFile codeFileB);
    }
}
