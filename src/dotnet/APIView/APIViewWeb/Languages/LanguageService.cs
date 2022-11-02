using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ApiView;
using APIView;

namespace APIViewWeb
{
    public abstract class LanguageService
    {
        public abstract string Name { get; }
        public abstract string Extension { get; }
        public virtual bool IsSupportedFile(string name) => name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
        public abstract bool CanUpdate(string versionString);
        public abstract Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis);
        public virtual bool IsReviewGenByPipeline { get; set; } = false;

        public readonly CodeFileToken ReviewNotReadyCodeFile = new CodeFileToken("API review is being generated for this revision and it will be available in few minutes. Please refresh this page after few minutes to see generated API review.", CodeFileTokenKind.Literal);
        public virtual CodeFile GetReviewGenPendingCodeFile(string fileName) => new CodeFile()
        {
            Name = fileName,
            PackageName = fileName,
            Language = Name,
            Tokens = new CodeFileToken[] {new CodeFileToken("", CodeFileTokenKind.Newline), ReviewNotReadyCodeFile, new CodeFileToken("", CodeFileTokenKind.Newline) },
            Navigation = new NavigationItem[] { new NavigationItem() { Text = fileName } }
        };
    }
}
