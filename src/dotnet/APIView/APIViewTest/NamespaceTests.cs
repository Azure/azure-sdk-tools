using APIView;
using System.Text;
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
            var builder = new StringBuilder();
            var renderer = new HTMLRendererAPIV();
            renderer.Render(ns, builder);
            Assert.Equal("<span class=\"keyword\">namespace</span> <span id=\"\" class=\"name\">TestNamespace</span> {<br />}", builder.ToString());
        }
    }
}
