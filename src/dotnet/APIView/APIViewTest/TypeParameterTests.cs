using Microsoft.CodeAnalysis;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class TypeParameterTests
    {
        [Fact]
        public void TypeParameterTestCreation()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "TypeParamParamsMethod");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.Single(method.TypeParameters);
            Assert.Equal("T", method.TypeParameters[0].Name);
        }
    }
}
