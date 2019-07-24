using APIView;
using System;
using Xunit;

namespace APIViewTest
{
    public class NamespaceTests
    {
        [Fact]
        public void NamespaceTestGlobalNoNamedTypesSomenamespaces()
        {
            AssemblyAPIV assembly = new AssemblyAPIV(TestResource.GetAssemblySymbol());
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            Assert.Empty(globalNamespace.NamedTypes);
            Assert.Single(globalNamespace.Namespaces);
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNonamespaces()
        {
            AssemblyAPIV assembly = new AssemblyAPIV(TestResource.GetAssemblySymbol());
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            NamespaceAPIV nestedNamespace = globalNamespace.Namespaces[0];

            Assert.Equal("TestLibrary", nestedNamespace.Name);

            Assert.NotEmpty(nestedNamespace.NamedTypes);
            Assert.Empty(nestedNamespace.Namespaces);
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNonamespacesStringRep()
        {
            AssemblyAPIV assembly = new AssemblyAPIV(TestResource.GetAssemblySymbol());
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            NamespaceAPIV nestedNamespace = globalNamespace.Namespaces[0];

            Assert.Contains("namespace TestLibrary {", nestedNamespace.ToString());
        }

        [Fact]
        public void NamespaceTestImplementingHTMLRender()
        {
            var ns = new NamespaceAPIV
            {
                Name = "TestNamespace",
                NamedTypes = new NamedTypeAPIV[] { },
                Namespaces = new NamespaceAPIV[] { }
            };
            var renderer = new HTMLRendererAPIV();
            var list = new StringListAPIV();
            renderer.Render(ns, list);
            Assert.Equal("<span class=\"keyword\">namespace</span> <a id=\"\" class=\"name commentable\">TestNamespace</a> {" 
                + Environment.NewLine + "}", list.ToString());
        }
    }
}
