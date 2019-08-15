using Microsoft.CodeAnalysis;
using ApiView;
using Xunit;

namespace APIViewTest
{
    public class ParameterTests
    {
        [Fact]
        public void ParameterTestNoRefKindStringDefaultValue()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "TypeParamParamsMethod");
            MethodApiView method = new MethodApiView(methodSymbol);

            Assert.Equal(2, method.Parameters.Length);

            ParameterApiView param = null;
            ParameterApiView num = null;
            foreach (ParameterApiView p in method.Parameters)
            {
                if (p.Name.Equals("param"))
                    param = p;
                else
                    num = p;
            }

            Assert.False(param == null || num == null);
            Assert.Single(param.Type.Tokens);
            Assert.Equal("T", param.Type.Tokens[0].DisplayString);
            Assert.Equal(TypeReferenceApiView.TokenType.TypeArgument, param.Type.Tokens[0].Type);
            Assert.Equal("param", param.Name);
            Assert.Null(param.ExplicitDefaultValue);

            Assert.Equal("str", num.Name);
            Assert.Equal("hello", num.ExplicitDefaultValue);
        }

        [Fact]
        public void ParameterTestSomeRefKindNoDefaultValue()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "RefKindParamMethod");
            MethodApiView method = new MethodApiView(methodSymbol);

            Assert.Single(method.Parameters);

            var typeParts = method.Parameters[0].Type.Tokens;
            Assert.Equal(RefKind.Ref, method.Parameters[0].RefKind);
            Assert.Single(typeParts);
            Assert.Equal("string", typeParts[0].DisplayString);
            Assert.Equal(TypeReferenceApiView.TokenType.BuiltInType, typeParts[0].Type);
            Assert.Equal("str", method.Parameters[0].Name);
            Assert.Null(method.Parameters[0].ExplicitDefaultValue);
        }
    }
}
