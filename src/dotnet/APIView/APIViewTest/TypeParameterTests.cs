using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class TypeParameterTests
    {
        [Fact]
        public void TypeParameterTestCreation()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var methodSymbol = (IMethodSymbol)a.GetTypeByMetadataName("TestLibrary.PublicInterface`1").GetMembers("TypeParamParamsMethod").Single();
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.Single(method.TypeParameters);
            Assert.Equal("T", method.TypeParameters[0].Name);
        }
    }
}
