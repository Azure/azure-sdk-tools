using Microsoft.CodeAnalysis;
using APIView;
using Xunit;
using System;

namespace APIViewTest
{
    public class MethodTests
    {
        [Fact]
        public void MethodTestNoAttributesOneTypeParamMultipleParams()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "TypeParamParamsMethod");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.True(method.IsInterfaceMethod);
            Assert.False(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.False(method.IsSealed);
            Assert.False(method.IsOverride);
            Assert.True(method.IsAbstract);
            Assert.False(method.IsExtern);
            Assert.Equal("int", method.ReturnType);

            Assert.Empty(method.Attributes);
            Assert.Equal(2, method.Parameters.Length);
            Assert.Single(method.TypeParameters);
        }

        [Fact]
        public void MethodTestNoAttributesOneTypeParamMultipleParamsStringRep()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "TypeParamParamsMethod");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.Equal("int TypeParamParamsMethod<T>(T param, string str = \"hello\");", method.ToString());
        }

        [Fact]
        public void MethodTestOneAttributeNoTypeParamsOneParam()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "StaticVoid");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.False(method.IsInterfaceMethod);
            Assert.True(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.False(method.IsSealed);
            Assert.False(method.IsOverride);
            Assert.False(method.IsAbstract);
            Assert.False(method.IsExtern);
            Assert.Equal("void", method.ReturnType);

            Assert.Single(method.Attributes);
            Assert.Single(method.Parameters);
            Assert.Empty(method.TypeParameters);
        }

        [Fact]
        public void MethodTestOneAttributeNoTypeParamsOneParamStringRep()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "StaticVoid");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            string stringRep = method.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("[System.Diagnostics.ConditionalAttribute(\"DEBUG\")]public static void StaticVoid(string[] args) { }", stringRep);
        }

        [Fact]
        public void MethodTestMultipleAttributesMultipleTypeParamsNoParams()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "AttributesTypeParamsMethod");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.True(method.IsInterfaceMethod);
            Assert.False(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.False(method.IsSealed);
            Assert.False(method.IsOverride);
            Assert.True(method.IsAbstract);
            Assert.False(method.IsExtern);
            Assert.Equal("int", method.ReturnType);

            Assert.Equal(2, method.Attributes.Length);
            Assert.Empty(method.Parameters);
            Assert.Equal(2, method.TypeParameters.Length);
        }

        [Fact]
        public void MethodTestMultipleAttributesMultipleTypeParamsNoParamsStringRep()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "AttributesTypeParamsMethod");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            string stringRep = method.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("[TestLibrary.CustomAttribute(\"Test\"), TestLibrary.NewAttribute]int AttributesTypeParamsMethod<T, R>();", stringRep);
        }
    }
}
