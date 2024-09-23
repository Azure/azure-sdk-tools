using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ApiView;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace APIViewUnitTests
{
    public class CodeFileBuilderTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public CodeFileBuilderTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        private Regex _stripRegex = new Regex(@"/\*-\*/(.*?)/\*-\*/", RegexOptions.Singleline);
        private Regex _retainRegex = new Regex(@"/\*@(.*?)@\*/", RegexOptions.Singleline);

        public static IEnumerable<object[]> FormattingFiles(string folder)
        {
                var assembly = typeof(CodeFileBuilderTests).Assembly;
                return assembly.GetManifestResourceNames()
                    .Where(r => r.Contains(folder))
                    .Select(r => new object[] { r })
                    .ToArray();
        }

        [Theory]
        [MemberData(nameof(FormattingFiles), new object[] { "ExactFormatting" })]
        public async Task VerifyFormatted(string name)
        {
            ExtractCodeAndFormat(name, out string code, out string formatted);
            await AssertFormattingAsync(code, formatted);
        }

        [Theory]
        [MemberData(nameof(FormattingFiles), new object[] { "InternalsVisibleTo" })]
        public async Task VerifyFormattedWithInternalVisibleTo(string name)
        {
            ExtractCodeAndFormat(name, out string code, out string formatted);
            formatted = $"Exposes internals to:{Environment.NewLine}Azure.Some.Client{Environment.NewLine}{Environment.NewLine}" + formatted;
            await AssertFormattingAsync(code, formatted);
        }

        private void ExtractCodeAndFormat(string name, out string code, out string formatted)
        {
            var manifestResourceStream = typeof(CodeFileBuilderTests).Assembly.GetManifestResourceStream(name);
            var streamReader = new StreamReader(manifestResourceStream);
            code = streamReader.ReadToEnd();
            code = code.Trim(' ', '\t', '\r', '\n');
            formatted = _stripRegex.Replace(code, string.Empty);
            formatted = _retainRegex.Replace(formatted, "$1");
            formatted = RemoveEmptyLines(formatted);
            formatted = formatted.Trim(' ', '\t', '\r', '\n');
        }

        private async Task AssertFormattingAsync(string code, string formatted)
        {
            using var memoryStream = new MemoryStream();

            await Common.BuildDllAsync(memoryStream, code);
            memoryStream.Position = 0;

            var compilationFromDll = CompilationFactory.GetCompilation(memoryStream, null);
            var codeModel = new CodeFileBuilder()
            {
                SymbolOrderProvider = new NameSymbolOrderProvider()
            }.Build(compilationFromDll, false, null);
            var formattedModel = new CodeFileRenderer().Render(codeModel).CodeLines;
            var formattedString = string.Join(Environment.NewLine, formattedModel.Select(l => l.DisplayString));
            _testOutputHelper.WriteLine(formattedString);
            if(formatted != formattedString)
            {
                _testOutputHelper.WriteLine(String.Empty);
                _testOutputHelper.WriteLine("Expected:");
                _testOutputHelper.WriteLine(formatted);
            }
            Assert.Equal(formatted, formattedString);
        }

        private string RemoveEmptyLines(string content)
        {
            var lines = content
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None) // handle both NewLine styles as on Windows they can be mismatched between the generated code and the expected code.
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            return String.Join(Environment.NewLine, lines);
        }

        public class NameSymbolOrderProvider : ICodeFileBuilderSymbolOrderProvider
        {
            public IEnumerable<T> OrderTypes<T>(IEnumerable<T> symbols) where T : ITypeSymbol
            {
                return symbols.OrderBy(s => s.Name);
            }

            public IEnumerable<ISymbol> OrderMembers(IEnumerable<ISymbol> members)
            {
                return members.OrderBy(s => s.Name);
            }

            public IEnumerable<INamespaceSymbol> OrderNamespaces(IEnumerable<INamespaceSymbol> namespaces)
            {
                return namespaces.OrderBy(s => s.ToDisplayString());
            }
        }
    }
}
