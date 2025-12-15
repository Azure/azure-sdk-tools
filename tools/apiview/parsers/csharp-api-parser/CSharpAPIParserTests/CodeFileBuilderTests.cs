using CSharpAPIParser.TreeToken;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSharpAPIParserTests
{
    public class CodeFileBuilderTests
    {
        [Theory]
        // Informational version with build metadata -> strip after '+'
        [InlineData(@"[assembly: System.Reflection.AssemblyInformationalVersion(""1.2.3+commitsha"")] class Dummy {}", "1.2.3")]
        [InlineData(@"[assembly: System.Reflection.AssemblyInformationalVersion("" 7.8.9 "" )] class Dummy {}", "7.8.9")]
        [InlineData(@"[assembly: System.Reflection.AssemblyVersion(""4.5.6.7"")] [assembly: System.Reflection.AssemblyInformationalVersion(""   "" )] class Dummy {}", null)]
        [InlineData(@"[assembly: System.Reflection.AssemblyInformationalVersion(""2.0.0+meta+extra"")] class Dummy {}", "2.0.0")]
        public void GetPackageVersion_StripsBuildMetadata(string source, string expected)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create(
                "TestAsm",
                new[] { tree },
                new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Reflection.AssemblyInformationalVersionAttribute).Assembly.Location)
                },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var version = CodeFileBuilder.GetPackageVersion(compilation.Assembly);

            Assert.Equal(expected, version);
        }
    }
}
