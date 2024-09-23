using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
}
