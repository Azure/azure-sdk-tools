using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public interface ILanguageService
    {
        string Name { get; }
        bool IsSupportedExtension(string extension);
        bool CanUpdate(string versionString);
        Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis);
    }
}
