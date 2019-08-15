using Microsoft.CodeAnalysis;
using ApiView;
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
            NamedTypeApiView publicClass = new NamedTypeApiView(namedTypeSymbol);

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
            NamedTypeApiView publicClass = new NamedTypeApiView(namedTypeSymbol);

            Assert.Contains("public class SomeEventsSomeFieldsNoMethodsSomeNamedTypes {", publicClass.ToString());
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypes()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1");
            NamedTypeApiView publicInterface = new NamedTypeApiView(namedTypeSymbol);

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
            NamedTypeApiView publicInterface = new NamedTypeApiView(namedTypeSymbol);

            Assert.Contains("public interface PublicInterface<T> {", publicInterface.ToString());
        }

        [Fact]
        public void NamedTypeTestImplementsInterface()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass");
            NamedTypeApiView implementer = new NamedTypeApiView(namedTypeSymbol);

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
            NamedTypeApiView implementer = new NamedTypeApiView(namedTypeSymbol);

            Assert.Contains("public class ImplementingClass : TestLibrary.PublicInterface<int> {", implementer.ToString());
        }

        [Fact]
        public void NamedTypeTestEnumDefaultUnderlyingType()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEnum");
            NamedTypeApiView publicEnum = new NamedTypeApiView(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.TypeKind);
            Assert.Equal("int", publicEnum.EnumUnderlyingType.Tokens[0].DisplayString);
        }
        
        [Fact]
        public void NamedTypeTestEnumDefaultUnderlyingTypeStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEnum");
            NamedTypeApiView publicEnum = new NamedTypeApiView(namedTypeSymbol);

            string stringRep = publicEnum.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("public enum PublicEnum {    One = 0,    Two = 1,    Three = 2,}", stringRep);
        }
        
        [Fact]
        public void NamedTypeTestEnumDeclaredUnderlyingType()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass", "PublicEnum");
            NamedTypeApiView publicEnum = new NamedTypeApiView(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.TypeKind);
            Assert.Equal("long", publicEnum.EnumUnderlyingType.Tokens[0].DisplayString);
        }

        [Fact]
        public void NamedTypeTestEnumDeclaredUnderlyingTypeStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass", "PublicEnum");
            NamedTypeApiView publicEnum = new NamedTypeApiView(namedTypeSymbol);

            string stringRep = publicEnum.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("public enum PublicEnum : long {    One = 1,    Two = 2,    Three = 3,}", stringRep);
        }
        
        [Fact]
        public void NamedTypeTestDelegate()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.publicDelegate");
            NamedTypeApiView publicDelegate = new NamedTypeApiView(namedTypeSymbol);

            Assert.Equal("publicDelegate", publicDelegate.Name);
            Assert.Equal("delegate", publicDelegate.TypeKind);
        }

        [Fact]
        public void NamedTypeTestDelegateStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.publicDelegate");
            NamedTypeApiView publicDelegate = new NamedTypeApiView(namedTypeSymbol);

            Assert.Equal("public delegate int publicDelegate(int num = 10) { }", publicDelegate.ToString().Replace(Environment.NewLine, ""));
        }
        
        [Fact]
        public void NamedTypeTestAutomaticConstructor()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeApiView publicClass = new NamedTypeApiView(namedTypeSymbol);

            foreach (MethodApiView method in publicClass.Methods)
            {
                Assert.NotEqual("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", method.Name);
                Assert.NotEqual(".ctor", method.Name);
            }
        }

        [Fact]
        public void NamedTypeTestExplicitConstructor()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass");
            NamedTypeApiView publicClass = new NamedTypeApiView(namedTypeSymbol);

            bool constructorFound = false;
            foreach (MethodApiView method in publicClass.Methods)
            {
                if (method.Name.Equals("PublicClass"))
                    constructorFound = true;
            }

            Assert.True(constructorFound);
        }

        [Fact]
        public void NamedTypeTestImplementingHTMLRender()
        {
            var p = new PropertyApiView
            {
                Name = "TestProperty",
                Type = new TypeReferenceApiView(new TokenApiView[] { new TokenApiView("string", TypeReferenceApiView.TokenType.BuiltInType) }),
                Accessibility = "protected",
                IsAbstract = false,
                IsVirtual = false,
                HasSetMethod = true
            };

            var nt = new NamedTypeApiView
            {
                Name = "ImplementingClass",
                TypeKind = "class",
                Accessibility = "public",
                Id = "ImplementingClass",
                Events = new EventApiView[] { },
                Fields = new FieldApiView[] { },
                Implementations = new TypeReferenceApiView[] { new TypeReferenceApiView(new TokenApiView[] { new TokenApiView("BaseClass", TypeReferenceApiView.TokenType.ClassType) }) },
                Methods = new MethodApiView[] { },
                NamedTypes = new NamedTypeApiView[] { },
                Properties = new PropertyApiView[] { p },
                TypeParameters = new TypeParameterApiView[] { }
            };
            var renderer = new HTMLRendererApiView();
            var list = new StringListApiView();
            renderer.Render(nt, list);
            Assert.Equal("<span class=\"keyword\">public</span> <span class=\"keyword\">class</span> <a href=\"#\" id=\"ImplementingClass\" class=\"class commentable\">ImplementingClass</a> : " +
                "<a href=\"#\" class=\"class\">BaseClass</a> {" + Environment.NewLine + "    <span class=\"keyword\">protected</span> <span class=\"keyword\">string</span> <a id=\"\" class" +
                "=\"name commentable\">TestProperty</a> { <span class=\"keyword\">get</span>; <span class=\"keyword\">set</span>; }" + Environment.NewLine + "}", list.ToString());
        }

        [Fact]
        public void NamedTypeTestTypeParamHTMLRender()
        {
            var tp = new TypeParameterApiView
            {
                Name = "T",
                Attributes = new string[] { }
            };

            var nt = new NamedTypeApiView
            {
                Name = "TestInterface",
                TypeKind = "interface",
                Accessibility = "public",
                Id = "TestInterface",
                Events = new EventApiView[] { },
                Fields = new FieldApiView[] { },
                Implementations = new TypeReferenceApiView[] { },
                Methods = new MethodApiView[] { },
                NamedTypes = new NamedTypeApiView[] { },
                Properties = new PropertyApiView[] { },
                TypeParameters = new TypeParameterApiView[] { tp }
            };
            var renderer = new HTMLRendererApiView();
            var list = new StringListApiView();
            renderer.Render(nt, list);
            Assert.Equal("<span class=\"keyword\">public</span> <span class=\"keyword\">interface</span> <a href=\"#\" id=\"TestInterface\" class=\"class commentable\">TestInterface</a>&lt;" +
                "<a href=\"#T\" class=\"type\">T</a>&gt; {" + Environment.NewLine + "}", list.ToString());
        }
    }
}
