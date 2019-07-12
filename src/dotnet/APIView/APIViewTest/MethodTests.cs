using Microsoft.CodeAnalysis;
using APIView;
using Xunit;
using System;
using System.Text;
using System.Reflection;

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
            Assert.Equal("int", method.ReturnType.Tokens[0].DisplayString);

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
            Assert.Equal("void", method.ReturnType.Tokens[0].DisplayString);

            Assert.Single(method.Attributes);
            Assert.Equal("System", method.Attributes[0].Type.Tokens[0].DisplayString);
            Assert.Equal(".", method.Attributes[0].Type.Tokens[1].DisplayString);
            Assert.Equal("Diagnostics", method.Attributes[0].Type.Tokens[2].DisplayString);
            Assert.Equal(".", method.Attributes[0].Type.Tokens[3].DisplayString);
            Assert.Equal("ConditionalAttribute", method.Attributes[0].Type.Tokens[4].DisplayString);
            Assert.Single(method.Attributes[0].ConstructorArgs);
            Assert.Equal("\"DEBUG\"", method.Attributes[0].ConstructorArgs[0].Value);

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
            Assert.Equal("int", method.ReturnType.Tokens[0].DisplayString);

            Assert.Equal(2, method.Attributes.Length);
            Assert.Equal("TestLibrary", method.Attributes[0].Type.Tokens[0].DisplayString);
            Assert.Equal(".", method.Attributes[0].Type.Tokens[1].DisplayString);
            Assert.Equal("CustomAttribute", method.Attributes[0].Type.Tokens[2].DisplayString);

            Assert.Equal(2, method.Attributes[0].ConstructorArgs.Length);
            Assert.Equal("\"Test\"", method.Attributes[0].ConstructorArgs[0].Value);
            Assert.Equal("Named", method.Attributes[0].ConstructorArgs[1].Name);
            Assert.Equal("\"Param\"", method.Attributes[0].ConstructorArgs[1].Value);

            Assert.Equal("TestLibrary", method.Attributes[1].Type.Tokens[0].DisplayString);
            Assert.Equal(".", method.Attributes[1].Type.Tokens[1].DisplayString);
            Assert.Equal("NewAttribute", method.Attributes[1].Type.Tokens[2].DisplayString);
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
            Assert.Equal("[TestLibrary.CustomAttribute(\"Test\", Named = \"Param\")][TestLibrary.NewAttribute]int AttributesTypeParamsMethod<T, R>();", stringRep);
        }

        [Fact]
        public void MethodTestConstructorHTMLRender()
        {
            var p = new ParameterAPIV
            {
                Type = new TypeReferenceAPIV(new TokenAPIV[] { new TokenAPIV("int", TypeReferenceAPIV.TokenType.BuiltInType) }),
                Name = "num",
                HasExplicitDefaultValue = true,
                ExplicitDefaultValue = 2,
                Attributes = new string[] { }
            };

            var m = new MethodAPIV
            {
                Name = "TestClass",
                ReturnType = null,
                Accessibility = "public",
                ClassNavigationID = "TestClass",
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
            Assert.Equal("<span class=\"keyword\">public</span> <a href=\"#TestClass\" class=\"class\">TestClass</a>(<span class=\"keyword\">int</span> num" +
                " = <span class=\"value\">2</span>) { }", builder.ToString());
        }

        [Fact]
        public void MethodTestAttributesHTMLRender()
        {
            var arg1 = new AttributeConstructArgAPIV
            {
                Value = "Test"
            };
            var arg2 = new AttributeConstructArgAPIV
            {
                Value = "\"String\""
            };

            var a = new AttributeAPIV
            {
                Type = new TypeReferenceAPIV(new TokenAPIV[] { new TokenAPIV("TestAttribute", TypeReferenceAPIV.TokenType.ClassType) }),
                ConstructorArgs = new AttributeConstructArgAPIV[] { arg1, arg2 }
            };

            var m = new MethodAPIV
            {
                Name = "TestMethod",
                ReturnType = new TypeReferenceAPIV(new TokenAPIV[] { new TokenAPIV("void", TypeReferenceAPIV.TokenType.BuiltInType) }),
                Accessibility = "public",
                ClassNavigationID = "",
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
            Assert.Equal("[<a href=\"#\" class=\"class\">TestAttribute</a>(<span class=\"value\">Test</span>, <span class=\"value\">\"String\"</span>)]<br />" +
                "<span class=\"keyword\">public</span> <span class=\"keyword\">void</span> <span class=\"name\">TestMethod</span>() { }", builder.ToString());
        }
    }
}
