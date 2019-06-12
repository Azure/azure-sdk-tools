using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class NamespaceTests
    {
        [Fact]
        public void NamespaceTestGlobalNoNamedTypesSomenamespaces()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamedTypeAPIV> namedTypes = globalNamespace.NamedTypes;
            Assert.Empty(namedTypes);

            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            Assert.Single(namespaces);
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNonamespaces()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            NamespaceAPIV nestedNamespace = globalNamespace.Namespaces[0];

            Assert.Equal("TestLibrary", nestedNamespace.Name);

            ImmutableArray<NamedTypeAPIV> NamedTypes = nestedNamespace.NamedTypes;
            Assert.NotEmpty(NamedTypes);

            ImmutableArray<NamespaceAPIV> namespaces = nestedNamespace.Namespaces;
            Assert.Empty(namespaces);
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNonamespacesStringRep()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            NamespaceAPIV nestedNamespace = globalNamespace.Namespaces[0];

            Assert.Contains("namespace TestLibrary {", nestedNamespace.ToString());
        }
    }
}
