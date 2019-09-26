using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public interface ILanguageService
    {
        bool IsSupportedExtension(string extension);
        bool CanUpdate(CodeFile codeFile);
        Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis);
    }
}
