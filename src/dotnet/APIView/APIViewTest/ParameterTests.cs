using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class ParameterTests
    {
        [Fact]
        public void ParameterTestNoRefKindStringDefaultValue()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var methodSymbol = (IMethodSymbol)a.GetTypeByMetadataName("TestLibrary.PublicInterface`1").GetMembers("TypeParamParamsMethod").Single();
            MethodAPIV method = new MethodAPIV(methodSymbol);

            ImmutableArray<ParameterAPIV> parameters = method.Parameters;
            Assert.Equal(2, parameters.Length);

            ParameterAPIV param = null;
            ParameterAPIV num = null;
            foreach (ParameterAPIV p in parameters)
            {
                if (p.Name.Equals("param"))
                    param = p;
                else
                    num = p;
            }

            Assert.False(param == null || num == null);
            Assert.Equal("T", param.Type);
            Assert.Equal("param", param.Name);
            Assert.Null(param.ExplicitDefaultValue);

            Assert.Equal("string", num.Type);
            Assert.Equal("str", num.Name);
            Assert.Equal("hello", num.ExplicitDefaultValue);
        }

        [Fact]
        public void ParameterTestSomeRefKindNoDefaultValue()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var methodSymbol = (IMethodSymbol)a.GetTypeByMetadataName("TestLibrary.PublicInterface`1").GetMembers("RefKindParamMethod").Single();
            MethodAPIV method = new MethodAPIV(methodSymbol);

            ImmutableArray<ParameterAPIV> parameters = method.Parameters;
            Assert.Single(parameters);

            Assert.Equal("ref string", parameters[0].Type);
            Assert.Equal("str", parameters[0].Name);
            Assert.Null(parameters[0].ExplicitDefaultValue);
        }
    }
}
