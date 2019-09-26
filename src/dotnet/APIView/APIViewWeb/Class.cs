using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Task<CodeFile> GetCodeFileAsync(Stream stream, bool runAnalysis);
    }

    public class JavaLanguageService : ILanguageService
    {
        public bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".json", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        public bool CanUpdate(CodeFile codeFile)
        {
            return false;
        }

        public async Task<CodeFile> GetCodeFileAsync(Stream stream, bool runAnalysis)
        {
            try
            {

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            var process = new ProcessStartInfo("java", "");
        }
    }

    public class JsonLanguageService : ILanguageService
    {
        public bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".json", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        public bool CanUpdate(CodeFile codeFile)
        {
            return false;
        }

        public async Task<CodeFile> GetCodeFileAsync(Stream stream, bool runAnalysis)
        {
            return await CodeFile.DeserializeAsync(stream);
        }
    }

    public class CSharpLanguageService : ILanguageService
    {
        public bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".dll", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        public bool CanUpdate(CodeFile codeFile)
        {
            return codeFile.Version != CodeFileBuilder.CurrentVersion;
        }

        public Task<CodeFile> GetCodeFileAsync(Stream stream, bool runAnalysis)
        {
            return Task.FromResult(CodeFileBuilder.Build(stream, runAnalysis));
        }
    }
}
