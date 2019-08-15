using ApiView;
using System;
using Xunit;

namespace APIViewTest
{
    public class NamespaceTests
    {
        [Fact]
        public void NamespaceTestGlobalNoNamedTypesSomenamespaces()
        {
            AssemblyApiView assembly = new AssemblyApiView(TestResource.GetAssemblySymbol());
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceApiView globalNamespace = assembly.GlobalNamespace;
            Assert.Empty(globalNamespace.NamedTypes);
            Assert.Single(globalNamespace.Namespaces);
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNonamespaces()
        {
            AssemblyApiView assembly = new AssemblyApiView(TestResource.GetAssemblySymbol());
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceApiView globalNamespace = assembly.GlobalNamespace;
            NamespaceApiView nestedNamespace = globalNamespace.Namespaces[0];

            Assert.Equal("TestLibrary", nestedNamespace.Name);

            Assert.NotEmpty(nestedNamespace.NamedTypes);
            Assert.Empty(nestedNamespace.Namespaces);
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNonamespacesStringRep()
        {
            AssemblyApiView assembly = new AssemblyApiView(TestResource.GetAssemblySymbol());
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceApiView globalNamespace = assembly.GlobalNamespace;
            NamespaceApiView nestedNamespace = globalNamespace.Namespaces[0];

            Assert.Contains("namespace TestLibrary {", nestedNamespace.ToString());
        }

        [Fact]
        public void NamespaceTestEmptyImplementingHTMLRender()
        {
            var ns = new NamespaceApiView
            {
                Name = "TestNamespace",
                NamedTypes = new NamedTypeApiView[] { },
                Namespaces = new NamespaceApiView[] { }
            };
            var renderer = new HTMLRendererApiView();
            var list = new StringListApiView();
            renderer.Render(ns, list);
            Assert.Equal("", list.ToString());
        }

        [Fact]
        public void NamespaceTestImplementingHTMLRender()
        {
            var ns = new NamespaceApiView
            {
                Name = "TestNamespace",
                NamedTypes = new NamedTypeApiView[] {
                    new NamedTypeApiView() {
                        Accessibility = "public",
                        Events = new EventApiView[] { },
                        Fields = new FieldApiView[] { },
                        Id = "",
                        Implementations = new TypeReferenceApiView[] { },
                        IsSealed = false,
                        IsStatic = false,
                        Methods = new MethodApiView[] { },
                        Name = "TestNamedType",
                        NamedTypes = new NamedTypeApiView[] { },
                        Properties = new PropertyApiView[] { },
                        TypeKind = "class",
                        TypeParameters = new TypeParameterApiView[] { }
                    } 
                },
                Namespaces = new NamespaceApiView[] { }
            };
            var renderer = new HTMLRendererApiView();
            var list = new StringListApiView();
            renderer.Render(ns, list);
            Assert.Equal("<span class=\"keyword\">namespace</span> <a id=\"\" class=\"name commentable\">TestNamespace</a> {" 
                + Environment.NewLine + "    <span class=\"keyword\">public</span> <span class=\"keyword\">class</span> <a h"
                + "ref=\"#\" id=\"\" class=\"class commentable\">TestNamedType</a> {" + Environment.NewLine + "    }" 
                + Environment.NewLine + "}", list.ToString());
        }
    }
}
