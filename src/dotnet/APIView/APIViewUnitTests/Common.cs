using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb;
using APIViewWeb.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Xunit;

namespace APIViewUnitTests
{
    internal static class Common
    {
        public static async Task BuildDllAsync(Stream stream, string code)
        {
            var project = DiagnosticProject.Create(typeof(CodeFileBuilderTests).Assembly, LanguageVersion.Latest, new[] { code })
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.Internal ));

            var compilation = await project.GetCompilationAsync();
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity > DiagnosticSeverity.Warning));

            compilation.Emit(stream);
        }
    }

    internal class MockLanguageService : LanguageService
    {
        private readonly string _name;
        private readonly bool _usesTreeStyleParser;

        public MockLanguageService(string name, bool usesTreeStyleParser = false)
        {
            _name = name;
            _usesTreeStyleParser = usesTreeStyleParser;
        }

        public override string Name => _name;
        public override string[] Extensions => new[] { ".json" };
        public override string VersionString => "1.0";
        public override bool CanUpdate(string versionString) => false;
        public override Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null) => Task.FromResult<CodeFile>(null);
        public override bool UsesTreeStyleParser => _usesTreeStyleParser;
        public override CodeFile GetReviewGenPendingCodeFile(string fileName) => null;
        public override bool GeneratePipelineRunParams(APIRevisionGenerationPipelineParamModel param) => false;
        public override bool CanConvert(string versionString) => false;
    }
}
