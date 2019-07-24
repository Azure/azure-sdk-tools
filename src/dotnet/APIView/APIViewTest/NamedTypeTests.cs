using Microsoft.CodeAnalysis;
using APIView;
using Xunit;
using System;
using System.Text;
using TestLibrary;
using System.Collections.Generic;

namespace APIViewTest
{
    public class NamedTypeTests
    {
        [Fact]
        public void NamedTypeTestClassSomeEventsSomeFieldsNoMethodsSomeNamedTypes()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", publicClass.Name);
            Assert.Equal("class", publicClass.TypeKind);
            Assert.Equal(2, publicClass.Events.Length);
            Assert.Equal(2, publicClass.Fields.Length);
            Assert.Empty(publicClass.Methods);
            Assert.Equal(2, publicClass.NamedTypes.Length);
        }

        [Fact]
        public void NamedTypeTestClassSomeEventsSomeFieldsNoMethodsSomeNamedTypesStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public class SomeEventsSomeFieldsNoMethodsSomeNamedTypes {", publicClass.ToString());
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypes()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1");
            NamedTypeAPIV publicInterface = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicInterface", publicInterface.Name);
            Assert.Equal("interface", publicInterface.TypeKind);
            Assert.Empty(publicInterface.Events);
            Assert.Empty(publicInterface.Fields);
            Assert.Equal(3, publicInterface.Methods.Length);
            Assert.Empty(publicInterface.NamedTypes);
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypesStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1");
            NamedTypeAPIV publicInterface = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public interface PublicInterface<T> {", publicInterface.ToString());
        }

        [Fact]
        public void NamedTypeTestImplementsInterface()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass");
            NamedTypeAPIV implementer = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("ImplementingClass", implementer.Name);
            Assert.Equal("class", implementer.TypeKind);
            Assert.Single(implementer.Implementations);
            Assert.Equal("TestLibrary", implementer.Implementations[0].Tokens[0].DisplayString);
            Assert.Equal(".", implementer.Implementations[0].Tokens[1].DisplayString);
            Assert.Equal("PublicInterface", implementer.Implementations[0].Tokens[2].DisplayString);
            Assert.Equal("<", implementer.Implementations[0].Tokens[3].DisplayString);
            Assert.Equal("int", implementer.Implementations[0].Tokens[4].DisplayString);
            Assert.Equal(">", implementer.Implementations[0].Tokens[5].DisplayString);
        }

        [Fact]
        public void NamedTypeTestImplementsInterfaceStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass");
            NamedTypeAPIV implementer = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public class ImplementingClass : TestLibrary.PublicInterface<int> {", implementer.ToString());
        }

        [Fact]
        public void NamedTypeTestEnumDefaultUnderlyingType()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.TypeKind);
            Assert.Equal("int", publicEnum.EnumUnderlyingType.Tokens[0].DisplayString);
        }
        
        [Fact]
        public void NamedTypeTestEnumDefaultUnderlyingTypeStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            string stringRep = publicEnum.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("public enum PublicEnum {    One = 0,    Two = 1,    Three = 2,}", stringRep);
        }
        
        [Fact]
        public void NamedTypeTestEnumDeclaredUnderlyingType()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.TypeKind);
            Assert.Equal("long", publicEnum.EnumUnderlyingType.Tokens[0].DisplayString);
        }

        [Fact]
        public void NamedTypeTestEnumDeclaredUnderlyingTypeStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            string stringRep = publicEnum.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("public enum PublicEnum : long {    One = 1,    Two = 2,    Three = 3,}", stringRep);
        }
        
        [Fact]
        public void NamedTypeTestDelegate()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.publicDelegate");
            NamedTypeAPIV publicDelegate = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("publicDelegate", publicDelegate.Name);
            Assert.Equal("delegate", publicDelegate.TypeKind);
        }

        [Fact]
        public void NamedTypeTestDelegateStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.publicDelegate");
            NamedTypeAPIV publicDelegate = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("public delegate int publicDelegate(int num = 10) { }", publicDelegate.ToString().Replace(Environment.NewLine, ""));
        }
        
        [Fact]
        public void NamedTypeTestAutomaticConstructor()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            foreach (MethodAPIV method in publicClass.Methods)
            {
                Assert.NotEqual("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", method.Name);
                Assert.NotEqual(".ctor", method.Name);
            }
        }

        [Fact]
        public void NamedTypeTestExplicitConstructor()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            bool constructorFound = false;
            foreach (MethodAPIV method in publicClass.Methods)
            {
                if (method.Name.Equals("PublicClass"))
                    constructorFound = true;
            }

            Assert.True(constructorFound);
        }

        [Fact]
        public void NamedTypeTestImplementingHTMLRender()
        {
            var p = new PropertyAPIV
            {
                Name = "TestProperty",
                Type = new TypeReferenceAPIV(new TokenAPIV[] { new TokenAPIV("string", TypeReferenceAPIV.TokenType.BuiltInType) }),
                Accessibility = "protected",
                IsAbstract = false,
                IsVirtual = false,
                HasSetMethod = true
            };

            var nt = new NamedTypeAPIV
            {
                Name = "ImplementingClass",
                TypeKind = "class",
                Accessibility = "public",
                Id = "ImplementingClass",
                Events = new EventAPIV[] { },
                Fields = new FieldAPIV[] { },
                Implementations = new TypeReferenceAPIV[] { new TypeReferenceAPIV(new TokenAPIV[] { new TokenAPIV("BaseClass", TypeReferenceAPIV.TokenType.ClassType) }) },
                Methods = new MethodAPIV[] { },
                NamedTypes = new NamedTypeAPIV[] { },
                Properties = new PropertyAPIV[] { p },
                TypeParameters = new TypeParameterAPIV[] { }
            };
            var renderer = new HTMLRendererAPIV();
            var list = new StringListAPIV();
            renderer.Render(nt, list);
            Assert.Equal("<span class=\"keyword\">public</span> <span class=\"keyword\">class</span> <a href=\"#\" id=\"ImplementingClass\" class=\"class commentable\">ImplementingClass</a> : " +
                "<a href=\"#\" class=\"class\">BaseClass</a> {" + Environment.NewLine + "    <span class=\"keyword\">protected</span> <span class=\"keyword\">string</span> <a id=\"\" class" +
                "=\"name commentable\">TestProperty</a> { <span class=\"keyword\">get</span>; <span class=\"keyword\">set</span>; }" + Environment.NewLine + "}", list.ToString());
        }

        [Fact]
        public void NamedTypeTestTypeParamHTMLRender()
        {
            var tp = new TypeParameterAPIV
            {
                Name = "T",
                Attributes = new string[] { }
            };

            var nt = new NamedTypeAPIV
            {
                Name = "TestInterface",
                TypeKind = "interface",
                Accessibility = "public",
                Id = "TestInterface",
                Events = new EventAPIV[] { },
                Fields = new FieldAPIV[] { },
                Implementations = new TypeReferenceAPIV[] { },
                Methods = new MethodAPIV[] { },
                NamedTypes = new NamedTypeAPIV[] { },
                Properties = new PropertyAPIV[] { },
                TypeParameters = new TypeParameterAPIV[] { tp }
            };
            var renderer = new HTMLRendererAPIV();
            var list = new StringListAPIV();
            renderer.Render(nt, list);
            Assert.Equal("<span class=\"keyword\">public</span> <span class=\"keyword\">interface</span> <a href=\"#\" id=\"TestInterface\" class=\"class commentable\">TestInterface</a>&lt;" +
                "<a href=\"#T\" class=\"type\">T</a>&gt; {" + Environment.NewLine + "}", list.ToString());
        }
    }
}
