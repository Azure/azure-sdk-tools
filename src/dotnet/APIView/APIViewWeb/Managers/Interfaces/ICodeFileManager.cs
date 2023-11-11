using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb.Managers.Interfaces
{
    public interface ICodeFileManager
    {
        public Task<CodeFile> GetCodeFileAsync(string repoName, string buildId, string artifactName, string packageName, string originalFileName, string codeFileName,
            MemoryStream originalFileStream, string baselineCodeFileName = "", MemoryStream baselineStream = null, string project = "public");
        public Task<APICodeFileModel> CreateCodeFileAsync(string revisionId, string originalName, Stream fileStream, bool runAnalysis, string language);
        public Task<CodeFile> CreateCodeFileAsync(string originalName, Stream fileStream, bool runAnalysis, MemoryStream memoryStream, string language = null);
        public Task<APICodeFileModel> CreateReviewCodeFileModel(string revisionId, MemoryStream memoryStream, CodeFile codeFile);

    }
}
