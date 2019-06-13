using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

            Assert.Equal(2, method.Parameters.Length);

            ParameterAPIV param = null;
            ParameterAPIV num = null;
            foreach (ParameterAPIV p in method.Parameters)
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

            Assert.Single(method.Parameters);

            Assert.Equal("ref string", method.Parameters[0].Type);
            Assert.Equal("str", method.Parameters[0].Name);
            Assert.Null(method.Parameters[0].ExplicitDefaultValue);
        }
    }
}
