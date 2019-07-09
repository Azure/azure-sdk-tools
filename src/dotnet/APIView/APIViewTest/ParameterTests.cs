using Microsoft.CodeAnalysis;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class ParameterTests
    {
        [Fact]
        public void ParameterTestNoRefKindStringDefaultValue()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "TypeParamParamsMethod");
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
            Assert.Single(param.TypeParts);
            Assert.Equal("T", param.TypeParts[0].DisplayString);
            Assert.Equal(TypeReference.SpecialType, param.TypeParts[0].Type);
            Assert.Equal("param", param.Name);
            Assert.Null(param.ExplicitDefaultValue);

            Assert.Equal("str", num.Name);
            Assert.Equal("hello", num.ExplicitDefaultValue);
        }

        [Fact]
        public void ParameterTestSomeRefKindNoDefaultValue()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "RefKindParamMethod");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.Single(method.Parameters);

            var typeParts = method.Parameters[0].TypeParts;
            Assert.Equal(3, typeParts.Length);
            Assert.Equal("ref", typeParts[0].DisplayString);
            Assert.Equal(TypeReference.BuiltInType, typeParts[0].Type);
            Assert.Equal(" ", typeParts[1].DisplayString);
            Assert.Equal(TypeReference.SpecialType, typeParts[1].Type);
            Assert.Equal("string", typeParts[2].DisplayString);
            Assert.Equal(TypeReference.BuiltInType, typeParts[2].Type);
            Assert.Equal("str", method.Parameters[0].Name);
            Assert.Null(method.Parameters[0].ExplicitDefaultValue);
        }
    }
}
