using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public abstract class LanguageService
    {
        public abstract string Name { get; }
        public abstract string Extension { get; }
        public virtual bool IsSupportedFile(string name) => name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
        public abstract bool CanUpdate(string versionString);
        public abstract Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis);
    }
}
