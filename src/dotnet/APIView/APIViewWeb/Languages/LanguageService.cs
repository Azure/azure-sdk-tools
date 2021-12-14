using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIViewWeb.Models;

namespace APIViewWeb
{
    public abstract class LanguageService
    {
        public abstract string Name { get; }
        public abstract string Extension { get; }
        public virtual bool IsSupportedFile(string name) => name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
        public abstract bool CanUpdate(string versionString);
        public abstract Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis);
        public virtual bool IsReviewGenByPipeline { get; } = false;

        public readonly CodeFileToken ReviewNotReadyCodeFile = new CodeFileToken("API review is being generated now and it will be available here in few minutes", CodeFileTokenKind.Text);
        public virtual CodeFile GetReviewGenPendingCodeFile(string fileName) => new CodeFile()
        {
            Name = Name,
            PackageName = fileName,
            Language = Name,
            Tokens = new CodeFileToken[] {ReviewNotReadyCodeFile}
        };
    }
}
