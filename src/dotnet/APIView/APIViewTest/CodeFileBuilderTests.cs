using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ApiView;
using Azure.ClientSdk.Analyzers.Tests;
using Xunit;

namespace APIViewTest
{
    public class CodeFileBuilderTests
    {
        private Regex _stripRegex = new Regex(@"/\*-\*/(.*?)/\*-\*/");

        public static IEnumerable<object[]> ExactFormattingFiles
        {
            get
            {
                var assembly = typeof(CodeFileBuilderTests).Assembly;
                return assembly.GetManifestResourceNames()
                    .Where(r => r.Contains("ExactFormatting"))
                    .Select(r => new object [] { r } )
                    .ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(ExactFormattingFiles))]
        public async Task TestMethod(string name)
        {
            var manifestResourceStream = typeof(CodeFileBuilderTests).Assembly.GetManifestResourceStream(name);
            var streamReader = new StreamReader(manifestResourceStream);
            var code = streamReader.ReadToEnd().Trim(' ', '\t', '\r', '\n');
            var formatted = _stripRegex.Replace(code, string.Empty);
            await AssertFormattingAsync(code, formatted);
        }

        public static async Task AssertFormattingAsync(string code, string formatted)
        {
            var project = DiagnosticProject.Create(typeof(CodeFileBuilderTests).Assembly, new[] { code });
            var compilation = await project.GetCompilationAsync();
            var codeModel = new CodeFileBuilder().Build(compilation.Assembly, false);
            var formattedModel = new CodeFileRenderer().Render(codeModel);
            var formattedString = formattedModel.ToString();
            Assert.Equal(formatted, formattedString);
        }
    }
}
