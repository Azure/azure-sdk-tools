using Microsoft.CodeAnalysis;
using APIView;
using Xunit;
using System;
using System.Text;
using Azure.Storage.Blobs.Models;

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
            Assert.Equal("System.Diagnostics.ConditionalAttribute", method.Attributes[0].Type);
            Assert.Single(method.Attributes[0].ConstructorArgs);
            Assert.Equal("\"DEBUG\"", method.Attributes[0].ConstructorArgs[0]);

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
            Assert.Equal("TestLibrary.CustomAttribute", method.Attributes[0].Type);
            Assert.Single(method.Attributes[0].ConstructorArgs);
            Assert.Equal("\"Test\"", method.Attributes[0].ConstructorArgs[0]);
            Assert.Equal("TestLibrary.NewAttribute", method.Attributes[1].Type);
            Assert.Empty(method.Attributes[1].ConstructorArgs);

            Assert.Empty(method.Parameters);
            Assert.Equal(2, method.TypeParameters.Length);
        }

        [Fact]
        public void MethodTestMultipleAttributesMultipleTypeParamsNoParamsStringRep()
        {
            var methodSymbol = (IMethodSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1", "AttributesTypeParamsMethod");
            MethodAPIV method = new MethodAPIV(methodSymbol);

            string stringRep = method.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("[TestLibrary.CustomAttribute(\"Test\")][TestLibrary.NewAttribute]int AttributesTypeParamsMethod<T, R>();", stringRep);
        }

        [Fact]
        public void MethodTestConstructorHTMLRender()
        {
            var p = new ParameterAPIV
            {
                Type = "int",
                Name = "num",
                HasExplicitDefaultValue = true,
                ExplicitDefaultValue = 2,
                Attributes = new string[] { }
            };

            var m = new MethodAPIV
            {
                Name = "TestClass",
                ReturnType = "",
                Accessibility = "public",
                IsConstructor = true,
                IsInterfaceMethod = false,
                IsStatic = false,
                IsVirtual = false,
                IsSealed = false,
                IsOverride = false,
                IsAbstract = false,
                IsExtern = false,
                Attributes = new AttributeAPIV[] { },
                Parameters = new ParameterAPIV[] { p },
                TypeParameters = new TypeParameterAPIV[] { }
            };
            var builder = new StringBuilder();
            var renderer = new HTMLRendererAPIV();
            renderer.Render(m, builder);
            Assert.Equal("<span class=\"keyword\">public</span> <span class=\"class\">TestClass</span>(<span class=\"type\">int</span> num" +
                " = <span class=\"value\">2</span>) { }", builder.ToString());
        }

        [Fact]
        public void MethodTestAttributesHTMLRender()
        {
            var a = new AttributeAPIV
            {
                Type = "TestAttribute",
                ConstructorArgs = new string[] {"Test", "\"String\""}
            };

            var m = new MethodAPIV
            {
                Name = "TestMethod",
                ReturnType = "void",
                Accessibility = "public",
                IsConstructor = false,
                IsInterfaceMethod = false,
                IsStatic = false,
                IsVirtual = false,
                IsSealed = false,
                IsOverride = false,
                IsAbstract = false,
                IsExtern = false,
                Attributes = new AttributeAPIV[] { a },
                Parameters = new ParameterAPIV[] { },
                TypeParameters = new TypeParameterAPIV[] { }
            };
            var builder = new StringBuilder();
            var renderer = new HTMLRendererAPIV();
            renderer.Render(m, builder);
            Assert.Equal("[<span class=\"class\">TestAttribute</span>(<span class=\"value\">Test</span>, <span class=\"value\">\"String\"</span>)]<br />" +
                "<span class=\"keyword\">public</span> <span class=\"keyword\">void</span> <span class=\"name\">TestMethod</span>() { }", builder.ToString());
        }
    }
}
