using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
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

            ImmutableArray<TypeParameterAPIV> parameters = method.TypeParameters;
            Assert.Single(parameters);
            Assert.Equal("T", parameters[0].Name);
        }
    }
}
